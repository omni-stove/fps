using System;

namespace Fps.Core
{
    /// <summary>
    /// クライアントが接続の承認データ (バイト列) に載せる、ビルドの版と切断して戻ってきた人を見分ける鍵 (<see cref="RejoinKey"/>)。
    /// 形は ASCII の <c>&lt;版&gt;/&lt;鍵&gt;</c>。サーバーは版で承認を決め (<see cref="ConnectionApproval"/>)、鍵で成績を引き継ぐ。
    /// </summary>
    /// <remarks>
    /// 版 (<see cref="BuildVersion"/>) と鍵のどちらにも使えない <c>/</c> で区切るので、区切りを数えるだけで読み分けられる。
    /// 承認データは相手が自由に送れるので、読むときは長さと文字を絞る。
    /// </remarks>
    public static class ConnectionPayload
    {
        public const char Separator = '/';

        /// <summary>承認データの最長のバイト数。</summary>
        public const int MaxLength = BuildVersion.MaxLength + 1 + RejoinKey.MaxLength;

        /// <summary>version と rejoinKey を承認データにする。鍵は小文字にそろえる。</summary>
        /// <exception cref="ArgumentException">
        /// version が <see cref="BuildVersion.TryNormalize"/> で正規化済みの版でない、または rejoinKey が <see cref="RejoinKey.IsValid"/> でない。
        /// </exception>
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

        /// <summary>
        /// 承認データから版と鍵を読む。区切りが 1 つでない・長すぎる・どちらかが使えない文字を含むときは両方 null にして false を返す。
        /// 版を載せない古いビルドの承認データ (鍵だけ) も読めない側に入る。
        /// </summary>
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

        /// <summary>
        /// payload の版はそのままに、鍵を rejoinKey に差し替えた承認データを作る。
        /// EOS の transport がログインした後に、端末の Id を ProductUserId に替えるのに使う。
        /// </summary>
        /// <returns>payload が読めない、または rejoinKey が使えない鍵なら false (replaced は null)。</returns>
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

        /// <summary>
        /// text が <see cref="BuildVersion.TryNormalize"/> を通し、正規化しても変わらない版なら true。
        /// </summary>
        public static bool IsNormalizedVersion(string text)
        {
            return BuildVersion.TryNormalize(text, out var normalized, out _) && normalized == text;
        }
    }
}
