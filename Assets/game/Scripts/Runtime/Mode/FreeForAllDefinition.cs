using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
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
