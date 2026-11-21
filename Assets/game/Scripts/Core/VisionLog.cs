using System;
using System.Collections.Generic;
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

        public static string GogglesOffAbilitiesOff(string ownerId)
        {
            return $"{Tag} goggles owner={Name(ownerId)} off reason=abilities-off";
        }

        // 能力の枠の順で、ゴーグルでない枠と空の枠は -
        public static string NetSlots(IReadOnlyList<string> gogglesIds)
        {
            if (gogglesIds == null)
            {
                throw new ArgumentNullException(nameof(gogglesIds));
            }

            var names = new string[gogglesIds.Count];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = Name(gogglesIds[i]);
            }

            return $"{Tag} net goggles slots={string.Join(",", names)}";
        }

        public static string NetSlotsEmptied(string reason)
        {
            return $"{Tag} net goggles slots emptied: {reason}";
        }

        // slot は 0 から数えた枠で、ログには入力と同じ 1 から数えた番号で出す
        public static string NetSlotInvalid(int slot, string reason)
        {
            RequireCount(slot, nameof(slot));
            return string.Format(CultureInfo.InvariantCulture, "{0} net goggles slot={1} not equipped: {2}", Tag, slot + 1, reason);
        }

        public static string NetAttached(string ownerId)
        {
            return $"{Tag} net vision attached owner={Name(ownerId)}";
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
