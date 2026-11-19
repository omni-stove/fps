using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class VisionLog
    {
        public const string Tag = "[Vision]";

        public static string Changed(VisionStatus previous, VisionStatus next)
        {
            return $"{Tag} {Describe(next)} was={Describe(previous)}";
        }

        public static string Goggles(string ownerId, VisionMode mode, bool on)
        {
            RequireDefined(mode);
            return $"{Tag} goggles owner={Name(ownerId)} mode={mode} {(on ? "on" : "off")}";
        }

        public static string GogglesOffOnDeath(string ownerId)
        {
            return $"{Tag} goggles owner={Name(ownerId)} off reason=died";
        }

        public static string Highlighted(int added, int total, bool codeMaterial)
        {
            RequireCount(added, nameof(added));
            RequireCount(total, nameof(total));
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} thermal highlighted added={1} total={2} material={3}",
                Tag,
                added,
                total,
                codeMaterial ? "code" : "catalog");
        }

        public static string Restored(int restored, int total)
        {
            RequireCount(restored, nameof(restored));
            RequireCount(total, nameof(total));
            return string.Format(CultureInfo.InvariantCulture, "{0} thermal restored={1} total={2}", Tag, restored, total);
        }

        public static string Describe(VisionStatus status)
        {
            return status.IsActive ? $"{status.Mode}/{status.Source}" : "none";
        }

        private static void RequireDefined(VisionMode mode)
        {
            if (!Enum.IsDefined(typeof(VisionMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode is not a defined VisionMode");
            }
        }

        private static void RequireCount(int count, string name)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(name, count, name + " must not be negative");
            }
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
