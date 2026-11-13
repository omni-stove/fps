using System;

namespace Fps.Core
{
    /// <summary>
    /// メニューからキャンペーンのシーンへ渡す選択。どのミッション (<see cref="MissionId"/>) をどの難易度 (<see cref="Difficulty"/>) で始めるか。
    /// シーンをまたいで持つ置き場所は Runtime の <c>CampaignLaunchSelection</c>。
    /// </summary>
    public sealed class CampaignLaunch
    {
        /// <summary>ミッション Id の最大の長さ。セーブのスロット名 (ファイル名) に入るので短く抑える。</summary>
        public const int MaxMissionIdLength = 64;

        /// <param name="missionId">ミッション Id (<see cref="IsValidMissionId"/> を満たすこと)。</param>
        /// <param name="difficulty">難易度。</param>
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

        /// <summary>選んだミッションの Id。MissionDefinition のアセット名と同じ。</summary>
        public string MissionId { get; }

        public CampaignDifficulty Difficulty { get; }

        /// <summary>
        /// id がミッション Id として使えるか。1〜<see cref="MaxMissionIdLength"/> 文字の英数字・<c>_</c>・<c>-</c> だけを許す
        /// (セーブのスロット名にそのまま入れるので、どの OS でもファイル名として安全な文字に限る)。
        /// </summary>
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

        /// <summary>id が <see cref="IsValidMissionId"/> を満たさなければ <see cref="ArgumentException"/> を投げる。</summary>
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
