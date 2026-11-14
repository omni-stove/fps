using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>爆発する置き物 (<see cref="ExplosiveCharge"/>) の <c>[Explosive]</c> 付きログの文言。</summary>
    public static class ExplosivePropLog
    {
        public const string Tag = "[Explosive]";

        /// <summary><see cref="Exploded"/> の行の始まり。smoke が爆発を見分けるのに使う。</summary>
        public const string ExplodedPrefix = Tag + " exploded ";

        /// <summary>耐久が減ったが残っているとき。</summary>
        /// <example><c>[Explosive] damaged id=Barrel_A dmg=10 hp=10/20</c></example>
        public static string Damaged(string id, int damage, ExplosiveCharge charge)
        {
            if (charge == null)
            {
                throw new ArgumentNullException(nameof(charge));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} damaged id={1} dmg={2} hp={3}/{4}",
                Tag,
                id,
                damage,
                charge.Health,
                charge.Spec.MaxHealth);
        }

        /// <summary>
        /// 耐久が 0 になり爆発を待ち始めたとき (step が <see cref="ExplosiveChargeStep.Triggered"/> か <see cref="ExplosiveChargeStep.Ignited"/>)。
        /// 燃え始めたなら <c>ignited</c>、そうでなければ <c>triggered</c>。
        /// </summary>
        /// <example><c>[Explosive] ignited id=GasCan chained=true delay=2.00</c></example>
        public static string Triggered(string id, ExplosiveChargeStep step, ExplosiveCharge charge)
        {
            if (charge == null)
            {
                throw new ArgumentNullException(nameof(charge));
            }

            if (step != ExplosiveChargeStep.Triggered && step != ExplosiveChargeStep.Ignited)
            {
                throw new ArgumentOutOfRangeException(nameof(step), step, "step must be Triggered or Ignited");
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} id={2} chained={3} delay={4:0.00}",
                Tag,
                step == ExplosiveChargeStep.Ignited ? "ignited" : "triggered",
                id,
                FormatBool(charge.IsChained),
                charge.RemainingSeconds);
        }

        /// <summary>爆発したとき。x, y, z は爆発の中心。</summary>
        /// <example><c>[Explosive] exploded id=Barrel_B chained=true at=(-12.5, 0.5, 14.0) damaged=1 killed=1 structures=2</c></example>
        public static string Exploded(
            string id, bool chained, float x, float y, float z, int damaged, int killed, int structureHits)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}id={1} chained={2} at=({3:0.0}, {4:0.0}, {5:0.0}) damaged={6} killed={7} structures={8}",
                ExplodedPrefix,
                id,
                FormatBool(chained),
                x,
                y,
                z,
                damaged,
                killed,
                structureHits);
        }

        /// <summary>爆発が人 (Damageable) にダメージを与えたとき。</summary>
        /// <example><c>[Explosive] hit id=Barrel_A target=Dummy dmg=150 distance=1.0</c></example>
        public static string Hit(string id, string targetId, int damage, float distance)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} hit id={1} target={2} dmg={3} distance={4:0.0}",
                Tag,
                id,
                targetId,
                damage,
                distance);
        }

        private static string FormatBool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
