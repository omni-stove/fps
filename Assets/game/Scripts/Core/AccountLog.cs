using System;
using System.Globalization;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、テストがログの行を同じ関数で作って突き合わせるため。アカウントの Id やトークンは行に入れない。
    public static class AccountLog
    {
        public const string LogTag = "[Account]";

        public static string Off(string reason)
        {
            return LogTag + " no account sign-in: " + Require(reason, nameof(reason));
        }

        public static string SettingsProblem(string problem)
        {
            return LogTag + " Epic account sign-in is off: " + Require(problem, nameof(problem));
        }

        public static string SignedIn(AccountKind kind)
        {
            return LogTag + " signed in with " + Describe(kind);
        }

        public static string Restoring()
        {
            return LogTag + " signing in with the remembered Epic account";
        }

        public static string NoSavedLogin(string reason)
        {
            return LogTag + " no remembered Epic account (" + Require(reason, nameof(reason)) + "); signing in with the device id";
        }

        public static string RestoreFailed(string error)
        {
            return LogTag + " the remembered Epic account could not sign in: " + Require(error, nameof(error)) + "; signing in with the device id";
        }

        public static string EpicSignInStarted()
        {
            return LogTag + " signing in with an Epic account";
        }

        public static string EpicSignInCancelled()
        {
            return LogTag + " the Epic account sign-in was cancelled; keeping the current sign-in";
        }

        public static string EpicSignInFailed(string error)
        {
            return LogTag + " the Epic account sign-in failed: " + Require(error, nameof(error)) + "; keeping the current sign-in";
        }

        public static string SignedOut()
        {
            return LogTag + " signed out of the Epic account; signing in with the device id";
        }

        public static string SignOutFailed(string error)
        {
            return LogTag + " signing out of the Epic account did not finish cleanly: " + Require(error, nameof(error))
                + "; signing in with the device id";
        }

        public static string DeviceFailed(string error, double retrySeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} the device id sign-in failed: {1}; retrying in {2:0.#}s",
                LogTag,
                Require(error, nameof(error)),
                retrySeconds);
        }

        public static string Lost(AccountKind kind)
        {
            return LogTag + " " + Describe(kind) + " sign-in ended; signing in again";
        }

        public static string Refreshed(AccountKind kind)
        {
            return LogTag + " refreshed " + Describe(kind) + " sign-in";
        }

        public static string RefreshFailed(AccountKind kind, string error)
        {
            return LogTag + " refreshing " + Describe(kind) + " sign-in failed: " + Require(error, nameof(error))
                + "; signing in again when it expires";
        }

        public static string NotWatched()
        {
            return LogTag + " EOS connect notifications could not be registered; an expired sign-in will not be refreshed";
        }

        private static string Describe(AccountKind kind)
        {
            switch (kind)
            {
                case AccountKind.Device:
                    return "the device id";
                case AccountKind.Epic:
                    return "the Epic account";
                default:
                    return "no account";
            }
        }

        private static string Require(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            return value;
        }
    }
}
