using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ハードポイント (<see cref="Hardpoint"/>) の HUD の行と、<c>[Mode]</c> 付きログの文言。
    /// 拠点は <see cref="DominationHudFormat.PointName"/> と同じく番号の順に A, B, C ... と呼ぶ。
    /// HUD の行は表のキー <c>mode.hardpoint.*</c> の <see cref="LocalizedText"/>、ログは言語に依らない文字列で返す。
    /// </summary>
    public static class HardpointText
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

        /// <summary>有効な拠点が移った。</summary>
        /// <example><c>[Mode] hardpoint moved from=A to=B</c></example>
        public static string MovedLog(int previousPointIndex, int nextPointIndex)
        {
            return ModeLog.Tag + " hardpoint moved from=" + DominationHudFormat.PointName(previousPointIndex)
                + " to=" + DominationHudFormat.PointName(nextPointIndex);
        }

        /// <summary>有効な拠点の様子が変わった。holder は確保している陣営の ID で、いなければ <c>-</c>。</summary>
        /// <example><c>[Mode] hardpoint point=B status=Held holder=1</c></example>
        public static string StatusLog(int pointIndex, HardpointStatus status, Side? holder)
        {
            return ModeLog.Tag + " hardpoint point=" + DominationHudFormat.PointName(pointIndex)
                + " status=" + status
                + " holder=" + (holder.HasValue ? holder.Value.Id.ToString(CultureInfo.InvariantCulture) : "-");
        }
    }
}
