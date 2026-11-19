using System;

namespace Shooter.Core
{
    // Epic アカウントでのサインインを使うかと、開発中に DevAuthTool で入るための宛先。
    // 使わないゲームでは何も書かず、今までどおり端末ごとの Device ID のユーザーだけで動く。
    public sealed class EosAccountLoginSettings
    {
        public const string EpicAccountLoginVariable = "EOS_EPIC_ACCOUNT_LOGIN";

        // "localhost:6547/名前" の形。DevAuthTool の宛先と、そこに登録した資格情報の名前を / で区切る。
        public const string EpicDevAuthVariable = "EOS_EPIC_DEV_AUTH";

        public const char DevAuthSeparator = '/';

        public static EosAccountLoginSettings Disabled { get; } = new EosAccountLoginSettings(false, null, null, null);

        private EosAccountLoginSettings(bool epicAccount, string devAuthHost, string devAuthCredential, string problem)
        {
            EpicAccount = epicAccount;
            DevAuthHost = devAuthHost;
            DevAuthCredential = devAuthCredential;
            Problem = problem;
        }

        public bool EpicAccount { get; }

        public string DevAuthHost { get; }

        public string DevAuthCredential { get; }

        public bool HasDevAuth => DevAuthHost != null && DevAuthCredential != null;

        // 書かれた値が読めなかったときの理由。読めなければ Epic アカウントを使わない設定に倒す。
        public string Problem { get; }

        public static EosAccountLoginSettings Enabled(string devAuthHost = null, string devAuthCredential = null)
        {
            var host = Normalize(devAuthHost);
            var credential = Normalize(devAuthCredential);
            if ((host == null) != (credential == null))
            {
                throw new ArgumentException("the dev auth host and credential name must be given together");
            }

            return new EosAccountLoginSettings(true, host, credential, null);
        }

        public static EosAccountLoginSettings Invalid(string problem)
        {
            if (string.IsNullOrWhiteSpace(problem))
            {
                throw new ArgumentException("must not be null or blank", nameof(problem));
            }

            return new EosAccountLoginSettings(false, null, null, problem);
        }

        public static EosAccountLoginSettings Parse(string epicAccountLogin, string epicDevAuth)
        {
            var switchValue = Normalize(epicAccountLogin);
            var devAuth = Normalize(epicDevAuth);
            if (switchValue == null)
            {
                return devAuth == null
                    ? Disabled
                    : Invalid(EpicDevAuthVariable + " is set but " + EpicAccountLoginVariable + " is not on");
            }

            if (!TryParseSwitch(switchValue, out var on))
            {
                return Invalid(EpicAccountLoginVariable + " must be true or false");
            }

            if (!on)
            {
                return Disabled;
            }

            if (devAuth == null)
            {
                return Enabled();
            }

            var separator = devAuth.LastIndexOf(DevAuthSeparator);
            if (separator <= 0 || separator == devAuth.Length - 1)
            {
                return Invalid(EpicDevAuthVariable + " must be written as host:port" + DevAuthSeparator + "credential");
            }

            var host = Normalize(devAuth.Substring(0, separator));
            var credential = Normalize(devAuth.Substring(separator + 1));
            if (host == null || credential == null || host.IndexOf(':') <= 0)
            {
                return Invalid(EpicDevAuthVariable + " must be written as host:port" + DevAuthSeparator + "credential");
            }

            return Enabled(host, credential);
        }

        public static bool TryParseSwitch(string value, out bool on)
        {
            switch (Normalize(value)?.ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                    on = true;
                    return true;
                case "0":
                case "false":
                case "no":
                case "off":
                    on = false;
                    return true;
                default:
                    on = false;
                    return false;
            }
        }

        public override string ToString()
        {
            if (Problem != null)
            {
                return "EosAccountLoginSettings(invalid)";
            }

            return EpicAccount ? (HasDevAuth ? "EosAccountLoginSettings(epic, dev auth)" : "EosAccountLoginSettings(epic)") : "EosAccountLoginSettings(device)";
        }

        private static string Normalize(string value)
        {
            if (value == null)
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
