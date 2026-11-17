using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "CaptureTheFlag", menuName = "Shooter/Modes/Capture The Flag")]
    public sealed class CaptureTheFlagDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("どちらかのチームがこの得点に届いたら試合が終わる")]
        private int _scoreLimit = CaptureTheFlagSpec.DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = CaptureTheFlagSpec.DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = CaptureTheFlagSpec.DefaultRespawnSeconds;

        [SerializeField, Min(0.1f), Tooltip("旗に触れたことにする距離 (m、水平)。敵の旗を持つのも、落ちた自陣の旗を台に戻すのもこの距離")]
        private float _pickupRadius = CaptureTheFlagSpec.DefaultPickupRadius;

        [SerializeField, Min(0.1f), Tooltip("敵の旗を運ぶ人が自陣の台に着いたことにする距離 (m、水平)")]
        private float _captureRadius = CaptureTheFlagSpec.DefaultCaptureRadius;

        [SerializeField, Min(0.1f), Tooltip("落ちた旗が誰にも触れられずに台へ戻るまでの秒数")]
        private float _returnSeconds = CaptureTheFlagSpec.DefaultReturnSeconds;

        [SerializeField, Min(1), Tooltip("敵の旗を 1 回自陣の台へ運んだときに入る点")]
        private int _pointsPerCapture = CaptureTheFlagSpec.DefaultPointsPerCapture;

        [SerializeField, Min(0.05f), Tooltip("bot の用事 (旗を取る・運ぶ・取り返す・守る) を決め直す間隔 (秒)")]
        private float _botPlanIntervalSeconds = CaptureTheFlagRunner.DefaultBotPlanIntervalSeconds;

        [SerializeField, Range(0f, 1f), Tooltip("陣営の bot のうち攻めの役にする割合。残りは自陣の台を守る")]
        private float _botAttackShare = CaptureTheFlagBotPlanner.DefaultAttackShare;

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

        public float PickupRadius
        {
            get => _pickupRadius;
            set => _pickupRadius = value;
        }

        public float CaptureRadius
        {
            get => _captureRadius;
            set => _captureRadius = value;
        }

        public float ReturnSeconds
        {
            get => _returnSeconds;
            set => _returnSeconds = value;
        }

        public int PointsPerCapture
        {
            get => _pointsPerCapture;
            set => _pointsPerCapture = value;
        }

        public float BotPlanIntervalSeconds
        {
            get => _botPlanIntervalSeconds;
            set => _botPlanIntervalSeconds = value;
        }

        public float BotAttackShare
        {
            get => _botAttackShare;
            set => _botAttackShare = value;
        }

        // Spec に入らない bot の間隔と割合もここで範囲を確かめるのは、不正なアセットで試合を始めないようにするため。
        public CaptureTheFlagSpec ToSpec()
        {
            try
            {
                CaptureTheFlagRunner.RequireBotTuning(_botPlanIntervalSeconds, _botAttackShare);
                return new CaptureTheFlagSpec(
                    _scoreLimit, _timeLimitSeconds, _respawnSeconds, _pickupRadius, _captureRadius, _returnSeconds, _pointsPerCapture);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"capture the flag definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(CaptureTheFlagSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _scoreLimit = spec.ScoreLimit;
            _timeLimitSeconds = spec.TimeLimitSeconds;
            _respawnSeconds = spec.RespawnSeconds;
            _pickupRadius = spec.PickupRadius;
            _captureRadius = spec.CaptureRadius;
            _returnSeconds = spec.ReturnSeconds;
            _pointsPerCapture = spec.PointsPerCapture;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed capture the flag definition)" : name;
    }
}
