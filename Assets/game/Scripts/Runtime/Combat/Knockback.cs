using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public static class Knockback
    {
        public const string LogTag = "[Knockback]";

        public const string ReasonLanded = "landed";
        public const string ReasonTimeout = "timeout";

        // 受け手が無い相手 (標的ダミー) も、倒れていれば既定の Spec で抜け殻を押す。爆発の向きに倒れるのは誰でも同じにしたいので。
        public static bool Apply(Damageable target, DamageKind kind, Vector3 direction, float strength)
        {
            if (!TryGetVelocity(target, kind, direction, strength, out var velocity))
            {
                return false;
            }

            if (target.IsDead)
            {
                var presenter = target.GetComponent<DeathBodyPresenter>();
                return presenter != null && presenter.Launch(velocity);
            }

            var receiver = target.GetComponent<IKnockbackReceiver>();
            return receiver != null && receiver.Launch(velocity, kind);
        }

        // とどめの 1 発では、抜け殻を作る DeathBodyPresenter が自分で押すので、生きている相手だけを飛ばす。両方で押すと 2 回押される。
        public static bool ApplyFromReport(Damageable target, DamageReport report)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (target.IsDead || !KnockbackSpec.ComesFromReport(report.Kind) || !report.Direction.HasValue)
            {
                return false;
            }

            return Apply(target, report.Kind, report.Direction.Value, 1f);
        }

        public static bool TryGetVelocity(Damageable target, DamageKind kind, Vector3 direction, float strength, out Vector3 velocity)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var receiver = target.GetComponent<IKnockbackReceiver>();
            var spec = receiver != null && receiver.KnockbackSpec != null ? receiver.KnockbackSpec : KnockbackSpec.Default;
            var weight = receiver != null ? receiver.KnockbackWeight : KnockbackSpec.DefaultWeight;
            var found = spec.TryGetVelocity(kind, direction.ToNumerics(), strength, weight, out var numerics);
            velocity = numerics.ToUnity();
            return found;
        }

        public static string FormatLaunchLog(string id, DamageKind kind, Vector3 velocity)
        {
            return $"{LogTag} id={id} launched kind={kind} velocity={FormatVector(velocity)}";
        }

        public static string FormatLandLog(string id, string reason, float airSeconds, Vector3 at)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} {2} after={3:0.00}s at={4}",
                LogTag,
                id,
                reason,
                airSeconds,
                FormatVector(at));
        }

        // 着地点を NavMesh へ寄せたときだけ末尾に足す。寄せていない着地の行を、寄せた行の頭と同じ形のまま読めるように。
        public static string FormatLandLog(string id, string reason, float airSeconds, Vector3 at, Vector3 snappedFrom)
        {
            return FormatLandLog(id, reason, airSeconds, at) + " snapped-from=" + FormatVector(snappedFrom);
        }

        public static string FormatVector(Vector3 value)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.0}, {1:0.0}, {2:0.0})", value.x, value.y, value.z);
        }
    }
}
