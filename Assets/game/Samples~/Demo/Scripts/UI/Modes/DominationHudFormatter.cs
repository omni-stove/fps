using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class DominationHudFormatter : ModeHudFormatterBase
    {
        public static LocalizedText[] HudLines(DominationStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            var lines = new LocalizedText[status.Points.Count];
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] = DominationHudText.PointOwner(i, status.Points[i].Owner);
            }

            return lines;
        }

        protected override void AppendModeLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (status.Detail is DominationStatus domination)
            {
                lines.AddRange(HudLines(domination));
            }
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ModeHudFormatters.Register<DominationStatus>(new DominationHudFormatter());
        }
    }
}
