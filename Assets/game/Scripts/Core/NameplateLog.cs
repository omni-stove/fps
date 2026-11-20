using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class NameplateLog
    {
        public const string Tag = "[Nameplate]";

        public static string View(string view, NameplateSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} view={1} friendlyRange={2:0.0} enemyRange={3:0.0}",
                Tag,
                string.IsNullOrWhiteSpace(view) ? "none" : view.Trim(),
                spec.FriendlyRange,
                spec.EnemyRange);
        }

        public static string Shown(int id, TeamRelation relation, float distance)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} shown id={1} relation={2} distance={3:0.0}", Tag, id, relation, distance);
        }

        public static string Source(string source)
        {
            return Tag + " source=" + (string.IsNullOrWhiteSpace(source) ? "none" : source.Trim());
        }

        public static string Hidden(int id)
        {
            return Tag + " hidden id=" + id.ToString(CultureInfo.InvariantCulture);
        }
    }
}
