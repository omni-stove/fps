using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 振る・当てる・外す・拒むの行はオフラインと同じ MeleeLog を使い、ここにはサーバーだけが出す行を置く
    internal static class ServerMeleeLog
    {
        public static string Started(MeleeSpec spec, string weaponId, Melee rule)
        {
            return MeleeLog.LogTag + " server rule=" + rule
                + " damage=" + spec.Damage.ToString(CultureInfo.InvariantCulture)
                + " reach=" + Text(spec.Reach)
                + " arc=" + Text(spec.ArcDegrees)
                + " impact=" + Text(spec.ImpactSeconds)
                + " swing=" + Text(spec.SwingSeconds)
                + " cooldown=" + Text(spec.CooldownSeconds)
                + " weapon=" + (string.IsNullOrEmpty(weaponId) ? "held" : weaponId);
        }

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
