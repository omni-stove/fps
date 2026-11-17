using System;

namespace Shooter.Core
{
    // 承認データは相手が自由に送れるので、長さと文字を絞ってから文字列にする。大文字と小文字は区別しない
    // (EOS の Id は小文字で返るが、貼り付けなどで大文字になっても同じ人として扱う)。
    public static class RejoinKey
    {
        // ProductUserId (32 桁) と端末の Id (32 桁) が収まる長さ。
        public const int MaxLength = 64;

        public static bool IsValid(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > MaxLength)
            {
                return false;
            }

            foreach (var c in key)
            {
                if (!IsAllowed(c))
                {
                    return false;
                }
            }

            return true;
        }

        public static byte[] Encode(string key)
        {
            if (!IsValid(key))
            {
                throw new ArgumentException("rejoin key must be 1-" + MaxLength + " characters of [0-9A-Za-z_-]", nameof(key));
            }

            var bytes = new byte[key.Length];
            for (var i = 0; i < key.Length; i++)
            {
                bytes[i] = (byte)ToLowerAscii(key[i]);
            }

            return bytes;
        }

        public static bool TryDecode(byte[] payload, out string key)
        {
            key = null;
            if (payload == null || payload.Length == 0 || payload.Length > MaxLength)
            {
                return false;
            }

            var chars = new char[payload.Length];
            for (var i = 0; i < payload.Length; i++)
            {
                var c = (char)payload[i];
                if (!IsAllowed(c))
                {
                    return false;
                }

                chars[i] = ToLowerAscii(c);
            }

            key = new string(chars);
            return true;
        }

        public static string NewDeviceKey()
        {
            return Guid.NewGuid().ToString("N");
        }

        // 使える文字を ASCII に絞ってあるので、文化に左右されない手書きの変換で足りる
        private static char ToLowerAscii(char c)
        {
            return c >= 'A' && c <= 'Z' ? (char)(c + ('a' - 'A')) : c;
        }

        private static bool IsAllowed(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '-' || c == '_';
        }
    }
}
