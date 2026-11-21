using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public static class PickupClientLog
    {
        public static string Requested(int itemId, ObjectiveItemKind kind, float distance, bool sent)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net collect request item={1} kind={2} distance={3:0.00} sent={4}",
                PickupLog.Tag,
                itemId,
                kind,
                distance,
                sent);
        }

        public static string Collected(int itemId, ObjectiveItemKind kind)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} net collected item={1} kind={2}", PickupLog.Tag, itemId, kind);
        }

        // 落とし物は拾われると並びから消えるので、消えたことだけを出す
        public static string Gone(int itemId, ObjectiveItemKind kind)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} net collected item={1} kind={2} reason=gone", PickupLog.Tag, itemId, kind);
        }

        // サーバーは断った理由を送らないので、待っても拾えなかったことだけを出す
        public static string Refused(int itemId, ObjectiveItemKind kind, float waitedSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net collect refused item={1} kind={2} reason=not-collected waited={3:0.00}s",
                PickupLog.Tag,
                itemId,
                kind,
                waitedSeconds);
        }

        // 差し替えの見た目が無くてコードの形で出したかを見分けられるようにする
        public static string Look(int itemId, ObjectiveItemKind kind, bool fromPrefab)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} net look item={1} kind={2} prefab={3}", PickupLog.Tag, itemId, kind, fromPrefab);
        }
    }
}
