using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class AimSpec
    {
        public const float DefaultRecoilRecoveryDelaySeconds = 0.15f;

        // 静的フィールドは書いた順に初期化されるので、Default より先に置く。
        private static readonly float[] _defaultFieldOfViews = { AdsZoom.DefaultFieldOfView };

        private static readonly IReadOnlyList<float> _defaultZoomLevels = Array.AsReadOnly(_defaultFieldOfViews);

        public static readonly AimSpec Default = new AimSpec();

        public AimSpec(
            float hipSpreadDegrees = 0f,
            float adsSpreadDegrees = 0f,
            float moveSpreadDegrees = 0f,
            float recoilPitchDegrees = 0f,
            float recoilYawDegrees = 0f,
            float recoilRecoveryPerSecond = 0f,
            float recoilRecoveryDelaySeconds = DefaultRecoilRecoveryDelaySeconds,
            float blindFireSpreadDegrees = 0f,
            IEnumerable<float> zoomLevels = null,
            BreathHoldSpec breathHold = null,
            float swayDegrees = 0f)
        {
            HipSpreadDegrees = SpecGuard.Angle(hipSpreadDegrees, nameof(hipSpreadDegrees));
            AdsSpreadDegrees = SpecGuard.Angle(adsSpreadDegrees, nameof(adsSpreadDegrees));
            MoveSpreadDegrees = SpecGuard.Angle(moveSpreadDegrees, nameof(moveSpreadDegrees));
            RecoilPitchDegrees = SpecGuard.Angle(recoilPitchDegrees, nameof(recoilPitchDegrees));
            RecoilYawDegrees = SpecGuard.Angle(recoilYawDegrees, nameof(recoilYawDegrees));
            RecoilRecoveryPerSecond = SpecGuard.NonNegativeFinite(recoilRecoveryPerSecond, nameof(recoilRecoveryPerSecond));
            RecoilRecoveryDelaySeconds = SpecGuard.NonNegativeFinite(recoilRecoveryDelaySeconds, nameof(recoilRecoveryDelaySeconds));
            BlindFireSpreadDegrees = SpecGuard.Angle(blindFireSpreadDegrees, nameof(blindFireSpreadDegrees));
            ZoomLevels = zoomLevels == null ? _defaultZoomLevels : Array.AsReadOnly(CopyZoomLevels(zoomLevels));
            BreathHold = breathHold;
            SwayDegrees = SpecGuard.Angle(swayDegrees, nameof(swayDegrees));
        }

        public float HipSpreadDegrees { get; }

        public float AdsSpreadDegrees { get; }

        public float MoveSpreadDegrees { get; }

        public float RecoilPitchDegrees { get; }

        public float RecoilYawDegrees { get; }

        public float RecoilRecoveryPerSecond { get; }

        public float RecoilRecoveryDelaySeconds { get; }

        public float BlindFireSpreadDegrees { get; }

        public IReadOnlyList<float> ZoomLevels { get; }

        public float AdsFieldOfView => ZoomLevels[0];

        public BreathHoldSpec BreathHold { get; }

        public float SwayDegrees { get; }

        private static float[] CopyZoomLevels(IEnumerable<float> zoomLevels)
        {
            var levels = new List<float>(zoomLevels).ToArray();
            if (levels.Length == 0)
            {
                throw new ArgumentException("zoomLevels must have at least one field of view", nameof(zoomLevels));
            }

            foreach (var level in levels)
            {
                AdsZoom.ValidateFieldOfView(level, nameof(zoomLevels));
            }

            return levels;
        }
    }
}
