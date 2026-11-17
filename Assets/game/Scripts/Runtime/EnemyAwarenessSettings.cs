using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class EnemyAwarenessSettings
    {
        [SerializeField, Range(1f, 360f), Tooltip("視野の全角 (度)。外ではゲージがたまらない")]
        private float _fieldOfViewDegrees = AwarenessSpec.DefaultFieldOfViewDegrees;

        [SerializeField, Min(0.01f), Tooltip("すべての係数が 1 のときに 1 秒でたまる量 (満ちて 1)")]
        private float _gainPerSecond = AwarenessSpec.DefaultGainPerSecond;

        [SerializeField, Min(0f), Tooltip("これより近ければ距離でたまり方を落とさない (m)")]
        private float _fullGainDistance = AwarenessSpec.DefaultFullGainDistance;

        [SerializeField, Range(0f, 1f), Tooltip("視認距離の端でのたまり方の係数")]
        private float _farGainFactor = AwarenessSpec.DefaultFarGainFactor;

        [SerializeField, Range(0f, 1f), Tooltip("視野の端でのたまり方の係数")]
        private float _peripheralGainFactor = AwarenessSpec.DefaultPeripheralGainFactor;

        [SerializeField, Range(0f, 1f), Tooltip("相手がしゃがんでいるときの係数")]
        private float _crouchGainFactor = AwarenessSpec.DefaultCrouchGainFactor;

        [SerializeField, Range(0f, 1f), Tooltip("相手が伏せているときの係数")]
        private float _proneGainFactor = AwarenessSpec.DefaultProneGainFactor;

        [SerializeField, Range(0f, 1f), Tooltip("相手が止まっているときの係数")]
        private float _stillGainFactor = AwarenessSpec.DefaultStillGainFactor;

        [SerializeField, Min(0.01f), Tooltip("相手がこの速さ (m/s) 以上で動くと動きの係数が 1")]
        private float _fullMotionSpeed = AwarenessSpec.DefaultFullMotionSpeed;

        [SerializeField, Min(0f), Tooltip("見えなくなってから減り始めるまでの秒数")]
        private float _decayDelaySeconds = AwarenessSpec.DefaultDecayDelaySeconds;

        [SerializeField, Min(0f), Tooltip("見えていない間に 1 秒で減る量")]
        private float _decayPerSecond = AwarenessSpec.DefaultDecayPerSecond;

        [SerializeField, Range(0.01f, 0.99f), Tooltip("これ以上で気づき")]
        private float _noticeThreshold = AwarenessSpec.DefaultNoticeThreshold;

        [SerializeField, Range(0.01f, 0.99f), Tooltip("これ以上で警戒。満ちると交戦")]
        private float _alertThreshold = AwarenessSpec.DefaultAlertThreshold;

        [SerializeField, Range(0.02f, 1f), Tooltip("交戦に入った後、これを下回るまで交戦のまま。警戒の境目より上")]
        private float _engagedHoldThreshold = AwarenessSpec.DefaultEngagedHoldThreshold;

        [SerializeField, Min(0f), Tooltip("交戦に入ったことを仲間に伝える範囲 (m)。0 なら伝えない")]
        private float _alarmRadius = AwarenessSpec.DefaultAlarmRadius;

        [SerializeField, Range(0f, 1f), Tooltip("すぐそばの銃声 1 発でゲージに足す量。離れるほど音の届く距離に比べて減る")]
        private float _gunshotGain = AwarenessSpec.DefaultGunshotGain;

        [SerializeField, Range(0f, 1f), Tooltip("すぐそばの足音 1 歩でゲージに足す量")]
        private float _footstepGain = AwarenessSpec.DefaultFootstepGain;

        [SerializeField, Range(0.01f, 0.99f), Tooltip("物音を聞いて調べに行くときのゲージの量。音だけではこれより上がらない。警戒の境目以上")]
        private float _investigateLevel = AwarenessSpec.DefaultInvestigateLevel;

        /// <exception cref="ArgumentException">値が不正。</exception>
        public AwarenessSpec ToSpec()
        {
            return new AwarenessSpec(
                _fieldOfViewDegrees,
                _gainPerSecond,
                _fullGainDistance,
                _farGainFactor,
                _peripheralGainFactor,
                _crouchGainFactor,
                _proneGainFactor,
                _stillGainFactor,
                _fullMotionSpeed,
                _decayDelaySeconds,
                _decayPerSecond,
                _noticeThreshold,
                _alertThreshold,
                _alarmRadius,
                _engagedHoldThreshold,
                _gunshotGain,
                _footstepGain,
                _investigateLevel);
        }

        public void CopyFrom(AwarenessSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _fieldOfViewDegrees = spec.FieldOfViewDegrees;
            _gainPerSecond = spec.GainPerSecond;
            _fullGainDistance = spec.FullGainDistance;
            _farGainFactor = spec.FarGainFactor;
            _peripheralGainFactor = spec.PeripheralGainFactor;
            _crouchGainFactor = spec.CrouchGainFactor;
            _proneGainFactor = spec.ProneGainFactor;
            _stillGainFactor = spec.StillGainFactor;
            _fullMotionSpeed = spec.FullMotionSpeed;
            _decayDelaySeconds = spec.DecayDelaySeconds;
            _decayPerSecond = spec.DecayPerSecond;
            _noticeThreshold = spec.NoticeThreshold;
            _alertThreshold = spec.AlertThreshold;
            _alarmRadius = spec.AlarmRadius;
            _engagedHoldThreshold = spec.EngagedHoldThreshold;
            _gunshotGain = spec.GunshotGain;
            _footstepGain = spec.FootstepGain;
            _investigateLevel = spec.InvestigateLevel;
        }
    }
}
