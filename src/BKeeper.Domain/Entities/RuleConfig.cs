using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

/// <summary>Per-box override of a rule's parameters. Absence of a row means "use the rule's compiled default".</summary>
public class RuleConfig : BoxScopedEntity
{
    public string RuleCode { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Params { get; set; } = new();
    public int CooldownDaysAmber { get; set; } = 14;
    public int CooldownDaysRed { get; set; } = 7;
    public UserRole AudienceRole { get; set; } = UserRole.Coach;
}
