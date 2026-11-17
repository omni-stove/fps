using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// ハードポイント (<see cref="HardpointRunner"/>) の HUD の書式役。<see cref="ModeStatus.Detail"/> の <see cref="Hardpoint"/> から、
    /// リスポーンの行の後に、有効な拠点と次へ移るまでの秒数・拠点の様子の 2 行 (<see cref="HudLines"/>) を出す。
    /// 拠点は <see cref="DominationHudFormat.PointName"/> と同じく番号の順に A, B, C ... と呼び、文は表のキー <c>mode.hardpoint.*</c> で返す。
    /// </summary>
    public sealed class HardpointHudFormatter : ModeHudFormatterBase
    {
        /// <summary>有効な拠点に誰もいないときの様子の行。</summary>
        public static LocalizedText Empty => new LocalizedText("mode.hardpoint.empty");

        /// <summary>有効な拠点に両チームがいるときの様子の行。</summary>
        public static LocalizedText Contested => new LocalizedText("mode.hardpoint.contested");

        /// <summary>有効な拠点と、次の拠点へ移るまでの秒数 (切り上げ)。</summary>
        /// <example><c>ActivePoint(1, 2, 22.3f)</c> は <c>拠点 B (次は C まで 23 秒)</c></example>
        public static LocalizedText ActivePoint(int pointIndex, int nextPointIndex, float secondsUntilMove)
        {
            return new LocalizedText(
                "mode.hardpoint.activePoint",
                DominationHudFormat.PointName(pointIndex),
                DominationHudFormat.PointName(nextPointIndex),
                HudFormat.DisplayedSeconds(secondsUntilMove));
        }

        /// <summary>有効な拠点の様子。確保中なら陣営名を出す。</summary>
        /// <example><c>Status(HardpointStatus.Held, Side.Bravo)</c> は <c>BRAVO が確保中</c></example>
        /// <exception cref="ArgumentException">Held なのに holder が null。</exception>
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

        /// <summary>hardpoint の HUD の 2 行 (有効な拠点と移るまでの秒数、様子)。</summary>
        /// <exception cref="ArgumentNullException">hardpoint が null。</exception>
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
