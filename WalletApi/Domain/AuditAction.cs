namespace WalletApi.Domain;

public enum AuditAction
{
    UserRegistered,
    LoginSucceeded,
    LoginFailed,
    Deposit,
    Withdrawal,
    Transfer
}
