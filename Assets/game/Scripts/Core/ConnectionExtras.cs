using System;

namespace Shooter.Core
{
    /// <summary>
    /// 接続の承認データに版と鍵の後ろへ載せる、どれも無くてよい欄 (パーティ・参加コード・実力・ボイスに使う ProductUserId)。
    /// 空の文字列は無いものとして null にそろえる。
    /// </summary>
    /// <remarks>
    /// 承認データは相手が自由に送れるので、文字列の欄は鍵 (<see cref="RejoinKey"/>) と同じく英数字と <c>-</c> <c>_</c> に絞る。
    /// </remarks>
    public sealed class ConnectionExtras
    {
        /// <summary>パーティの Id の最長の文字数。EOS のロビーの Id が収まる。</summary>
        public const int MaxPartyIdLength = 64;

        /// <summary>参加コードの最長の文字数。</summary>
        public const int MaxJoinCodeLength = 16;

        /// <summary>ProductUserId の最長の文字数 (EOS_PRODUCTUSERID_MAX_LENGTH)。</summary>
        public const int MaxProductUserIdLength = 32;

        /// <summary>どの欄も無い。</summary>
        public static readonly ConnectionExtras None = new ConnectionExtras(null, null, null, null);

        /// <param name="partyId">入っているパーティ (メンバー用ロビー) の Id。無ければ null か空。</param>
        /// <param name="joinCode">参加コードで入るときのコード。無ければ null か空。</param>
        /// <param name="skillRating">実力の値。無ければ null。</param>
        /// <param name="productUserId">ボイスで使う EOS の ProductUserId。無ければ null か空。</param>
        /// <exception cref="ArgumentException">文字列の欄が長すぎる、または英数字と <c>-</c> <c>_</c> 以外を含む。</exception>
        public ConnectionExtras(string partyId, string joinCode, int? skillRating, string productUserId)
        {
            PartyId = Normalize(partyId, MaxPartyIdLength, nameof(partyId));
            JoinCode = Normalize(joinCode, MaxJoinCodeLength, nameof(joinCode));
            SkillRating = skillRating;
            ProductUserId = Normalize(productUserId, MaxProductUserIdLength, nameof(productUserId));
        }

        /// <summary>パーティの Id。無ければ null。</summary>
        public string PartyId { get; }

        /// <summary>参加コード。無ければ null。</summary>
        public string JoinCode { get; }

        /// <summary>実力の値。無ければ null。</summary>
        public int? SkillRating { get; }

        /// <summary>ボイスで使う ProductUserId。無ければ null。</summary>
        public string ProductUserId { get; }

        /// <summary>どの欄も無いか。</summary>
        public bool IsEmpty => PartyId == null && JoinCode == null && !SkillRating.HasValue && ProductUserId == null;

        /// <summary>text が 1〜maxLength 文字の英数字と <c>-</c> <c>_</c> だけなら true。</summary>
        public static bool IsValidField(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length > maxLength)
            {
                return false;
            }

            foreach (var c in text)
            {
                if (!IsAllowed(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>ASCII の英数字と <c>-</c> <c>_</c> なら true。</summary>
        public static bool IsAllowed(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '-' || c == '_';
        }

        private static string Normalize(string text, int maxLength, string name)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            if (!IsValidField(text, maxLength))
            {
                throw new ArgumentException(name + " must be 1-" + maxLength + " characters of [0-9A-Za-z_-]", name);
            }

            return text;
        }
    }
}
