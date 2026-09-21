namespace BKeeper.Application.Import;

public record ImportRowIssue(string Sheet, int Row, string Message);

public class ImportResult
{
    public Guid ImportRunId { get; set; }
    public int MembersUpserted { get; set; }
    public int SessionsUpserted { get; set; }
    public int BookingsUpserted { get; set; }
    public int NotesUpserted { get; set; }
    public int MembershipsUpserted { get; set; }
    public int FreezesUpserted { get; set; }
    public int GoalsUpserted { get; set; }
    public bool Succeeded { get; set; }
    public List<ImportRowIssue> Issues { get; set; } = new();
}

public interface IExcelImportService
{
    Task<ImportResult> ImportAsync(Guid boxId, Stream fileStream, string fileName, CancellationToken ct = default);
}
