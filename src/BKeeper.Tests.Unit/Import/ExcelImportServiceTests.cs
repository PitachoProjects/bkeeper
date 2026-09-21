using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Import;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Tests.Unit.Import;

public class ExcelImportServiceTests
{
    private static BKeeperDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<BKeeperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BKeeperDbContext(options, new CurrentBoxAccessor());
    }

    private static MemoryStream BuildWorkbook(Action<XLWorkbook> populate)
    {
        using var wb = new XLWorkbook();
        populate(wb);
        var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task ImportAsync_ParsesRealExcelDateCells_NotJustDateStrings()
    {
        using var db = NewDb();
        var boxId = Guid.NewGuid();
        using var stream = BuildWorkbook(wb =>
        {
            var ws = wb.Worksheets.Add("Members");
            ws.Cell(1, 1).Value = "member_id";
            ws.Cell(1, 2).Value = "name";
            ws.Cell(1, 3).Value = "join_date";
            ws.Cell(1, 4).Value = "status";
            ws.Cell(2, 1).Value = "M1";
            ws.Cell(2, 2).Value = "Ana Silva";
            ws.Cell(2, 3).Value = new DateTime(2023, 7, 28); // real date cell, not a string
            ws.Cell(2, 4).Value = "active";
        });

        var result = await new ExcelImportService(db).ImportAsync(boxId, stream, "members.xlsx");

        Assert.Empty(result.Issues);
        Assert.Equal(1, result.MembersUpserted);
        var member = await db.Members.SingleAsync(m => m.BoxId == boxId);
        Assert.Equal(new DateOnly(2023, 7, 28), member.JoinDate);
    }

    [Fact]
    public async Task ImportAsync_DedupesWorkoutsWithinTheSameRun_ByDateAndTitle()
    {
        using var db = NewDb();
        var boxId = Guid.NewGuid();
        using var stream = BuildWorkbook(wb =>
        {
            var ws = wb.Worksheets.Add("Classes");
            ws.Cell(1, 1).Value = "session_id";
            ws.Cell(1, 2).Value = "date";
            ws.Cell(1, 3).Value = "start_time";
            ws.Cell(1, 4).Value = "class_type";
            ws.Cell(1, 5).Value = "workout_title";

            var date = new DateTime(2024, 9, 23);
            ws.Cell(2, 1).Value = "S1"; ws.Cell(2, 2).Value = date; ws.Cell(2, 3).Value = "06:30"; ws.Cell(2, 4).Value = "CrossFit"; ws.Cell(2, 5).Value = "Back Squat 5x5";
            ws.Cell(3, 1).Value = "S2"; ws.Cell(3, 2).Value = date; ws.Cell(3, 3).Value = "09:00"; ws.Cell(3, 4).Value = "CrossFit"; ws.Cell(3, 5).Value = "Back Squat 5x5";
        });

        var result = await new ExcelImportService(db).ImportAsync(boxId, stream, "classes.xlsx");

        Assert.Empty(result.Issues);
        Assert.Equal(2, result.SessionsUpserted);
        Assert.Equal(1, await db.Workouts.CountAsync(w => w.BoxId == boxId));
    }

    [Fact]
    public async Task ImportAsync_WiresPlansFreezesAndGoals_ForAMember()
    {
        using var db = NewDb();
        var boxId = Guid.NewGuid();
        using var stream = BuildWorkbook(wb =>
        {
            var members = wb.Worksheets.Add("Members");
            members.Cell(1, 1).Value = "member_id"; members.Cell(1, 2).Value = "name";
            members.Cell(1, 3).Value = "join_date"; members.Cell(1, 4).Value = "plan"; members.Cell(1, 5).Value = "status";
            members.Cell(2, 1).Value = "M1"; members.Cell(2, 2).Value = "Ana Silva";
            members.Cell(2, 3).Value = new DateTime(2023, 7, 28); members.Cell(2, 4).Value = "3x/week"; members.Cell(2, 5).Value = "active";

            var plans = wb.Worksheets.Add("Plans");
            plans.Cell(1, 1).Value = "plan_name"; plans.Cell(1, 2).Value = "sessions_per_week"; plans.Cell(1, 3).Value = "monthly_price_eur";
            plans.Cell(2, 1).Value = "3x/week"; plans.Cell(2, 2).Value = 3; plans.Cell(2, 3).Value = 55;

            var freezes = wb.Worksheets.Add("Freezes");
            freezes.Cell(1, 1).Value = "freeze_id"; freezes.Cell(1, 2).Value = "member_id";
            freezes.Cell(1, 3).Value = "freeze_start"; freezes.Cell(1, 4).Value = "freeze_end"; freezes.Cell(1, 5).Value = "reason";
            freezes.Cell(2, 1).Value = "F1"; freezes.Cell(2, 2).Value = "M1";
            freezes.Cell(2, 3).Value = new DateTime(2024, 9, 30); freezes.Cell(2, 4).Value = new DateTime(2024, 10, 6); freezes.Cell(2, 5).Value = "holiday";
            freezes.Cell(3, 1).Value = "F2"; freezes.Cell(3, 2).Value = "M1";
            freezes.Cell(3, 3).Value = new DateTime(2025, 11, 10); freezes.Cell(3, 4).Value = new DateTime(2025, 11, 23); freezes.Cell(3, 5).Value = "travel";

            var goals = wb.Worksheets.Add("Goals");
            goals.Cell(1, 1).Value = "goal_id"; goals.Cell(1, 2).Value = "member_id"; goals.Cell(1, 3).Value = "category";
            goals.Cell(1, 4).Value = "description"; goals.Cell(1, 5).Value = "target_value"; goals.Cell(1, 6).Value = "unit"; goals.Cell(1, 7).Value = "target_date";
            goals.Cell(2, 1).Value = "G1"; goals.Cell(2, 2).Value = "M1"; goals.Cell(2, 3).Value = "skill";
            goals.Cell(2, 4).Value = "First muscle-up"; goals.Cell(2, 5).Value = 1; goals.Cell(2, 6).Value = "reps"; goals.Cell(2, 7).Value = new DateTime(2025, 4, 7);
        });

        var result = await new ExcelImportService(db).ImportAsync(boxId, stream, "sample.xlsx");

        Assert.Empty(result.Issues);
        Assert.Equal(1, result.MembershipsUpserted);
        Assert.Equal(2, result.FreezesUpserted);
        Assert.Equal(1, result.GoalsUpserted);

        var membership = await db.Memberships.Include(m => m.Freezes).SingleAsync(m => m.BoxId == boxId);
        Assert.Equal("3x/week", membership.PlanName);
        Assert.Equal(3, membership.PlanFreqPerWeek);
        Assert.Equal(55, membership.MonthlyPriceEur);
        Assert.Equal(2, membership.Freezes.Count);
        Assert.Contains(membership.Freezes, f => f.Reason == "holiday" && f.StartDate == new DateOnly(2024, 9, 30));

        var goal = await db.Goals.SingleAsync(g => g.BoxId == boxId);
        Assert.Equal(GoalCategory.Skill, goal.Category);
        Assert.Equal("First muscle-up", goal.Description);
        Assert.Equal(1, goal.TargetValue);
        Assert.Equal("reps", goal.Unit);
        Assert.Equal(new DateOnly(2025, 4, 7), goal.TargetDate);
    }

    [Fact]
    public async Task ImportAsync_IsIdempotent_ForMembershipsFreezesAndGoals()
    {
        using var db = NewDb();
        var boxId = Guid.NewGuid();
        MemoryStream Build() => BuildWorkbook(wb =>
        {
            var members = wb.Worksheets.Add("Members");
            members.Cell(1, 1).Value = "member_id"; members.Cell(1, 2).Value = "name";
            members.Cell(1, 3).Value = "join_date"; members.Cell(1, 4).Value = "plan"; members.Cell(1, 5).Value = "status";
            members.Cell(2, 1).Value = "M1"; members.Cell(2, 2).Value = "Ana Silva";
            members.Cell(2, 3).Value = new DateTime(2023, 7, 28); members.Cell(2, 4).Value = "3x/week"; members.Cell(2, 5).Value = "active";

            var freezes = wb.Worksheets.Add("Freezes");
            freezes.Cell(1, 1).Value = "freeze_id"; freezes.Cell(1, 2).Value = "member_id";
            freezes.Cell(1, 3).Value = "freeze_start"; freezes.Cell(1, 4).Value = "freeze_end"; freezes.Cell(1, 5).Value = "reason";
            freezes.Cell(2, 1).Value = "F1"; freezes.Cell(2, 2).Value = "M1";
            freezes.Cell(2, 3).Value = new DateTime(2024, 9, 30); freezes.Cell(2, 4).Value = new DateTime(2024, 10, 6); freezes.Cell(2, 5).Value = "holiday";

            var goals = wb.Worksheets.Add("Goals");
            goals.Cell(1, 1).Value = "goal_id"; goals.Cell(1, 2).Value = "member_id"; goals.Cell(1, 3).Value = "category";
            goals.Cell(1, 4).Value = "description"; goals.Cell(1, 5).Value = "unit";
            goals.Cell(2, 1).Value = "G1"; goals.Cell(2, 2).Value = "M1"; goals.Cell(2, 3).Value = "skill";
            goals.Cell(2, 4).Value = "First muscle-up"; goals.Cell(2, 5).Value = "reps";
        });

        var service = new ExcelImportService(db);
        using (var s1 = Build()) await service.ImportAsync(boxId, s1, "sample.xlsx");
        using (var s2 = Build()) await service.ImportAsync(boxId, s2, "sample.xlsx");

        Assert.Equal(1, await db.Memberships.CountAsync(m => m.BoxId == boxId));
        Assert.Equal(1, await db.MembershipFreezes.CountAsync(f => f.BoxId == boxId));
        Assert.Equal(1, await db.Goals.CountAsync(g => g.BoxId == boxId));
    }
}
