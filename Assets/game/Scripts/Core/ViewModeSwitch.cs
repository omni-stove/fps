using System;

namespace Shooter.Core
{
    public static class ViewModeSwitch
    {
        public const string LogTag = "[ViewSwitch]";

        public enum Refusal
        {
            None = 0,

            NoLocalView,

            ViewBorrowed,
        }

        public static ViewMode Other(ViewMode mode)
        {
            switch (mode)
            {
                case ViewMode.FirstPerson: return ViewMode.ThirdPerson;
                case ViewMode.ThirdPerson: return ViewMode.FirstPerson;
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode is not a defined ViewMode");
            }
        }

        public static string FormatSwitchLog(ViewMode from, ViewMode to)
        {
            return $"{LogTag} view: {from} -> {to}";
        }

        public static string FormatRefusalLog(Refusal refusal)
        {
            switch (refusal)
            {
                case Refusal.NoLocalView: return $"{LogTag} refused: no local player view in this scene";
                case Refusal.ViewBorrowed: return $"{LogTag} refused: the view is borrowed by the death or vehicle camera";
                default: throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "refusal must name a reason");
            }
        }
    }
}
