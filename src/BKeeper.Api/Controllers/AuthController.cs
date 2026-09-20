using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Auth;
using BKeeper.Infrastructure.Identity;
using BKeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Api.Controllers;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, string DisplayName, string Role, string BoxId);
public record BootstrapRequest(string BoxName, string OwnerEmail, string OwnerPassword, string OwnerName);

[ApiController]
[Route("auth")]
public class AuthController(UserManager<ApplicationUser> userManager, JwtTokenService tokenService, BKeeperDbContext db) : ControllerBase
{
    /// <summary>Dev/first-run only: creates the first Box + Owner user. Refuses once any Box exists.</summary>
    [HttpPost("bootstrap")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Bootstrap(BootstrapRequest request)
    {
        if (await db.Boxes.AnyAsync()) return Conflict("A box already exists. Ask an Owner to invite you instead.");

        var box = new Box { Name = request.BoxName };
        db.Boxes.Add(box);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = request.OwnerEmail,
            Email = request.OwnerEmail,
            DisplayName = request.OwnerName,
            BoxId = box.Id,
            Role = UserRole.Owner,
        };
        var result = await userManager.CreateAsync(user, request.OwnerPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(new LoginResponse(tokenService.CreateToken(user), user.DisplayName, user.Role.ToString(), box.Id.ToString()));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized("Invalid email or password.");

        return Ok(new LoginResponse(tokenService.CreateToken(user), user.DisplayName, user.Role.ToString(), user.BoxId.ToString()));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        var user = await userManager.FindByIdAsync(userId ?? "");
        if (user is null) return Unauthorized();
        return Ok(new LoginResponse(tokenService.CreateToken(user), user.DisplayName, user.Role.ToString(), user.BoxId.ToString()));
    }
}
