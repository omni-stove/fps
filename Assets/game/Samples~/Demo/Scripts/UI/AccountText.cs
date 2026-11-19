using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class AccountText
    {
        public const string TitleKey = "account.title";
        public const string NoneKey = "account.none";
        public const string DeviceKey = "account.device";
        public const string EpicKey = "account.epic";
        public const string SigningInKey = "account.signingIn";
        public const string SigningOutKey = "account.signingOut";
        public const string CancelledKey = "account.cancelled";
        public const string FailedKey = "account.failed";
        public const string SignInKey = "account.signIn";
        public const string SignOutKey = "account.signOut";

        public static string Title => GameText.Get(TitleKey);

        public static string SignIn => GameText.Get(SignInKey);

        public static string SignOut => GameText.Get(SignOutKey);

        public static string Status(AccountStatus status)
        {
            switch (status.Stage)
            {
                case AccountStage.SigningIn:
                    return GameText.Get(SigningInKey);
                case AccountStage.SigningOut:
                    return GameText.Get(SigningOutKey);
            }

            switch (status.Kind)
            {
                case AccountKind.Epic:
                    return GameText.Get(EpicKey);
                case AccountKind.Device:
                    return GameText.Get(DeviceKey);
                case AccountKind.None:
                    return GameText.Get(NoneKey);
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status.Kind, "unknown account kind");
            }
        }

        // 何も起きていなければ null を返し、画面は知らせの行を隠す。
        public static string Failure(AccountSignInFailure failure)
        {
            switch (failure)
            {
                case AccountSignInFailure.None:
                    return null;
                case AccountSignInFailure.Cancelled:
                    return GameText.Get(CancelledKey);
                case AccountSignInFailure.Failed:
                    return GameText.Get(FailedKey);
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure), failure, "unknown account sign-in failure");
            }
        }
    }
}
