using System.Globalization;
using Shooter.Core;

namespace Shooter.NetV2
{
    public static class ConnectLog
    {
        public const string Tag = "[Net]";

        public const string PartyIdField = "partyId";

        public const string JoinCodeField = "joinCode";

        public const string ProductUserIdField = "productUserId";

        public static string Requesting(ConnectionExtras extras)
        {
            var value = extras ?? ConnectionExtras.None;
            return Tag + " connect request party=" + Token(value.PartyId)
                + " joinCode=" + (string.IsNullOrEmpty(value.JoinCode) ? "none" : "set")
                + " skill=" + (value.SkillRating.HasValue ? value.SkillRating.Value.ToString(CultureInfo.InvariantCulture) : "none")
                + " productUserId=" + Token(value.ProductUserId);
        }

        // 欄に合わない値は何が入っているか分からないので、ログの行を崩さないよう中身は出さず長さだけ出す。
        public static string FieldDropped(string field, int length, int maxLength)
        {
            return Tag + " dropped the connect request field " + field + " (length " + length.ToString(CultureInfo.InvariantCulture)
                + "): it must be 1-" + maxLength.ToString(CultureInfo.InvariantCulture) + " characters of [0-9A-Za-z_-]";
        }

        private static string Token(string text)
        {
            return string.IsNullOrEmpty(text) ? "none" : text;
        }
    }
}
