using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class ExplosivePropLog
    {
        public const string Tag = "[Explosive]";

        // smoke が爆発の行を見分けるのに使うので、行の始まりを公開する。
        public const string ExplodedPrefix = Tag + " exploded ";

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
