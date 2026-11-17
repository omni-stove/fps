using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 拠点の数はシーンの ControlPointZone で決まるので持たず、ToSpec に渡す。
    [CreateAssetMenu(fileName = "Domination", menuName = "Shooter/Modes/Domination")]
    public sealed class DominationDefinition : ScriptableObject
    {
        [SerializeField, Min(0.01f), Tooltip("1 人で中立の拠点を占領し終えるまでの秒数")]
        private float _captureSeconds = DominationRunner.DefaultCaptureSeconds;

        [SerializeField, Min(0.01f), Tooltip("拠点 1 つを持っているチームに 1 秒あたり入るポイント")]
        private float _pointsPerSecond = DominationRunner.DefaultPointsPerSecond;

        [SerializeField, Min(1), Tooltip("どちらかのチームがこのポイントに届いたら試合が終わる")]
        private int _scoreLimit = DominationRunner.DefaultScoreLimit;

        [SerializeField, Min(0.01f), Tooltip("試合の制限時間 (秒)")]
        private float _timeLimitSeconds = DominationRunner.DefaultTimeLimitSeconds;

        [SerializeField, Min(1), Tooltip("占領の速さに数える人数の上限")]
        private int _maxCapturers = DominationSpec.DefaultMaxCapturers;

        [SerializeField, Min(0f), Tooltip("倒れてから自チームの出現点に戻るまでの秒数")]
        private float _respawnSeconds = DominationRunner.DefaultRespawnSeconds;

        public float CaptureSeconds
        {
            get => _captureSeconds;
            set => _captureSeconds = value;
        }

        public float PointsPerSecond
        {
            get => _pointsPerSecond;
            set => _pointsPerSecond = value;
        }

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

        public int MaxCapturers
        {
            get => _maxCapturers;
            set => _maxCapturers = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public DominationSpec ToSpec(int pointCount)
        {
            ValidateRespawnSeconds();
            try
            {
                return new DominationSpec(pointCount, _captureSeconds, _pointsPerSecond, _scoreLimit, _timeLimitSeconds, _maxCapturers);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new ArgumentOutOfRangeException(e.ParamName, e.ActualValue, $"domination tuning '{SourceName}' is invalid: {e.Message}");
            }
        }

        public void Validate()
        {
            ToSpec(1);
        }

        private void ValidateRespawnSeconds()
        {
            if (!(_respawnSeconds >= 0f) || float.IsInfinity(_respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(RespawnSeconds),
                    _respawnSeconds,
                    $"domination tuning '{SourceName}' is invalid: respawnSeconds must be a finite, non-negative value");
            }
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed domination tuning)" : name;
    }
}
