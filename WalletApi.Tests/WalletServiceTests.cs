using Microsoft.EntityFrameworkCore;
using WalletApi.Domain;
using WalletApi.Services;

namespace WalletApi.Tests;

public class WalletServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Deposit_ArtirirBakiyeyi()
    {
        var (userId, _) = await _db.AddUserWithAccountAsync("ayse@test.com");

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        await service.DepositAsync(userId, 1000m, "ilk yatirma");

        var account = await service.GetAccountAsync(userId);
        Assert.Equal(1000m, account.Balance);
    }

    [Fact]
    public async Task Deposit_IslemKaydinaSonrakiBakiyeyiYazar()
    {
        var (userId, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 250m);

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        var transaction = await service.DepositAsync(userId, 100m, null);

        Assert.Equal(TransactionType.Deposit, transaction.Type);
        Assert.Equal(100m, transaction.Amount);
        Assert.Equal(350m, transaction.BalanceAfter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999.99)]
    public async Task Deposit_SifirVeyaNegatifTutariReddeder(decimal amount)
    {
        var (userId, _) = await _db.AddUserWithAccountAsync("ayse@test.com");

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        var exception = await Assert.ThrowsAsync<WalletException>(
            () => service.DepositAsync(userId, amount, null));

        Assert.Equal(WalletErrorCode.InvalidAmount, exception.Code);
    }

    [Fact]
    public async Task Deposit_KurusunAltiniYuvarlar()
    {
        var (userId, _) = await _db.AddUserWithAccountAsync("ayse@test.com");

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        await service.DepositAsync(userId, 10.126m, null);

        var account = await service.GetAccountAsync(userId);
        Assert.Equal(10.13m, account.Balance);
    }

    [Fact]
    public async Task Withdraw_AzaltirBakiyeyi()
    {
        var (userId, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 500m);

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        await service.WithdrawAsync(userId, 250.50m, null);

        var account = await service.GetAccountAsync(userId);
        Assert.Equal(249.50m, account.Balance);
    }

    [Fact]
    public async Task Withdraw_YetersizBakiyedeReddederVeBakiyeyiDegistirmez()
    {
        var (userId, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 100m);

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        var exception = await Assert.ThrowsAsync<WalletException>(
            () => service.WithdrawAsync(userId, 100.01m, null));

        Assert.Equal(WalletErrorCode.InsufficientFunds, exception.Code);

        await using var freshContext = _db.CreateContext();
        var account = await new WalletService(freshContext).GetAccountAsync(userId);
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public async Task Withdraw_TumBakiyeyeIzinVerir()
    {
        var (userId, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 100m);

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        await service.WithdrawAsync(userId, 100m, null);

        var account = await service.GetAccountAsync(userId);
        Assert.Equal(0m, account.Balance);
    }

    [Fact]
    public async Task Transfer_ParayiIkiHesapArasindaTasir()
    {
        var (ayse, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 500m);
        await _db.AddUserWithAccountAsync("mehmet@test.com", 50m);

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        await service.TransferAsync(ayse, "mehmet@test.com", 300m, "kira");

        await using var freshContext = _db.CreateContext();
        var accounts = await freshContext.Accounts.Include(a => a.User).ToListAsync();

        Assert.Equal(200m, accounts.Single(a => a.User!.Email == "ayse@test.com").Balance);
        Assert.Equal(350m, accounts.Single(a => a.User!.Email == "mehmet@test.com").Balance);
    }

    [Fact]
    public async Task Transfer_IkiTarafaDaKayitYazar()
    {
        var (ayse, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 500m);
        var (_, mehmetAccount) = await _db.AddUserWithAccountAsync("mehmet@test.com");

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        var outgoing = await service.TransferAsync(ayse, "mehmet@test.com", 300m, "kira");

        await using var freshContext = _db.CreateContext();
        var transactions = await freshContext.Transactions.ToListAsync();

        Assert.Equal(2, transactions.Count);

        var incoming = transactions.Single(t => t.Type == TransactionType.TransferIn);
        Assert.Equal(mehmetAccount, incoming.AccountId);
        Assert.Equal(outgoing.AccountId, incoming.CounterpartyAccountId);
        Assert.Equal(300m, incoming.Amount);
    }

    [Fact]
    public async Task Transfer_YetersizBakiyedeHicbirHesabiDegistirmez()
    {
        var (ayse, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 100m);
        await _db.AddUserWithAccountAsync("mehmet@test.com", 50m);

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        await Assert.ThrowsAsync<WalletException>(
            () => service.TransferAsync(ayse, "mehmet@test.com", 500m, null));

        await using var freshContext = _db.CreateContext();
        var accounts = await freshContext.Accounts.Include(a => a.User).ToListAsync();

        Assert.Equal(100m, accounts.Single(a => a.User!.Email == "ayse@test.com").Balance);
        Assert.Equal(50m, accounts.Single(a => a.User!.Email == "mehmet@test.com").Balance);
        Assert.Empty(freshContext.Transactions);
    }

    [Fact]
    public async Task Transfer_KendineTransferiReddeder()
    {
        var (ayse, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 500m);

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        var exception = await Assert.ThrowsAsync<WalletException>(
            () => service.TransferAsync(ayse, "ayse@test.com", 100m, null));

        Assert.Equal(WalletErrorCode.SelfTransfer, exception.Code);
    }

    [Fact]
    public async Task Transfer_TanimsizAliciyiReddeder()
    {
        var (ayse, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 500m);

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        var exception = await Assert.ThrowsAsync<WalletException>(
            () => service.TransferAsync(ayse, "yok@test.com", 100m, null));

        Assert.Equal(WalletErrorCode.AccountNotFound, exception.Code);
    }

    [Fact]
    public async Task Transfer_AliciEpostasindaBuyukKucukHarfiOnemsemez()
    {
        var (ayse, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 500m);
        await _db.AddUserWithAccountAsync("mehmet@test.com");

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        await service.TransferAsync(ayse, "  MEHMET@Test.COM  ", 100m, null);

        await using var freshContext = _db.CreateContext();
        var mehmet = await freshContext.Accounts
            .SingleAsync(a => a.User!.Email == "mehmet@test.com");

        Assert.Equal(100m, mehmet.Balance);
    }

    [Fact]
    public async Task Gecmis_EnYenidenEskiyeSiralar()
    {
        var (userId, _) = await _db.AddUserWithAccountAsync("ayse@test.com");

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        await service.DepositAsync(userId, 100m, "birinci");
        await service.DepositAsync(userId, 200m, "ikinci");
        await service.WithdrawAsync(userId, 50m, "ucuncu");

        var history = await service.GetHistoryAsync(userId, 50);

        Assert.Equal(new[] { "ucuncu", "ikinci", "birinci" }, history.Select(t => t.Description));
    }

    [Fact]
    public async Task Gecmis_YalnizcaKendiIslemleriniDondurur()
    {
        var (ayse, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 500m);
        var (mehmet, _) = await _db.AddUserWithAccountAsync("mehmet@test.com");

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        await service.TransferAsync(ayse, "mehmet@test.com", 100m, "transfer");

        var mehmetHistory = await service.GetHistoryAsync(mehmet, 50);

        Assert.Equal(TransactionType.TransferIn, Assert.Single(mehmetHistory).Type);
    }

    // Projenin en kritik testi: iki istek aynı bakiyeyi aynı anda harcamaya çalışırsa
    // ikincisi reddedilmeli. Aksi halde "lost update" ile yoktan para yaratılır.
    [Fact]
    public async Task EszamanliCekim_IkinciIstegiReddederVeBakiyeyiBozmaz()
    {
        var (userId, _) = await _db.AddUserWithAccountAsync("ayse@test.com", 500m);

        await using var contextA = _db.CreateContext();
        await using var contextB = _db.CreateContext();
        var serviceA = new WalletService(contextA);
        var serviceB = new WalletService(contextB);

        // İki istek de hesabı aynı anda okur: ikisi de 500 TL görür.
        await serviceA.GetAccountAsync(userId);
        await serviceB.GetAccountAsync(userId);

        await serviceA.WithdrawAsync(userId, 400m, "once bu gecer");

        var exception = await Assert.ThrowsAsync<WalletException>(
            () => serviceB.WithdrawAsync(userId, 400m, "bu reddedilmeli"));

        Assert.Equal(WalletErrorCode.ConcurrencyConflict, exception.Code);

        // Bakiye yalnızca ilk çekimi yansıtmalı; eksiye düşmemeli.
        await using var freshContext = _db.CreateContext();
        var account = await new WalletService(freshContext).GetAccountAsync(userId);

        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public async Task Defter_ToplamiHesapBakiyesineEsittir()
    {
        var (ayse, _) = await _db.AddUserWithAccountAsync("ayse@test.com");
        await _db.AddUserWithAccountAsync("mehmet@test.com");

        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        await service.DepositAsync(ayse, 1000m, null);
        await service.WithdrawAsync(ayse, 250.50m, null);
        await service.TransferAsync(ayse, "mehmet@test.com", 300m, null);
        await service.DepositAsync(ayse, 75.25m, null);

        var account = await service.GetAccountAsync(ayse);
        var history = await service.GetHistoryAsync(ayse, 200);

        var ledgerTotal = history.Sum(t => t.Type switch
        {
            TransactionType.Deposit or TransactionType.TransferIn => t.Amount,
            _ => -t.Amount
        });

        Assert.Equal(account.Balance, ledgerTotal);
        Assert.Equal(524.75m, account.Balance);
    }

    [Fact]
    public async Task Hesap_BulunamazsaAnlamliHataDoner()
    {
        await using var context = _db.CreateContext();
        var service = new WalletService(context);

        var exception = await Assert.ThrowsAsync<WalletException>(
            () => service.GetAccountAsync(Guid.NewGuid()));

        Assert.Equal(WalletErrorCode.AccountNotFound, exception.Code);
    }
}
