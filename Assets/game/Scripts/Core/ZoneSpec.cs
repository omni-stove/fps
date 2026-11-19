using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 縮むエリアの段階と外のダメージの与え方。最初の円はマップ (シーンの ShrinkingZone・地点ファイルの zone) が持つので、ここには持たない。
    // 既定はオフラインの ShrinkingZone の既定と同じ値
    public sealed class ZoneSpec
    {
        public const float DefaultDamageIntervalSeconds = 1f;

        public static IReadOnlyList<ZonePhase> DefaultPhases { get; } = new[]
        {
            new ZonePhase(30f, 20f, 35f, 1f),
            new ZonePhase(20f, 15f, 18f, 3f),
            new ZonePhase(15f, 10f, 6f, 8f),
        };

        public static ZoneSpec Default { get; } = new ZoneSpec();

        // seed が null なら、始めるたびに選び直す (ShrinkingZone の UseRandomSeed と同じ)
        public ZoneSpec(IReadOnlyList<ZonePhase> phases = null, float damageIntervalSeconds = DefaultDamageIntervalSeconds, int? seed = null)
        {
            if (phases != null && phases.Count == 0)
            {
                throw new ArgumentException("phases must contain at least one phase", nameof(phases));
            }

            // 最初の円との比べはマップを読むまでできないので ZoneSchedule に任せ、段階どうしの並びだけはここで断る
            for (var i = 1; phases != null && i < phases.Count; i++)
            {
                if (phases[i].Radius > phases[i - 1].Radius)
                {
                    throw new ArgumentException(
                        "phase " + i + " radius " + phases[i].Radius + " is larger than the previous radius " + phases[i - 1].Radius, nameof(phases));
                }
            }

            Phases = phases == null ? DefaultPhases : new List<ZonePhase>(phases).AsReadOnly();
            DamageIntervalSeconds = SpecGuard.PositiveFinite(damageIntervalSeconds, nameof(damageIntervalSeconds));
            Seed = seed;
        }

        public IReadOnlyList<ZonePhase> Phases { get; }

        public float DamageIntervalSeconds { get; }

        public int? Seed { get; }
    }
}
