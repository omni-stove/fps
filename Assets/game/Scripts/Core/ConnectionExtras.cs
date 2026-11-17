using System;

namespace Shooter.Core
{
    // 承認データは相手が自由に送れるので、文字列の欄は鍵 (RejoinKey) と同じく英数字と - _ に絞る。
    public sealed class ConnectionExtras
    {
        // EOS のロビーの Id が収まる長さにする。
        public const int MaxPartyIdLength = 64;

        public const int MaxJoinCodeLength = 16;

        // EOS の EOS_PRODUCTUSERID_MAX_LENGTH に合わせる。
        public const int MaxProductUserIdLength = 32;

        public static readonly ConnectionExtras None = new ConnectionExtras(null, null, null, null);

        public ConnectionExtras(string partyId, string joinCode, int? skillRating, string productUserId)
        {
            PartyId = Normalize(partyId, MaxPartyIdLength, nameof(partyId));
            JoinCode = Normalize(joinCode, MaxJoinCodeLength, nameof(joinCode));
            SkillRating = skillRating;
            ProductUserId = Normalize(productUserId, MaxProductUserIdLength, nameof(productUserId));
        }

        public string PartyId { get; }

        public string JoinCode { get; }

        public int? SkillRating { get; }

        public string ProductUserId { get; }

        public bool IsEmpty => PartyId == null && JoinCode == null && !SkillRating.HasValue && ProductUserId == null;

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
