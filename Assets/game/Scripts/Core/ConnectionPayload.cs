using System;

namespace Shooter.Core
{
    public static class ConnectionPayload
    {
        // 版と鍵のどちらにも使えない文字で区切るので、区切りを数えるだけで読み分けられる。
        public const char Separator = '/';

        public const int MaxLength = BuildVersion.MaxLength + 1 + RejoinKey.MaxLength;

        public static byte[] Encode(string version, string rejoinKey)
        {
            if (!IsNormalizedVersion(version))
            {
                throw new ArgumentException(
                    "version '" + version + "' must be a normalized build version (letters, digits, '.', '-', '_'; up to "
                    + BuildVersion.MaxLength + " characters)", nameof(version));
            }

            var key = RejoinKey.Encode(rejoinKey);
            var bytes = new byte[version.Length + 1 + key.Length];
            for (var i = 0; i < version.Length; i++)
            {
                bytes[i] = (byte)version[i];
            }

            bytes[version.Length] = (byte)Separator;
            Array.Copy(key, 0, bytes, version.Length + 1, key.Length);
            return bytes;
        }

        // 承認データは相手が自由に送れるので、読むときは長さと文字を絞る。
        public static bool TryDecode(byte[] payload, out string version, out string rejoinKey)
        {
            version = null;
            rejoinKey = null;
            if (payload == null || payload.Length == 0 || payload.Length > MaxLength)
            {
                return false;
            }

            var separatorAt = -1;
            for (var i = 0; i < payload.Length; i++)
            {
                if (payload[i] != (byte)Separator)
                {
                    continue;
                }

                if (separatorAt >= 0)
                {
                    return false;
                }

                separatorAt = i;
            }

            if (separatorAt <= 0)
            {
                return false;
            }

            var chars = new char[separatorAt];
            for (var i = 0; i < separatorAt; i++)
            {
                chars[i] = (char)payload[i];
            }

            var decodedVersion = new string(chars);
            if (!IsNormalizedVersion(decodedVersion))
            {
                return false;
            }

            var keyBytes = new byte[payload.Length - separatorAt - 1];
            Array.Copy(payload, separatorAt + 1, keyBytes, 0, keyBytes.Length);
            if (!RejoinKey.TryDecode(keyBytes, out var decodedKey))
            {
                return false;
            }

            version = decodedVersion;
            rejoinKey = decodedKey;
            return true;
        }

        public static bool TryReplaceRejoinKey(byte[] payload, string rejoinKey, out byte[] replaced)
        {
            replaced = null;
            if (!RejoinKey.IsValid(rejoinKey) || !TryDecode(payload, out var version, out _))
            {
                return false;
            }

            replaced = Encode(version, rejoinKey);
            return true;
        }

        public static bool IsNormalizedVersion(string text)
        {
            return BuildVersion.TryNormalize(text, out var normalized, out _) && normalized == text;
        }
    }
}
