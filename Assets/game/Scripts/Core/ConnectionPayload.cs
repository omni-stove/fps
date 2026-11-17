using System;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    public static class ConnectionPayload
    {
        // 版・鍵・欄のどれにも使えない文字で区切るので、区切りを数えるだけで読み分けられる。
        public const char Separator = '/';

        public const int MaxSkillRatingLength = 11;

        public const int MaxLength = BuildVersion.MaxLength + 1 + RejoinKey.MaxLength
            + 1 + ConnectionExtras.MaxPartyIdLength
            + 1 + ConnectionExtras.MaxJoinCodeLength
            + 1 + MaxSkillRatingLength
            + 1 + ConnectionExtras.MaxProductUserIdLength;

        private const int _basicSeparators = 1;

        private const int _extendedSeparators = 5;

        public static byte[] Encode(string version, string rejoinKey)
        {
            return Encode(version, rejoinKey, ConnectionExtras.None);
        }

        // 欄がどれも無ければ欄を足す前と同じバイト列にするのは、足す前のビルドのサーバーとも版を比べて断り合えるようにするため。
        public static byte[] Encode(string version, string rejoinKey, ConnectionExtras extras)
        {
            if (extras == null)
            {
                throw new ArgumentNullException(nameof(extras));
            }

            if (!IsNormalizedVersion(version))
            {
                throw new ArgumentException(
                    "version '" + version + "' must be a normalized build version (letters, digits, '.', '-', '_'; up to "
                    + BuildVersion.MaxLength + " characters)", nameof(version));
            }

            var key = RejoinKey.Encode(rejoinKey);
            var text = new StringBuilder(MaxLength);
            text.Append(version).Append(Separator);
            foreach (var b in key)
            {
                text.Append((char)b);
            }

            if (!extras.IsEmpty)
            {
                text.Append(Separator).Append(extras.PartyId)
                    .Append(Separator).Append(extras.JoinCode)
                    .Append(Separator).Append(extras.SkillRating.HasValue ? extras.SkillRating.Value.ToString(CultureInfo.InvariantCulture) : null)
                    .Append(Separator).Append(extras.ProductUserId);
            }

            var bytes = new byte[text.Length];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)text[i];
            }

            return bytes;
        }

        public static bool TryDecode(byte[] payload, out string version, out string rejoinKey)
        {
            return TryDecode(payload, out version, out rejoinKey, out _);
        }

        // 承認データは相手が自由に送れるので、読むときは長さと文字を絞る。
        public static bool TryDecode(byte[] payload, out string version, out string rejoinKey, out ConnectionExtras extras)
        {
            version = null;
            rejoinKey = null;
            extras = null;
            if (payload == null || payload.Length == 0 || payload.Length > MaxLength)
            {
                return false;
            }

            var separators = new int[_extendedSeparators];
            var separatorCount = 0;
            for (var i = 0; i < payload.Length; i++)
            {
                if (payload[i] != (byte)Separator)
                {
                    continue;
                }

                if (separatorCount == separators.Length)
                {
                    return false;
                }

                separators[separatorCount++] = i;
            }

            if (separatorCount != _basicSeparators && separatorCount != _extendedSeparators)
            {
                return false;
            }

            var versionEnd = separators[0];
            if (versionEnd == 0)
            {
                return false;
            }

            var decodedVersion = Ascii(payload, 0, versionEnd);
            if (!IsNormalizedVersion(decodedVersion))
            {
                return false;
            }

            var keyStart = versionEnd + 1;
            var keyEnd = separatorCount == _basicSeparators ? payload.Length : separators[1];
            var keyBytes = new byte[keyEnd - keyStart];
            Array.Copy(payload, keyStart, keyBytes, 0, keyBytes.Length);
            if (!RejoinKey.TryDecode(keyBytes, out var decodedKey))
            {
                return false;
            }

            var decodedExtras = ConnectionExtras.None;
            if (separatorCount == _extendedSeparators
                && !TryDecodeExtras(payload, separators, out decodedExtras))
            {
                return false;
            }

            version = decodedVersion;
            rejoinKey = decodedKey;
            extras = decodedExtras;
            return true;
        }

        public static bool TryReplaceRejoinKey(byte[] payload, string rejoinKey, out byte[] replaced)
        {
            replaced = null;
            if (!RejoinKey.IsValid(rejoinKey) || !TryDecode(payload, out var version, out _, out var extras))
            {
                return false;
            }

            replaced = Encode(version, rejoinKey, extras);
            return true;
        }

        public static bool IsNormalizedVersion(string text)
        {
            return BuildVersion.TryNormalize(text, out var normalized, out _) && normalized == text;
        }

        public static bool TryParseSkillRating(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text) || text.Length > MaxSkillRatingLength)
            {
                return false;
            }

            var negative = text[0] == '-';
            var start = negative ? 1 : 0;
            if (start == text.Length || (text[start] == '0' && (negative || text.Length - start > 1)))
            {
                return false;
            }

            long total = 0;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (c < '0' || c > '9')
                {
                    return false;
                }

                total = (total * 10) + (c - '0');
            }

            if (negative)
            {
                total = -total;
            }

            if (total < int.MinValue || total > int.MaxValue)
            {
                return false;
            }

            value = (int)total;
            return true;
        }

        private static bool TryDecodeExtras(byte[] payload, int[] separators, out ConnectionExtras extras)
        {
            extras = null;
            if (!TryField(payload, separators[1], separators[2], ConnectionExtras.MaxPartyIdLength, out var partyId)
                || !TryField(payload, separators[2], separators[3], ConnectionExtras.MaxJoinCodeLength, out var joinCode)
                || !TryField(payload, separators[4], payload.Length, ConnectionExtras.MaxProductUserIdLength, out var productUserId))
            {
                return false;
            }

            int? skillRating = null;
            var skillText = Ascii(payload, separators[3] + 1, separators[4] - separators[3] - 1);
            if (skillText.Length > 0)
            {
                if (!TryParseSkillRating(skillText, out var skill))
                {
                    return false;
                }

                skillRating = skill;
            }

            extras = new ConnectionExtras(partyId, joinCode, skillRating, productUserId);
            return true;
        }

        private static bool TryField(byte[] payload, int separatorAt, int end, int maxLength, out string value)
        {
            value = Ascii(payload, separatorAt + 1, end - separatorAt - 1);
            if (value.Length == 0)
            {
                value = null;
                return true;
            }

            if (!ConnectionExtras.IsValidField(value, maxLength))
            {
                value = null;
                return false;
            }

            return true;
        }

        // 0x80 以上のバイトはここで弾かず、ASCII でない文字にして後の文字の検査で落とす。
        private static string Ascii(byte[] payload, int start, int length)
        {
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = (char)payload[start + i];
            }

            return new string(chars);
        }
    }
}
