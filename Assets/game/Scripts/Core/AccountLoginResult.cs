using System;

namespace Shooter.Core
{
    public enum AccountLoginOutcome
    {
        SignedIn,

        // 覚えているサインインが無い (一度も入っていない・サインアウトした・期限が切れた)。
        NoSavedLogin,

        Cancelled,
        Failed,
    }

    public readonly struct AccountLoginResult : IEquatable<AccountLoginResult>
    {
        private AccountLoginResult(AccountLoginOutcome outcome, string error)
        {
            Outcome = outcome;
            Error = error;
        }

        public AccountLoginOutcome Outcome { get; }

        // 失敗の理由。ログの行にだけ使い、画面には Outcome から出す。
        public string Error { get; }

        public bool Succeeded => Outcome == AccountLoginOutcome.SignedIn;

        public static AccountLoginResult SignedIn()
        {
            return new AccountLoginResult(AccountLoginOutcome.SignedIn, null);
        }

        public static AccountLoginResult NoSavedLogin(string reason)
        {
            return new AccountLoginResult(AccountLoginOutcome.NoSavedLogin, Require(reason, nameof(reason)));
        }

        public static AccountLoginResult Cancelled()
        {
            return new AccountLoginResult(AccountLoginOutcome.Cancelled, "cancelled by the player");
        }

        public static AccountLoginResult Failed(string error)
        {
            return new AccountLoginResult(AccountLoginOutcome.Failed, Require(error, nameof(error)));
        }

        public bool Equals(AccountLoginResult other)
        {
            return Outcome == other.Outcome && string.Equals(Error, other.Error, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AccountLoginResult other && Equals(other);
        }

        public static bool operator ==(AccountLoginResult left, AccountLoginResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AccountLoginResult left, AccountLoginResult right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Outcome, Error == null ? 0 : StringComparer.Ordinal.GetHashCode(Error));
        }

        public override string ToString()
        {
            return Error == null ? Outcome.ToString() : Outcome + ": " + Error;
        }

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("must not be null or blank", name);
            }

            return value;
        }
    }
}
