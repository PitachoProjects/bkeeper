namespace BKeeper.Domain.Enums;

public enum MemberStatus { Active, Frozen, Cancelled, Lapsed }

public enum MembershipStatus { Active, Frozen, Ended }

public enum BookingStatus { Booked, Attended, NoShow, LateCancel, Cancelled }

public enum ClassWindow { Early, Morning, Lunch, Afternoon, Evening }

public enum WorkoutTagType { Strength, Metcon, Gymnastics, Endurance, Hybrid, Skill, Mobility }

public enum WorkoutTagSource { Rule, Llm, Manual }

public enum AlertFamily { Attendance, Pattern, Onboarding, Goal, Eval, Membership, Winback, Positive }

public enum AlertSeverity { Info, Amber, Red }

public enum AlertStatus { New, Claimed, InProgress, Escalated, Snoozed, Resolved, AutoResolved, Reopened }

public enum AlertEventType { Created, Claimed, Escalated, Snoozed, Outreach, Resolved, Reopened, AutoResolved }

/// <summary>Source of a member note: who/what delivered the information.</summary>
public enum NoteSource { Coach, Import, Member, System }

public enum UserRole { Owner, Manager, Coach, Reception, Member }

public enum ImportRunStatus { Pending, Validating, Succeeded, PartialSuccess, Failed }
