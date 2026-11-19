using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AircraftStickSettings
    {
        [SerializeField, Range(0f, 0.99f), Tooltip("この倒し具合までは操縦桿を倒していないとみなす")]
        private float _deadzone = AircraftStickSpec.DefaultDeadzone;

        [SerializeField, Min(1f), Tooltip("倒し具合に掛ける指数。大きいほど中ほどが鈍く、押し切ったときだけ強く効く。1 なら比例")]
        private float _exponent = AircraftStickSpec.DefaultExponent;

        [SerializeField, Min(0.01f), Tooltip("スロットルの開き具合が狙いの値へ寄る速さ (1/秒)")]
        private float _throttleRate = AircraftStickSpec.DefaultThrottleRate;

        [SerializeField, Tooltip("前へ倒すと機首が下がる (飛行機の操縦桿と同じ向き)。外すと前で機首が上がる")]
        private bool _forwardPitchesDown = AircraftStickSpec.DefaultForwardPitchesDown;

        public AircraftStickSpec ToSpec()
        {
            return new AircraftStickSpec(_deadzone, _exponent, _throttleRate, _forwardPitchesDown);
        }

        public void CopyFrom(AircraftStickSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _deadzone = spec.Deadzone;
            _exponent = spec.Exponent;
            _throttleRate = spec.ThrottleRate;
            _forwardPitchesDown = spec.ForwardPitchesDown;
        }
    }
}
