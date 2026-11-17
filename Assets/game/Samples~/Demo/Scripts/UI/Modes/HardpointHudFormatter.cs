using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class HardpointHudFormatter : ModeHudFormatterBase
    {
        public static LocalizedText Empty => new LocalizedText("mode.hardpoint.empty");

        public static LocalizedText Contested => new LocalizedText("mode.hardpoint.contested");

        public static LocalizedText ActivePoint(int pointIndex, int nextPointIndex, float secondsUntilMove)
        {
            return new LocalizedText(
                "mode.hardpoint.activePoint",
                DominationHudFormat.PointName(pointIndex),
                DominationHudFormat.PointName(nextPointIndex),
                HudFormat.DisplayedSeconds(secondsUntilMove));
        }

        public static LocalizedText Status(HardpointStatus status, Side? holder)
        {
            switch (status)
            {
                case HardpointStatus.Empty:
                    return Empty;
                case HardpointStatus.Contested:
                    return Contested;
                case HardpointStatus.Held:
                    if (!holder.HasValue)
                    {
                        throw new ArgumentException("a held point needs a holder", nameof(holder));
                    }

                    return new LocalizedText("mode.hardpoint.held", ModeHudText.SideName(holder.Value));
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "status is not a defined HardpointStatus");
            }
        }

        public static LocalizedText[] HudLines(Hardpoint hardpoint)
        {
            if (hardpoint == null)
            {
                throw new ArgumentNullException(nameof(hardpoint));
            }

            return new[]
            {
                ActivePoint(hardpoint.ActivePoint, hardpoint.NextPoint, hardpoint.SecondsUntilMove),
                Status(hardpoint.Status, hardpoint.Holder),
            };
        }

        protected override void AppendModeLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (status.Detail is Hardpoint hardpoint)
            {
                lines.AddRange(HudLines(hardpoint));
            }
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ModeHudFormatters.Register<Hardpoint>(new HardpointHudFormatter());
        }
    }
}
