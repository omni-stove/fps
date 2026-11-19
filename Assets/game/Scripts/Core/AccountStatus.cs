using System;

namespace Shooter.Core
{
    public enum AccountKind
    {
        None,

        // 端末ごとに別のユーザーになる Device ID のログイン。
        Device,

        // 端末をまたいで同じユーザーになる Epic アカウントのログイン。
        Epic,
    }

    public enum AccountStage
    {
        Idle,
        SigningIn,
        SigningOut,
    }

    public enum AccountSignInFailure
    {
        None,
        Cancelled,
        Failed,
    }

    public readonly struct AccountStatus : IEquatable<AccountStatus>
    {
        public AccountStatus(AccountKind kind, AccountStage stage, AccountSignInFailure lastFailure, bool canUseEpicAccount)
        {
            if (!Enum.IsDefined(typeof(AccountKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a defined AccountKind");
            }

            if (!Enum.IsDefined(typeof(AccountStage), stage))
            {
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "stage is not a defined AccountStage");
            }

            if (!Enum.IsDefined(typeof(AccountSignInFailure), lastFailure))
            {
                throw new ArgumentOutOfRangeException(nameof(lastFailure), lastFailure, "lastFailure is not a defined AccountSignInFailure");
            }

            Kind = kind;
            Stage = stage;
            LastFailure = lastFailure;
            CanUseEpicAccount = canUseEpicAccount;
        }

        public AccountKind Kind { get; }

        public AccountStage Stage { get; }

        // 自分から始めたサインインが入れなかった理由。次に始めるか、入れたときに None へ戻る。
        public AccountSignInFailure LastFailure { get; }

        public bool CanUseEpicAccount { get; }

        public bool IsBusy => Stage != AccountStage.Idle;

        public bool CanSignInWithEpic => CanUseEpicAccount && Stage == AccountStage.Idle && Kind != AccountKind.Epic;

        public bool CanSignOut => Stage == AccountStage.Idle && Kind == AccountKind.Epic;

        public bool Equals(AccountStatus other)
        {
            return Kind == other.Kind && Stage == other.Stage && LastFailure == other.LastFailure && CanUseEpicAccount == other.CanUseEpicAccount;
        }

        public override bool Equals(object obj)
        {
            return obj is AccountStatus other && Equals(other);
        }

        public static bool operator ==(AccountStatus left, AccountStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AccountStatus left, AccountStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, Stage, LastFailure, CanUseEpicAccount);
        }

        public override string ToString()
        {
            return "account=" + Kind + " stage=" + Stage + " failure=" + LastFailure + (CanUseEpicAccount ? " epic" : string.Empty);
        }
    }
}
