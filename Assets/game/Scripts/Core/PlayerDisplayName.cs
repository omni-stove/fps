using System;
using System.Text;

namespace Shooter.Core
{
    // 名前はほかの人の画面にそのまま出るので、サーバーが受け取るときと名簿を読むときの両方で同じ決まりに通す
    public static class PlayerDisplayName
    {
        // Core からは Protocol を参照できないので、ここに置いて ConnectRequestMessage.MaxDisplayNameBytes から使う
        public const int MaxBytes = 64;

        private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false, true);

        // 不正な名前を切り詰めたり直したりせず空にするのは、本人の意図しない名前で他人の画面に出さないため。
        // 空なら呼ぶ側が P<id> などの名前に戻す
        public static string Sanitize(string name, int maxLength)
        {
            RequireMaxLength(maxLength);
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var trimmed = name.Trim();
            return IsValid(trimmed, maxLength) ? trimmed : string.Empty;
        }

        // 空と前後の空白を断るのは、Sanitize を通した名前だけを通すため
        public static bool IsValid(string name, int maxLength)
        {
            RequireMaxLength(maxLength);
            if (string.IsNullOrEmpty(name) || name.Length > maxLength)
            {
                return false;
            }

            if (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[name.Length - 1]))
            {
                return false;
            }

            for (var i = 0; i < name.Length; i++)
            {
                if (IsRefused(name[i]))
                {
                    return false;
                }
            }

            int bytes;
            try
            {
                bytes = _utf8.GetByteCount(name);
            }
            catch (EncoderFallbackException)
            {
                // 対になっていないサロゲートは UTF-8 にできず、送るときに MessageWriter が投げるため
                return false;
            }

            return bytes <= MaxBytes;
        }

        // char.IsControl (Cc) だけで済ませないのは、行や段落の区切りで HUD の行が崩れ、
        // 書字方向の上書きで前後のキルフィードの文字の並びが変わるため。
        // 数値で書くのは、U+2028/2029 を文字リテラルに直接置くと C# が改行と見なしてコンパイルできないため
        private static bool IsRefused(char c)
        {
            if (char.IsControl(c))
            {
                return true;
            }

            return c == (char)0x2028
                || c == (char)0x2029
                || (c >= (char)0x202A && c <= (char)0x202E)
                || (c >= (char)0x2066 && c <= (char)0x2069);
        }

        private static void RequireMaxLength(int maxLength)
        {
            if (maxLength < 1 || maxLength > NetMatchSettings.MaxMaxDisplayNameLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxLength), maxLength, nameof(maxLength) + " must be between 1 and " + NetMatchSettings.MaxMaxDisplayNameLength);
            }
        }
    }
}
