using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

/// <summary>
/// A record of a payment that happened elsewhere (cash, card terminal, bank transfer, a third-party
/// platform) — the box logs it here for member-history purposes. Deliberately data-model-only: no
/// payment gateway/processor integration, no webhooks, no charging (see docs/DECISIONS.md — payments
/// were previously out of scope; this is the minimal record-keeping slice per revised product direction).
/// </summary>
public class Payment : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public Guid? MembershipId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateOnly PaymentDate { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
    public PaymentMethod Method { get; set; } = PaymentMethod.Other;
    public string? Notes { get; set; }

    public Member? Member { get; set; }
    public Membership? Membership { get; set; }
}
