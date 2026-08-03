using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletApi.Contracts;
using WalletApi.Extensions;
using WalletApi.Services;

namespace WalletApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IWalletService _wallet;

    public AccountsController(IWalletService wallet)
    {
        _wallet = wallet;
    }

    // GET /api/accounts/me — yalnızca kendi hesabını görürsün.
    // Hesap kimliği istekten değil token'dan gelir; başkasının bakiyesi sorgulanamaz.
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> Me(CancellationToken ct)
    {
        var account = await _wallet.GetAccountAsync(User.GetUserId(), ct);

        return Ok(new AccountResponse(account.Id, account.Balance, account.Currency, account.CreatedAt));
    }
}
