using System.Globalization;
using BKeeper.Application.Import;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Infrastructure.Import;

/// <summary>
/// Parses the fixed v1 Excel template (Members / Classes / Attendance / optional Notes sheets, §4 of
/// the plan) and upserts idempotently by (box_id, external_id).
/// ponytail: no column-mapping wizard and no email/phone fuzzy-match review queue yet — a single
/// fixed header layout and exact member_id matching only. Upgrade path: add a mapping-profile table
/// once a second box's export uses different headers.
/// </summary>
public class ExcelImportService(BKeeperDbContext db) : IExcelImportService
{
    public async Task<ImportResult> ImportAsync(Guid boxId, Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var run = new ImportRun { BoxId = boxId, Source = "excel", FileName = fileName, Status = ImportRunStatus.Validating };
        db.ImportRuns.Add(run);

        var result = new ImportResult { ImportRunId = run.Id };
        using var workbook = new XLWorkbook(fileStream);

        var members = await ImportMembersAsync(boxId, workbook, result, ct);
        var sessions = await ImportClassesAsync(boxId, workbook, result, ct);
        await ImportAttendanceAsync(boxId, workbook, members, sessions, result, ct);
        await ImportNotesAsync(boxId, workbook, members, result, ct);

        run.MembersUpserted = result.MembersUpserted;
        run.SessionsUpserted = result.SessionsUpserted;
        run.BookingsUpserted = result.BookingsUpserted;
        run.NotesUpserted = result.NotesUpserted;
        run.RowErrorCount = result.Issues.Count;
        run.Status = result.Issues.Count == 0 ? ImportRunStatus.Succeeded : ImportRunStatus.PartialSuccess;
        result.Succeeded = true;

        foreach (var issue in result.Issues)
            db.ImportRowErrors.Add(new ImportRowError { BoxId = boxId, ImportRunId = run.Id, Sheet = issue.Sheet, Row = issue.Row, Message = issue.Message });

        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<Dictionary<string, Member>> ImportMembersAsync(Guid boxId, XLWorkbook wb, ImportResult result, CancellationToken ct)
    {
        var byExternalId = new Dictionary<string, Member>();
        if (!wb.Worksheets.TryGetWorksheet("Members", out var ws)) return byExternalId;

        var existing = await db.Members.Where(m => m.BoxId == boxId).ToDictionaryAsync(m => m.ExternalId ?? "", ct);

        foreach (var (row, cells, rowNum) in ReadRows(ws))
        {
            var memberId = cells.GetValueOrDefault("member_id");
            var name = cells.GetValueOrDefault("name");
            if (string.IsNullOrWhiteSpace(memberId) || string.IsNullOrWhiteSpace(name))
            {
                result.Issues.Add(new ImportRowIssue("Members", rowNum, "member_id and name are required"));
                continue;
            }

            if (!TryParseDate(cells.GetValueOrDefault("join_date"), out var joinDate))
            {
                result.Issues.Add(new ImportRowIssue("Members", rowNum, "join_date is missing or not a valid date"));
                continue;
            }

            if (!existing.TryGetValue(memberId, out var member))
            {
                member = new Member { BoxId = boxId, ExternalId = memberId, JoinDate = joinDate };
                db.Members.Add(member);
                existing[memberId] = member;
            }

            member.Name = name;
            member.Email = cells.GetValueOrDefault("email");
            member.PhoneE164 = cells.GetValueOrDefault("phone");
            member.JoinDate = joinDate;
            member.Status = ParseStatus(cells.GetValueOrDefault("status"));
            if (TryParseDate(cells.GetValueOrDefault("cancel_date"), out var cancelDate)) member.CancelDate = cancelDate;
            member.CancelReason = cells.GetValueOrDefault("cancel_reason");
            member.UpdatedAt = DateTimeOffset.UtcNow;

            byExternalId[memberId] = member;
            result.MembersUpserted++;
        }

        return byExternalId;
    }

    private async Task<Dictionary<string, ClassSession>> ImportClassesAsync(Guid boxId, XLWorkbook wb, ImportResult result, CancellationToken ct)
    {
        var byExternalId = new Dictionary<string, ClassSession>();
        if (!wb.Worksheets.TryGetWorksheet("Classes", out var ws)) return byExternalId;

        var existing = await db.ClassSessions.Where(s => s.BoxId == boxId).ToDictionaryAsync(s => s.ExternalId ?? "", ct);

        foreach (var (row, cells, rowNum) in ReadRows(ws))
        {
            var sessionId = cells.GetValueOrDefault("session_id");
            var classType = cells.GetValueOrDefault("class_type");
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(classType))
            {
                result.Issues.Add(new ImportRowIssue("Classes", rowNum, "session_id and class_type are required"));
                continue;
            }

            if (!TryParseDate(cells.GetValueOrDefault("date"), out var date) ||
                !TryParseTime(cells.GetValueOrDefault("start_time"), out var time))
            {
                result.Issues.Add(new ImportRowIssue("Classes", rowNum, "date/start_time missing or invalid"));
                continue;
            }

            var startsAt = new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero);

            if (!existing.TryGetValue(sessionId, out var session))
            {
                session = new ClassSession { BoxId = boxId, ExternalId = sessionId };
                db.ClassSessions.Add(session);
                existing[sessionId] = session;
            }

            session.StartsAt = startsAt;
            session.ClassType = classType;
            session.CoachName = cells.GetValueOrDefault("coach");
            session.Window = ClassSession.WindowFor(time);
            if (int.TryParse(cells.GetValueOrDefault("capacity"), out var capacity)) session.Capacity = capacity;
            session.UpdatedAt = DateTimeOffset.UtcNow;

            var title = cells.GetValueOrDefault("workout_title");
            if (!string.IsNullOrWhiteSpace(title))
            {
                var description = cells.GetValueOrDefault("workout_description");
                session.WorkoutId = (await UpsertWorkoutAsync(boxId, date, title, description, ct)).Id;
            }

            byExternalId[sessionId] = session;
            result.SessionsUpserted++;
        }

        return byExternalId;
    }

    private async Task<Workout> UpsertWorkoutAsync(Guid boxId, DateOnly date, string title, string? description, CancellationToken ct)
    {
        var workout = await db.Workouts.Include(w => w.Tags)
            .FirstOrDefaultAsync(w => w.BoxId == boxId && w.Date == date && w.Title == title, ct);

        if (workout is null)
        {
            workout = new Workout { BoxId = boxId, Date = date, Title = title, Description = description, Source = "import" };
            db.Workouts.Add(workout);

            var classification = Application.Import.WorkoutClassifier.Classify(title, description);
            foreach (var (tag, weight) in classification)
            {
                db.WorkoutTags.Add(new WorkoutTag
                {
                    BoxId = boxId,
                    Workout = workout,
                    Tag = tag,
                    Weight = weight,
                    Source = WorkoutTagSource.Rule,
                    Confidence = Application.Import.WorkoutClassifier.Confidence(classification),
                });
            }
        }

        return workout;
    }

    private async Task ImportAttendanceAsync(Guid boxId, XLWorkbook wb, Dictionary<string, Member> members,
        Dictionary<string, ClassSession> sessions, ImportResult result, CancellationToken ct)
    {
        if (!wb.Worksheets.TryGetWorksheet("Attendance", out var ws)) return;

        if (members.Count == 0) members = await db.Members.Where(m => m.BoxId == boxId).ToDictionaryAsync(m => m.ExternalId ?? "", ct);
        if (sessions.Count == 0) sessions = await db.ClassSessions.Where(s => s.BoxId == boxId).ToDictionaryAsync(s => s.ExternalId ?? "", ct);

        var existingBookings = await db.Bookings.Where(b => b.BoxId == boxId)
            .ToDictionaryAsync(b => b.ExternalId ?? Guid.NewGuid().ToString(), ct);

        foreach (var (row, cells, rowNum) in ReadRows(ws))
        {
            var memberId = cells.GetValueOrDefault("member_id");
            var sessionId = cells.GetValueOrDefault("session_id");
            if (memberId is null || !members.TryGetValue(memberId, out var member))
            {
                result.Issues.Add(new ImportRowIssue("Attendance", rowNum, $"unknown member_id '{memberId}'"));
                continue;
            }
            if (sessionId is null || !sessions.TryGetValue(sessionId, out var session))
            {
                result.Issues.Add(new ImportRowIssue("Attendance", rowNum, $"unknown session_id '{sessionId}'"));
                continue;
            }

            var status = ParseBookingStatus(cells.GetValueOrDefault("status"));
            if (status is null)
            {
                result.Issues.Add(new ImportRowIssue("Attendance", rowNum, "status is missing or not recognised"));
                continue;
            }

            var bookingId = cells.GetValueOrDefault("booking_id");
            Booking? booking = null;
            if (bookingId is not null) existingBookings.TryGetValue(bookingId, out booking);
            booking ??= await db.Bookings.FirstOrDefaultAsync(b => b.BoxId == boxId && b.MemberId == member.Id && b.SessionId == session.Id, ct);

            if (booking is null)
            {
                booking = new Booking { BoxId = boxId, ExternalId = bookingId, MemberId = member.Id, SessionId = session.Id };
                db.Bookings.Add(booking);
                if (bookingId is not null) existingBookings[bookingId] = booking;
            }

            booking.Status = status.Value;
            if (TryParseDateTime(cells.GetValueOrDefault("booked_at"), out var bookedAt)) booking.BookedAt = bookedAt;
            booking.UpdatedAt = DateTimeOffset.UtcNow;

            result.BookingsUpserted++;
        }
    }

    private async Task ImportNotesAsync(Guid boxId, XLWorkbook wb, Dictionary<string, Member> members, ImportResult result, CancellationToken ct)
    {
        if (!wb.Worksheets.TryGetWorksheet("Notes", out var ws)) return;
        if (members.Count == 0) members = await db.Members.Where(m => m.BoxId == boxId).ToDictionaryAsync(m => m.ExternalId ?? "", ct);

        foreach (var (row, cells, rowNum) in ReadRows(ws))
        {
            var memberId = cells.GetValueOrDefault("member_id");
            var text = cells.GetValueOrDefault("note");
            if (memberId is null || !members.TryGetValue(memberId, out var member))
            {
                result.Issues.Add(new ImportRowIssue("Notes", rowNum, $"unknown member_id '{memberId}'"));
                continue;
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Issues.Add(new ImportRowIssue("Notes", rowNum, "note text is required"));
                continue;
            }

            var alreadyExists = await db.MemberNotes.AnyAsync(n => n.MemberId == member.Id && n.Text == text && n.IsActive, ct);
            if (alreadyExists) continue;

            db.MemberNotes.Add(new MemberNote { BoxId = boxId, MemberId = member.Id, Text = text, Source = NoteSource.Import });
            result.NotesUpserted++;
        }
    }

    private static IEnumerable<(IXLRow Row, Dictionary<string, string?> Cells, int RowNum)> ReadRows(IXLWorksheet ws)
    {
        var headerRow = ws.Row(1);
        var headers = new Dictionary<int, string>();
        foreach (var cell in headerRow.CellsUsed())
            headers[cell.Address.ColumnNumber] = cell.GetString().Trim().ToLowerInvariant();

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var cells = new Dictionary<string, string?>();
            foreach (var (col, header) in headers)
            {
                var value = row.Cell(col).GetString().Trim();
                cells[header] = string.IsNullOrEmpty(value) ? null : value;
            }
            yield return (row, cells, row.RowNumber());
        }
    }

    private static bool TryParseDate(string? value, out DateOnly date)
    {
        date = default;
        if (value is null) return false;
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;
        if (double.TryParse(value, out var oa)) { date = DateOnly.FromDateTime(DateTime.FromOADate(oa)); return true; }
        return false;
    }

    private static bool TryParseTime(string? value, out TimeOnly time)
    {
        time = default;
        if (value is null) return false;
        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out time)) return true;
        if (double.TryParse(value, out var oa)) { time = TimeOnly.FromDateTime(DateTime.FromOADate(oa)); return true; }
        return false;
    }

    private static bool TryParseDateTime(string? value, out DateTimeOffset dt)
    {
        dt = default;
        if (value is null) return false;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt)) return true;
        if (double.TryParse(value, out var oa)) { dt = new DateTimeOffset(DateTime.FromOADate(oa), TimeSpan.Zero); return true; }
        return false;
    }

    private static MemberStatus ParseStatus(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "frozen" => MemberStatus.Frozen,
        "cancelled" or "canceled" => MemberStatus.Cancelled,
        _ => MemberStatus.Active,
    };

    private static BookingStatus? ParseBookingStatus(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "attended" => BookingStatus.Attended,
        "no_show" or "no-show" => BookingStatus.NoShow,
        "late_cancel" or "late-cancel" => BookingStatus.LateCancel,
        "cancelled" or "canceled" => BookingStatus.Cancelled,
        "booked" => BookingStatus.Booked,
        _ => null,
    };
}
