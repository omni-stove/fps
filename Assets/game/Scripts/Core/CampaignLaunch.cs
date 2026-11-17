using System;

namespace Shooter.Core
{
    public sealed class CampaignLaunch
    {
        // セーブのスロット名 (ファイル名) に入るので短く抑える。
        public const int MaxMissionIdLength = 64;

        public CampaignLaunch(string missionId, CampaignDifficulty difficulty)
        {
            RequireValidMissionId(missionId, nameof(missionId));
            if (!Enum.IsDefined(typeof(CampaignDifficulty), difficulty))
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "unknown difficulty");
            }

            MissionId = missionId;
            Difficulty = difficulty;
        }

        public string MissionId { get; }

        public CampaignDifficulty Difficulty { get; }

        // セーブのスロット名にそのまま入れるので、どの OS でもファイル名として安全な文字に限る。
        public static bool IsValidMissionId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > MaxMissionIdLength)
            {
                return false;
            }

            foreach (var c in id)
            {
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        public static void RequireValidMissionId(string id, string parameterName)
        {
            if (id == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!IsValidMissionId(id))
            {
                throw new ArgumentException(
                    $"mission id '{id}' must be 1-{MaxMissionIdLength} characters of letters, digits, '_' or '-'",
                    parameterName);
            }
        }

        public override string ToString()
        {
            return MissionId + "/" + CampaignLaunchOptions.DifficultyName(Difficulty);
        }
    }
}
