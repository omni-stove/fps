using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public static class LootClientLog
    {
        public static string Requested(int itemId, ObjectiveItemKind kind, float distance, bool sent)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net open request item={1} kind={2} distance={3:0.00} sent={4}",
                LootLog.Tag,
                itemId,
                kind,
                distance,
                sent);
        }

        public static string Opened(int itemId, ObjectiveItemKind kind)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} net opened item={1} kind={2}", LootLog.Tag, itemId, kind);
        }

        // サーバーは断った理由を送らないので、待っても開かなかったことだけを出す
        public static string Refused(int itemId, ObjectiveItemKind kind, float waitedSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net open refused item={1} kind={2} reason=not-opened waited={3:0.00}s",
                LootLog.Tag,
                itemId,
                kind,
                waitedSeconds);
        }

        public static string Gone(int itemId, ObjectiveItemKind kind)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} net open refused item={1} kind={2} reason=gone", LootLog.Tag, itemId, kind);
        }

        // 差し替えの見た目が無くてコードの形で出したかを見分けられるようにする
        public static string Look(int itemId, ObjectiveItemKind kind, bool fromPrefab)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} net look item={1} kind={2} prefab={3}", LootLog.Tag, itemId, kind, fromPrefab);
        }
    }
}
