using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record PaymentDto(Guid Id, Guid MemberId, Guid? MembershipId, decimal Amount, string Currency,
    DateOnly PaymentDate, PaymentStatus Status, PaymentMethod Method, string? Notes);
public record CreatePaymentRequest(Guid? MembershipId, decimal Amount, string? Currency, DateOnly PaymentDate,
    PaymentStatus Status, PaymentMethod Method, string? Notes);
public record UpdatePaymentRequest(Guid? MembershipId, decimal Amount, string Currency, DateOnly PaymentDate,
    PaymentStatus Status, PaymentMethod Method, string? Notes);

/// <summary>
/// Record-keeping CRUD for payments the box collected elsewhere (cash, card terminal, bank transfer) —
/// no payment gateway/processor integration, see Payment's doc comment. Viewing and recording are both
/// gated to Owner/Manager/Reception: this is financial data, and Reception is the role the plan already
/// has handling membership-adjacent admin (plan §1's role table); Coach is intentionally excluded.
/// </summary>
[ApiController]
[Authorize]
public class PaymentsController(BKeeperDbContext db) : ControllerBase
{
    [HttpGet("members/{memberId:guid}/payments")]
    public async Task<ActionResult<List<PaymentDto>>> ListForMember(Guid memberId)
    {
        if (!CanAccessPayments()) return Forbid();

        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound();

        var payments = await db.Payments.Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentDto(p.Id, p.MemberId, p.MembershipId, p.Amount, p.Currency, p.PaymentDate, p.Status, p.Method, p.Notes))
            .ToListAsync();
        return Ok(payments);
    }

    [HttpPost("members/{memberId:guid}/payments")]
    public async Task<ActionResult<PaymentDto>> Create(Guid memberId, CreatePaymentRequest request)
    {
        if (!CanAccessPayments()) return Forbid();
        if (request.Amount <= 0) return BadRequest("Amount must be positive.");

        var member = await db.Members.FindAsync(memberId);
        if (member is null) return NotFound();

        var payment = new Payment
        {
            BoxId = member.BoxId,
            MemberId = memberId,
            MembershipId = request.MembershipId,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.Trim().ToUpperInvariant(),
            PaymentDate = request.PaymentDate,
            Status = request.Status,
            Method = request.Method,
            Notes = request.Notes,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return Ok(new PaymentDto(payment.Id, payment.MemberId, payment.MembershipId, payment.Amount, payment.Currency, payment.PaymentDate, payment.Status, payment.Method, payment.Notes));
    }

    [HttpPut("payments/{id:guid}")]
    public async Task<ActionResult<PaymentDto>> Update(Guid id, UpdatePaymentRequest request)
    {
        if (!CanAccessPayments()) return Forbid();
        if (request.Amount <= 0) return BadRequest("Amount must be positive.");

        var payment = await db.Payments.FindAsync(id);
        if (payment is null) return NotFound();

        payment.MembershipId = request.MembershipId;
        payment.Amount = request.Amount;
        payment.Currency = request.Currency.Trim().ToUpperInvariant();
        payment.PaymentDate = request.PaymentDate;
        payment.Status = request.Status;
        payment.Method = request.Method;
        payment.Notes = request.Notes;
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new PaymentDto(payment.Id, payment.MemberId, payment.MembershipId, payment.Amount, payment.Currency, payment.PaymentDate, payment.Status, payment.Method, payment.Notes));
    }

    private bool CanAccessPayments()
    {
        var role = User.FindFirst("role")?.Value;
        return role is "Owner" or "Manager" or "Reception";
    }
}
