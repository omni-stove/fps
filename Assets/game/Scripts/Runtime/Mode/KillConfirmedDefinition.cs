using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "KillConfirmed", menuName = "Shooter/Modes/Kill Confirmed")]
    public sealed class KillConfirmedDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("どちらかのチームがこの確定数に届いたら試合が終わる")]
        private int _scoreLimit = KillConfirmedRunner.DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = KillConfirmedRunner.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = KillConfirmedRunner.DefaultRespawnSeconds;

        [SerializeField, Min(0.01f), Tooltip("落ちたタグが誰にも拾われずに消えるまでの秒数")]
        private float _tagLifetimeSeconds = KillConfirmedRunner.DefaultTagLifetimeSeconds;

        [SerializeField, Min(1), Tooltip("敵のタグを 1 枚拾って確定させたときに入る点")]
        private int _pointsPerConfirm = KillConfirmedRunner.DefaultPointsPerConfirm;

        [SerializeField, Min(Pickup.MinRadius), Tooltip("落ちたタグを拾える範囲の半径 (m)")]
        private float _tagPickupRadius = KillConfirmedRunner.DefaultTagRadius;

        public int ScoreLimit
        {
            get => _scoreLimit;
            set => _scoreLimit = value;
        }

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public float TagLifetimeSeconds
        {
            get => _tagLifetimeSeconds;
            set => _tagLifetimeSeconds = value;
        }

        public int PointsPerConfirm
        {
            get => _pointsPerConfirm;
            set => _pointsPerConfirm = value;
        }

        public float TagPickupRadius
        {
            get => _tagPickupRadius;
            set => _tagPickupRadius = value;
        }

        public KillConfirmedSpec ToSpec()
        {
            try
            {
                return new KillConfirmedSpec(
                    _scoreLimit, _timeLimitSeconds, _respawnSeconds, _tagLifetimeSeconds, _pointsPerConfirm, _tagPickupRadius);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"kill confirmed definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(KillConfirmedSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _scoreLimit = spec.ScoreLimit;
            _timeLimitSeconds = spec.TimeLimitSeconds;
            _respawnSeconds = spec.RespawnSeconds;
            _tagLifetimeSeconds = spec.TagLifetimeSeconds;
            _pointsPerConfirm = spec.PointsPerConfirm;
            _tagPickupRadius = spec.TagPickupRadius;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed kill confirmed definition)" : name;
    }
}
