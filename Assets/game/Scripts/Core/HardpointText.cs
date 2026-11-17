using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ハードポイント (<see cref="Hardpoint"/>) の <c>[Mode]</c> 付きログの文言。言語に依らない文字列で返す。
    /// 拠点は <see cref="DominationHudFormat.PointName"/> と同じく番号の順に A, B, C ... と呼ぶ。
    /// </summary>
    public static class HardpointText
    {
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
