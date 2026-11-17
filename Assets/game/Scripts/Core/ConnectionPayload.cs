using System;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    /// <summary>
    /// クライアントが接続の承認データ (バイト列) に載せる、ビルドの版と切断して戻ってきた人を見分ける鍵 (<see cref="RejoinKey"/>)、
    /// それにどれも無くてよい欄 (<see cref="ConnectionExtras"/>)。
    /// 形は ASCII の <c>&lt;版&gt;/&lt;鍵&gt;</c>、欄があれば <c>&lt;版&gt;/&lt;鍵&gt;/&lt;パーティ&gt;/&lt;参加コード&gt;/&lt;実力&gt;/&lt;ProductUserId&gt;</c>
    /// (無い欄は空)。サーバーは版で承認を決め (<see cref="ConnectionApproval"/>)、鍵で成績を引き継ぐ。
    /// </summary>
    /// <remarks>
    /// 版 (<see cref="BuildVersion"/>)・鍵・欄のどれにも使えない <c>/</c> で区切るので、区切りを数えるだけで読み分けられる。
    /// 欄がどれも無ければ欄を足す前と同じバイト列にするのは、足す前のビルドのサーバーとも版を比べて断り合えるようにするため。
    /// 承認データは相手が自由に送れるので、読むときは長さと文字を絞る。
    /// </remarks>
    public static class ConnectionPayload
    {
        public const char Separator = '/';

        /// <summary>実力の値を 10 進で書いたときの最長の文字数 (<c>-2147483648</c>)。</summary>
        public const int MaxSkillRatingLength = 11;

        /// <summary>承認データの最長のバイト数。</summary>
        public const int MaxLength = BuildVersion.MaxLength + 1 + RejoinKey.MaxLength
            + 1 + ConnectionExtras.MaxPartyIdLength
            + 1 + ConnectionExtras.MaxJoinCodeLength
            + 1 + MaxSkillRatingLength
            + 1 + ConnectionExtras.MaxProductUserIdLength;

        /// <summary>版と鍵だけの承認データの区切りの数。</summary>
        private const int _basicSeparators = 1;

        /// <summary>欄を載せた承認データの区切りの数。</summary>
        private const int _extendedSeparators = 5;

        /// <summary>version と rejoinKey を承認データにする。鍵は小文字にそろえる。</summary>
        /// <exception cref="ArgumentException">
        /// version が <see cref="BuildVersion.TryNormalize"/> で正規化済みの版でない、または rejoinKey が <see cref="RejoinKey.IsValid"/> でない。
        /// </exception>
        public static byte[] Encode(string version, string rejoinKey)
        {
            return Encode(version, rejoinKey, ConnectionExtras.None);
        }

        /// <summary>version と rejoinKey と extras を承認データにする。鍵は小文字にそろえ、欄は大文字・小文字をそのまま載せる。</summary>
        /// <exception cref="ArgumentNullException">extras が null。</exception>
        /// <exception cref="ArgumentException">
        /// version が <see cref="BuildVersion.TryNormalize"/> で正規化済みの版でない、または rejoinKey が <see cref="RejoinKey.IsValid"/> でない。
        /// </exception>
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

        /// <summary>
        /// 承認データから版と鍵を読む。欄は読み捨てる。読めないときの扱いは <see cref="TryDecode(byte[], out string, out string, out ConnectionExtras)"/> と同じ。
        /// </summary>
        public static bool TryDecode(byte[] payload, out string version, out string rejoinKey)
        {
            return TryDecode(payload, out version, out rejoinKey, out _);
        }

        /// <summary>
        /// 承認データから版と鍵と欄を読む。区切りが 1 つでも 5 つでもない・長すぎる・どれかが使えない文字を含むときは
        /// どれも null にして false を返す。欄の無い承認データは extras を <see cref="ConnectionExtras.None"/> にする。
        /// 版を載せない古いビルドの承認データ (鍵だけ) も読めない側に入る。
        /// </summary>
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

        /// <summary>
        /// payload の版と欄はそのままに、鍵を rejoinKey に差し替えた承認データを作る。
        /// EOS の transport がログインした後に、端末の Id を ProductUserId に替えるのに使う。
        /// </summary>
        /// <returns>payload が読めない、または rejoinKey が使えない鍵なら false (replaced は null)。</returns>
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

        /// <summary>
        /// text が <see cref="BuildVersion.TryNormalize"/> を通し、正規化しても変わらない版なら true。
        /// </summary>
        public static bool IsNormalizedVersion(string text)
        {
            return BuildVersion.TryNormalize(text, out var normalized, out _) && normalized == text;
        }

        /// <summary>
        /// text が <see cref="ConnectionExtras.SkillRating"/> を invariant の 10 進で書いたもの (先頭の 0 と <c>-0</c> は無し) なら読む。
        /// </summary>
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

        /// <summary>区切り separatorAt の次から end の手前までを欄として読む。空なら null。</summary>
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

        /// <summary>
        /// バイトを 1 つずつ文字にする。0x80 以上のバイトは ASCII でない文字になり、後の文字の検査で落ちる。
        /// </summary>
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
