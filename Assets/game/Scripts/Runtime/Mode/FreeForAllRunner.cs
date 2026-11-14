using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// フリーフォーオール (<see cref="FreeForAllMode"/>) をシーンで進める。参加者 (<see cref="SideMember"/>) は 1 人ずつ別の陣営にすること
    /// (同じ陣営が 2 人いると始められない)。HUD の得点の下に、自分の順位と 1 位との差 (<see cref="FreeForAllMode.RankText(ModeSession, Side)"/>) を出す。
    /// </summary>
    public sealed class FreeForAllRunner : ModeRunnerBase
    {
        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = FreeForAllMode.DefaultTimeLimitSeconds;

        [SerializeField, Min(1), Tooltip("勝利に必要なキル数")]
        private int _scoreLimit = FreeForAllMode.DefaultScoreLimit;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = FreeForAllMode.DefaultRespawnSeconds;

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public int ScoreLimit
        {
            get => _scoreLimit;
            set => _scoreLimit = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        protected override ModeSession CreateSession()
        {
            return FreeForAllMode.Create(_timeLimitSeconds, _scoreLimit, _respawnSeconds);
        }

        protected override void AppendHudTexts(List<LocalizedText> lines)
        {
            var player = LocalPlayer;
            if (player != null && Session != null)
            {
                lines.Add(FreeForAllMode.RankText(Session, player.Side));
            }
        }
    }
}
