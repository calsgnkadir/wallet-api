using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApi.Contracts;
using WalletApi.Data;
using WalletApi.Domain;

namespace WalletApi.Controllers;

[ApiController]
[Route("api/[controller]")]
// Denetim kaydını yalnızca yöneticiler okuyabilir: içinde başka kullanıcıların
// hareketleri ve IP adresleri vardır.
[Authorize(Roles = nameof(UserRole.Admin))]
public class AuditController : ControllerBase
{
    private readonly WalletDbContext _db;

    public AuditController(WalletDbContext db)
    {
        _db = db;
    }

    // GET /api/audit?take=50&userId=...
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AuditEventResponse>>> List(
        CancellationToken ct,
        [FromQuery] int take = 50,
        [FromQuery] Guid? userId = null,
        [FromQuery] AuditAction? action = null)
    {
        take = Math.Clamp(take, 1, 200);

        var query = _db.AuditEvents.AsNoTracking();

        if (userId is not null)
        {
            query = query.Where(e => e.UserId == userId);
        }

        if (action is not null)
        {
            query = query.Where(e => e.Action == action);
        }

        var events = await query
            .OrderByDescending(e => e.OccurredAt)
            .Take(take)
            .ToListAsync(ct);

        return Ok(events.Select(e => new AuditEventResponse(
            e.Id,
            e.OccurredAt,
            e.Action.ToString(),
            e.Outcome.ToString(),
            e.UserId,
            e.TargetId,
            e.Amount,
            e.IpAddress,
            e.Details)));
    }
}
