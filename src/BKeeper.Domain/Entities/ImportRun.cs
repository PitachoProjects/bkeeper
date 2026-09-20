using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class ImportRun : BoxScopedEntity
{
    public string Source { get; set; } = "excel";
    public string FileName { get; set; } = string.Empty;
    public string? MappingProfile { get; set; }
    public int MembersUpserted { get; set; }
    public int SessionsUpserted { get; set; }
    public int BookingsUpserted { get; set; }
    public int NotesUpserted { get; set; }
    public int RowErrorCount { get; set; }
    public ImportRunStatus Status { get; set; } = ImportRunStatus.Pending;

    public List<ImportRowError> RowErrors { get; set; } = new();
}

public class ImportRowError : BoxScopedEntity
{
    public Guid ImportRunId { get; set; }
    public string Sheet { get; set; } = string.Empty;
    public int Row { get; set; }
    public string Message { get; set; } = string.Empty;

    public ImportRun? ImportRun { get; set; }
}
