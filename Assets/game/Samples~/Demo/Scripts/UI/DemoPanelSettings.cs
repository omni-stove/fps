using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public static class DemoPanelSettings
    {
        public const PanelScaleMode ScaleMode = PanelScaleMode.ScaleWithScreenSize;

        public const PanelScreenMatchMode ScreenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;

        public const float Match = 0.5f;

        public static Vector2Int ReferenceResolution => Hud.ReferenceResolution;

        public static PanelSettings Create(string name, ThemeStyleSheet theme)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("panel settings name must not be empty", nameof(name));
            }

            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme), "panel settings need the Demo theme style sheet");
            }

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = name;
            settings.themeStyleSheet = theme;
            settings.scaleMode = ScaleMode;
            settings.screenMatchMode = ScreenMatchMode;
            settings.referenceResolution = ReferenceResolution;
            settings.match = Match;
            return settings;
        }
    }
}
