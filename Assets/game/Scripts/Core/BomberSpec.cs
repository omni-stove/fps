using System;

namespace Fps.Core
{
    public sealed class BomberSpec
    {
        public const float DefaultTriggerDistance = 3f;
        public const float DefaultFuseSeconds = 1.5f;
        public const float DefaultRunSpeedMultiplier = 1.6f;

        public const int DefaultBlastDamage = 80;
        public const float DefaultBlastRadius = 4.5f;
        public const float DefaultBlastFullDamageRadius = 1.5f;

        public const int DefaultDeathBlastDamage = 25;
        public const float DefaultDeathBlastRadius = 2.5f;
        public const float DefaultDeathBlastFullDamageRadius = 0.5f;

        public static readonly ExplosionDamage DefaultBlast = new ExplosionDamage(
            DefaultBlastDamage, DefaultBlastRadius, DefaultBlastFullDamageRadius);

        public static readonly ExplosionDamage DefaultDeathBlast = new ExplosionDamage(
            DefaultDeathBlastDamage, DefaultDeathBlastRadius, DefaultDeathBlastFullDamageRadius);

        public static readonly BomberSpec Default = new BomberSpec(
            DefaultTriggerDistance,
            DefaultFuseSeconds,
            DefaultRunSpeedMultiplier,
            DefaultBlast,
            DefaultDeathBlast);

        public float TriggerDistance { get; }

        public float FuseSeconds { get; }

        // 盾持ちの ApproachSpeedMultiplier と同じく、歩きの速さに掛ける。ダッシュの倍率に頼ると、攻撃距離の内では歩きに戻ってしまう。
        public float RunSpeedMultiplier { get; }

        public ExplosionDamage Blast { get; }

        public ExplosionDamage DeathBlast { get; }

        /// <exception cref="ArgumentNullException">blast か deathBlast が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">値が範囲の外。</exception>
        public BomberSpec(float triggerDistance, float fuseSeconds, float runSpeedMultiplier, ExplosionDamage blast, ExplosionDamage deathBlast)
        {
            if (blast == null)
            {
                throw new ArgumentNullException(nameof(blast));
            }

            if (deathBlast == null)
            {
                throw new ArgumentNullException(nameof(deathBlast));
            }

            // 倒されたときの爆発が本来の爆発より強いと、近づかれる前に撃ち倒すほど損をする。
            if (deathBlast.MaxDamage > blast.MaxDamage || deathBlast.Radius > blast.Radius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deathBlast), "deathBlast must not deal more damage or reach farther than blast");
            }

            // 爆発の届かない距離で合図を始めると、近づかれた相手が何もせずとも巻き込まれない。
            if (!(triggerDistance > 0f) || triggerDistance >= blast.Radius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(triggerDistance), triggerDistance, "triggerDistance must be positive and less than the blast radius");
            }

            // 0 秒にすると合図を見せずに爆発し、見てから離れるという前提が崩れる。
            if (!(fuseSeconds > 0f) || float.IsInfinity(fuseSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(fuseSeconds), fuseSeconds, "fuseSeconds must be positive and finite");
            }

            if (!(runSpeedMultiplier > 0f) || float.IsInfinity(runSpeedMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runSpeedMultiplier), runSpeedMultiplier, "runSpeedMultiplier must be positive and finite");
            }

            TriggerDistance = triggerDistance;
            FuseSeconds = fuseSeconds;
            RunSpeedMultiplier = runSpeedMultiplier;
            Blast = blast;
            DeathBlast = deathBlast;
        }
    }
}
