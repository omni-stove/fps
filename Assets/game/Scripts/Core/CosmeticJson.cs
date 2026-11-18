using System.Collections.Generic;

namespace Shooter.Core
{
    public static class CosmeticJson
    {
        public const string ResourcePath = "Tuning/cosmetics";

        public const string ItemsItem = "items";

        public const string IdItem = "id";
        public const string KindItem = "kind";
        public const string NameKeyItem = "nameKey";
        public const string VisualIdItem = "visualId";
        public const string UnlockLevelItem = "unlockLevel";

        private const string _kind = "cosmetic JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { ItemsItem };

        public static IReadOnlyList<string> CosmeticItemNames { get; } = new[] { IdItem, KindItem, NameKeyItem, VisualIdItem, UnlockLevelItem };

        public static CosmeticCatalog Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var items = SpecJsonReader.ReadList(fields, ItemsItem, CosmeticItemNames, ReadItem);
                return SpecJsonReader.Build(fields, ItemNames, () => new CosmeticCatalog(items));
            });
        }

        private static CosmeticItem ReadItem(ServerDataJson.Fields entry)
        {
            var id = SpecJsonReader.RequireText(entry, IdItem);
            var kind = entry.ToEnum<CosmeticKind>(entry.ItemPath(KindItem), entry.Require(KindItem));
            var nameKey = entry.ReadString(NameKeyItem, null);
            var visualId = SpecJsonReader.RequireText(entry, VisualIdItem);
            var unlockLevel = entry.ReadInt(UnlockLevelItem, CosmeticItem.DefaultUnlockLevel);
            return new CosmeticItem(id, kind, nameKey, visualId, unlockLevel);
        }
    }
}
