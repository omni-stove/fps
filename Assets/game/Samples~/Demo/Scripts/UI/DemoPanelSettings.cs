using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public static class DemoPanelSettings
    {
        public const PanelScaleMode ScaleMode = PanelScaleMode.ScaleWithScreenSize;

        public const PanelScreenMatchMode ScreenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;

        public const float Match = 0.5f;

        public const float BaseScale = 1f;

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で空にする。
        private static readonly List<PanelSettings> _live = new List<PanelSettings>();

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
            settings.scale = ScaleFor(AppliedControlSettings.Current);
            Follow(settings);
            return settings;
        }

        public static float ScaleFor(ControlSettings controls)
        {
            if (controls == null)
            {
                throw new ArgumentNullException(nameof(controls));
            }

            return BaseScale * controls.UiScale;
        }

        private static void Follow(PanelSettings settings)
        {
            _live.RemoveAll(live => live == null);
            _live.Add(settings);

            // 購読の有無を覚えておくと、AppliedControlSettings が Play の開始時に購読を消したのと食い違うので、毎回付け直して 1 つにする。
            AppliedControlSettings.Changed -= HandleSettingsChanged;
            AppliedControlSettings.Changed += HandleSettingsChanged;
        }

        // 画面ごとに購読させると、作って捨てるたびに外し忘れが出るので、作った PanelSettings をここでまとめて追う。
        private static void HandleSettingsChanged(ControlSettings controls)
        {
            var scale = ScaleFor(controls);
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var settings = _live[i];
                if (settings == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                settings.scale = scale;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _live.Clear();
            AppliedControlSettings.Changed -= HandleSettingsChanged;
        }
    }
}
