using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AircraftWarningSettings
    {
        [SerializeField, Range(0.1f, 90f), Tooltip("迎え角 (度) がこれ以上になると、空中で失速の警告を出す")]
        private float _stallAngleDegrees = AircraftWarningSpec.DefaultStallAngleDegrees;

        [SerializeField, Min(0f), Tooltip("対気速度 (m/s) がこれより遅いと、空中で失速の警告を出す")]
        private float _stallSpeed = AircraftWarningSpec.DefaultStallSpeed;

        public AircraftWarningSpec ToSpec()
        {
            return new AircraftWarningSpec(_stallAngleDegrees, _stallSpeed);
        }

        public void CopyFrom(AircraftWarningSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _stallAngleDegrees = spec.StallAngleDegrees;
            _stallSpeed = spec.StallSpeed;
        }
    }
}
