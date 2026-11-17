using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// フリーフォーオール (<see cref="FreeForAllMode"/>) の制限時間・勝利に必要な点・リスポーン・キル 1 回の点をアセットとして持つ。
    /// 新しく作ったときの値は <see cref="FreeForAllMode"/> の既定値と同じ。
    /// <see cref="FreeForAllRunner.Definition"/> に配線するか入口 (<see cref="GameContent.FreeForAll"/>) に置くと、
    /// <see cref="FreeForAllRunner"/> と <see cref="MatchRunner"/> は Inspector の値の代わりにこれを読む。
    /// </summary>
    [CreateAssetMenu(fileName = "FreeForAll", menuName = "Shooter/Modes/Free For All")]
    public sealed class FreeForAllDefinition : ScriptableObject
    {
        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = FreeForAllMode.DefaultTimeLimitSeconds;

        [SerializeField, Min(1), Tooltip("誰かがこの点に届いたら試合が終わる")]
        private int _scoreLimit = FreeForAllMode.DefaultScoreLimit;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = FreeForAllMode.DefaultRespawnSeconds;

        [SerializeField, Min(1), Tooltip("別の人を 1 回倒すごとに入る点")]
        private int _pointsPerKill = FreeForAllMode.DefaultPointsPerKill;

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

        public int PointsPerKill
        {
            get => _pointsPerKill;
            set => _pointsPerKill = value;
        }

        /// <summary>この定義の値でモード (<see cref="FreeForAllMode.Create"/>) を組む。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="InvalidOperationException">値が不正なとき。メッセージにアセット名を含む。</exception>
        public ModeSession CreateSession()
        {
            try
            {
                return FreeForAllMode.Create(_timeLimitSeconds, _scoreLimit, _respawnSeconds, _pointsPerKill);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"free for all definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed free for all definition)" : name;
    }
}
