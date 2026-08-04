using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletApi.Contracts;
using WalletApi.Domain;
using WalletApi.Extensions;
using WalletApi.Infrastructure;
using WalletApi.Services;

namespace WalletApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly IWalletService _wallet;

    public TransactionsController(IWalletService wallet)
    {
        _wallet = wallet;
    }

    // POST /api/transactions/deposit
    [HttpPost("deposit")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransactionResponse>> Deposit(AmountRequest request, CancellationToken ct)
    {
        var transaction = await _wallet.DepositAsync(
            User.GetUserId(), request.Amount, request.Description, ct);

        return Ok(ToResponse(transaction));
    }

    // POST /api/transactions/withdraw
    [HttpPost("withdraw")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionResponse>> Withdraw(AmountRequest request, CancellationToken ct)
    {
        var transaction = await _wallet.WithdrawAsync(
            User.GetUserId(), request.Amount, request.Description, ct);

        return Ok(ToResponse(transaction));
    }

    // POST /api/transactions/transfer
    [HttpPost("transfer")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionResponse>> Transfer(TransferRequest request, CancellationToken ct)
    {
        var transaction = await _wallet.TransferAsync(
            User.GetUserId(), request.ToEmail, request.Amount, request.Description, ct);

        return Ok(ToResponse(transaction));
    }

    // GET /api/transactions?take=50 — kendi hesabının işlem geçmişi.
    // Okuma işlemi zaten yan etkisizdir; idempotency anahtarına gerek yok.
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> History(
        CancellationToken ct, [FromQuery] int take = 50)
    {
        // Sayfa boyutunu sınırla: istemci "take=1000000" diyerek sunucuyu yoramaz.
        take = Math.Clamp(take, 1, 200);

        var transactions = await _wallet.GetHistoryAsync(User.GetUserId(), take, ct);

        return Ok(transactions.Select(ToResponse));
    }

    private static TransactionResponse ToResponse(Transaction t) =>
        new(t.Id, t.Type.ToString(), t.Amount, t.BalanceAfter, t.CounterpartyAccountId, t.Description, t.CreatedAt);
}
