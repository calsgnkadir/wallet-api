using Microsoft.AspNetCore.Mvc;

namespace WalletApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    // GET /api/health  ->  { "status": "ok", "time": "..." }
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            time = DateTimeOffset.UtcNow
        });
    }
}
