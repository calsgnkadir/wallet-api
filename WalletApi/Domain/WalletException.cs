namespace WalletApi.Domain;

public enum WalletErrorCode
{
    AccountNotFound,
    InsufficientFunds,
    InvalidAmount,
    SelfTransfer,
    CurrencyMismatch,
    ConcurrencyConflict
}

public class WalletException : Exception
{
    public WalletException(WalletErrorCode code, string message) : base(message)
    {
        Code = code;
    }

    public WalletErrorCode Code { get; }
}
