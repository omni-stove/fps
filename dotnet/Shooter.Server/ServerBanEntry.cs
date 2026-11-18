using System;
using System.Globalization;
using System.Net;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerBanEntry
    {
        public const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        private static readonly char[] _addressMarks = { '.', ':' };

        public ServerBanEntry(ServerBanKind kind, string value, string reason, DateTime? bannedAt)
        {
            if (!TryNormalize(kind, value, out var normalized))
            {
                throw new ArgumentException("'" + value + "' is not a " + KindName(kind), nameof(value));
            }

            Kind = kind;
            Value = normalized;
            Reason = CleanReason(reason);
            BannedAt = bannedAt?.ToUniversalTime();
        }

        public ServerBanKind Kind { get; }

        public string Value { get; }

        public string Reason { get; }

        public DateTime? BannedAt { get; }

        public static string KindName(ServerBanKind kind)
        {
            switch (kind)
            {
                case ServerBanKind.ProductUserId:
                    return "product user id";
                case ServerBanKind.Address:
                    return "address";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown ban kind");
            }
        }

        // ProductUserId は 32 桁の 16 進なので、数字だけの Id も IP アドレス (. か : を含む) と取り違えない
        public static bool TryParseTarget(string text, out ServerBanKind kind, out string value)
        {
            if (TryNormalize(ServerBanKind.ProductUserId, text, out value))
            {
                kind = ServerBanKind.ProductUserId;
                return true;
            }

            if (TryNormalize(ServerBanKind.Address, text, out value))
            {
                kind = ServerBanKind.Address;
                return true;
            }

            kind = default;
            return false;
        }

        // IPAddress.TryParse は "1" も 0.0.0.1 と読むので、. か : の無いものはアドレスとして扱わない
        public static bool TryNormalize(ServerBanKind kind, string text, out string value)
        {
            value = null;
            var trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            switch (kind)
            {
                case ServerBanKind.ProductUserId:
                    if (!NetLaunchOptions.IsProductUserId(trimmed))
                    {
                        return false;
                    }

                    value = trimmed;
                    return true;
                case ServerBanKind.Address:
                    if (trimmed.IndexOfAny(_addressMarks) < 0 || !IPAddress.TryParse(trimmed, out var address))
                    {
                        return false;
                    }

                    value = UdpMatchTransport.AddressText(address);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown ban kind");
            }
        }

        // 手で書いた一覧の理由に改行があると、ログの 1 行が割れて監視が読み違えるので空白に替える
        private static string CleanReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return null;
            }

            var chars = reason.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsControl(chars[i]))
                {
                    chars[i] = ' ';
                }
            }

            return new string(chars);
        }

        public bool Targets(ServerBanKind kind, string value)
        {
            return Kind == kind && string.Equals(Value, value, StringComparison.OrdinalIgnoreCase);
        }

        // ProductUserId は 16 進なので大文字と小文字を区別しない
        public bool Matches(string productUserId, string address)
        {
            switch (Kind)
            {
                case ServerBanKind.ProductUserId:
                    return productUserId != null && string.Equals(Value, productUserId, StringComparison.OrdinalIgnoreCase);
                case ServerBanKind.Address:
                    return address != null && TryNormalize(ServerBanKind.Address, address, out var normalized)
                        && string.Equals(Value, normalized, StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        public string Describe()
        {
            var text = (Kind == ServerBanKind.ProductUserId ? "product_user_id=" : "address=") + Value;
            if (BannedAt.HasValue)
            {
                text += " banned_at=" + BannedAt.Value.ToString(TimeFormat, CultureInfo.InvariantCulture);
            }

            if (Reason != null)
            {
                text += " reason=\"" + Reason + "\"";
            }

            return text;
        }
    }
}
