using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

/// <summary>
/// Free-text note about a member (e.g. "recovering from knee injury"), shown on the member profile
/// and used by rule suppression (see InjuryFlagUntil) when a note is tagged as such by a coach.
/// Notes can be entered by staff or arrive from an import (e.g. an optional "Notes" sheet).
/// </summary>
public class MemberNote : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public string Text { get; set; } = string.Empty;
    public NoteSource Source { get; set; } = NoteSource.Coach;
    public Guid? AuthorUserId { get; set; }
    public bool IsActive { get; set; } = true;

    public Member? Member { get; set; }
}
