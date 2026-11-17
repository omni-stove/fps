using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public static class InventoryJson
    {
        public const string RuleItem = "rule";
        public const string ItemsItem = "items";
        public const string StartingItem = "starting";

        public const string MaxStacksItem = "maxStacks";
        public const string MaxWeightItem = "maxWeight";
        public const string GridWidthItem = "gridWidth";
        public const string GridHeightItem = "gridHeight";
        public const string AllowRotationItem = "allowRotation";
        public const string CountWeightInCarryLoadItem = "countWeightInCarryLoad";

        public const string IdItem = "id";
        public const string NameKeyItem = "nameKey";
        public const string IconIdItem = "iconId";
        public const string MaxStackItem = "maxStack";
        public const string MaxCarriedItem = "maxCarried";
        public const string WeightItem = "weight";
        public const string WidthItem = "width";
        public const string HeightItem = "height";
        public const string UseHealthItem = "useHealth";
        public const string UseArmorItem = "useArmor";
        public const string WeaponIdItem = "weaponId";
        public const string AmmoItemIdItem = "ammoItemId";
        public const string AttachmentItem = "attachment";
        public const string AttachmentSlotsItem = "attachmentSlots";

        public const string SlotItem = "slot";
        public const string WeaponIdsItem = "weaponIds";
        public const string RangeMultiplierItem = "rangeMultiplier";
        public const string HipSpreadMultiplierItem = "hipSpreadMultiplier";
        public const string AdsSpreadMultiplierItem = "adsSpreadMultiplier";
        public const string MoveSpreadMultiplierItem = "moveSpreadMultiplier";
        public const string RecoilMultiplierItem = "recoilMultiplier";
        public const string ReloadSecondsMultiplierItem = "reloadSecondsMultiplier";
        public const string MagazineSizeBonusItem = "magazineSizeBonus";
        public const string ShotNoiseRadiusMultiplierItem = "shotNoiseRadiusMultiplier";
        public const string MoveSpeedMultiplierItem = "moveSpeedMultiplier";
        public const string ZoomLevelsItem = "zoomLevels";

        public const string CountItem = "count";

        private const string _kind = "inventory JSON";

        public static IReadOnlyList<string> CatalogItemNames { get; } = new[] { RuleItem, ItemsItem, StartingItem };

        public static IReadOnlyList<string> RuleItemNames { get; } = new[]
        {
            MaxStacksItem,
            MaxWeightItem,
            GridWidthItem,
            GridHeightItem,
            AllowRotationItem,
            CountWeightInCarryLoadItem,
        };

        public static IReadOnlyList<string> ItemItemNames { get; } = new[]
        {
            IdItem,
            NameKeyItem,
            IconIdItem,
            MaxStackItem,
            MaxCarriedItem,
            WeightItem,
            WidthItem,
            HeightItem,
            UseHealthItem,
            UseArmorItem,
            WeaponIdItem,
            AmmoItemIdItem,
            AttachmentItem,
            AttachmentSlotsItem,
        };

        public static IReadOnlyList<string> AttachmentItemNames { get; } = new[]
        {
            SlotItem,
            WeaponIdsItem,
            RangeMultiplierItem,
            HipSpreadMultiplierItem,
            AdsSpreadMultiplierItem,
            MoveSpreadMultiplierItem,
            RecoilMultiplierItem,
            ReloadSecondsMultiplierItem,
            MagazineSizeBonusItem,
            ShotNoiseRadiusMultiplierItem,
            MoveSpeedMultiplierItem,
            ZoomLevelsItem,
        };

        public static IReadOnlyList<string> StartingItemNames { get; } = new[] { IdItem, CountItem };

        public static ItemCatalog Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, CatalogItemNames);
                var ruleFields = fields.Object(RuleItem, RuleItemNames);
                var rule = ruleFields == null ? InventoryRule.Default : ReadRule(ruleFields);
                var items = new List<ItemSpec>();
                var itemNodes = fields.Array(ItemsItem);
                for (var i = 0; i < itemNodes.Count; i++)
                {
                    items.Add(ReadItem(fields.Element(itemNodes[i], ItemsItem, i, ItemItemNames)));
                }

                var starting = new List<ItemCount>();
                var startingNodes = fields.Array(StartingItem);
                for (var i = 0; i < startingNodes.Count; i++)
                {
                    var entry = fields.Element(startingNodes[i], StartingItem, i, StartingItemNames);
                    try
                    {
                        starting.Add(new ItemCount(entry.ReadString(IdItem, null), entry.ReadInt(CountItem, 1)));
                    }
                    catch (ArgumentException e)
                    {
                        throw entry.Rejected(entry.ItemPath(e.ParamName == "count" ? CountItem : IdItem), e.Message, e);
                    }
                }

                try
                {
                    var catalog = new ItemCatalog(items, rule, starting);
                    catalog.CheckAttachments();
                    return catalog;
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(e.ParamName == "starting" ? StartingItem : ItemsItem, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static InventoryRule ReadRule(ServerDataJson.Fields fields)
        {
            try
            {
                return new InventoryRule(
                    fields.ReadInt(MaxStacksItem, InventoryRule.DefaultMaxStacks),
                    fields.ReadFloat(MaxWeightItem, InventoryRule.DefaultMaxWeight),
                    fields.ReadInt(GridWidthItem, InventoryRule.DefaultGridWidth),
                    fields.ReadInt(GridHeightItem, InventoryRule.DefaultGridHeight),
                    fields.ReadBool(AllowRotationItem, InventoryRule.DefaultAllowRotation),
                    fields.ReadBool(CountWeightInCarryLoadItem, InventoryRule.DefaultCountWeightInCarryLoad));
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(RuleItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
            }
        }

        private static ItemSpec ReadItem(ServerDataJson.Fields fields)
        {
            var weaponId = fields.ReadString(WeaponIdItem, null);
            var weight = fields.ReadFloat(WeightItem, ItemSpec.DefaultWeight);
            var attachmentFields = fields.Object(AttachmentItem, AttachmentItemNames);
            var attachment = attachmentFields == null ? null : ReadAttachment(attachmentFields);
            var slots = Texts(fields, AttachmentSlotsItem);

            // 武器の重さは武器のデータ (WeaponSpec.Weight) から取るので、ここにも書くとどちらが効くか読み手が迷う
            if (!string.IsNullOrWhiteSpace(weaponId) && weight != ItemSpec.DefaultWeight)
            {
                throw fields.Rejected(fields.ItemPath(WeightItem), "a weapon item takes its weight from the weapon; remove weight", null);
            }

            try
            {
                return new ItemSpec(
                    fields.ReadString(IdItem, null),
                    fields.ReadString(NameKeyItem, null),
                    fields.ReadString(IconIdItem, null),
                    fields.ReadInt(MaxStackItem, ItemSpec.DefaultMaxStack),
                    fields.ReadInt(MaxCarriedItem, ItemSpec.DefaultMaxCarried),
                    weight,
                    fields.ReadInt(WidthItem, ItemSpec.DefaultWidth),
                    fields.ReadInt(HeightItem, ItemSpec.DefaultHeight),
                    fields.ReadInt(UseHealthItem, ItemSpec.DefaultUseHealth),
                    fields.ReadInt(UseArmorItem, ItemSpec.DefaultUseArmor),
                    weaponId,
                    fields.ReadString(AmmoItemIdItem, null),
                    attachment,
                    slots);
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(ItemItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
            }
        }

        private static AttachmentSpec ReadAttachment(ServerDataJson.Fields fields)
        {
            var zoomNodes = fields.Find(ZoomLevelsItem) == null ? null : fields.Array(ZoomLevelsItem);
            List<float> zoomLevels = null;
            if (zoomNodes != null)
            {
                zoomLevels = new List<float>(zoomNodes.Count);
                for (var i = 0; i < zoomNodes.Count; i++)
                {
                    zoomLevels.Add(fields.ToFloat(fields.ElementPath(ZoomLevelsItem, i), zoomNodes[i]));
                }
            }

            try
            {
                return new AttachmentSpec(
                    fields.ReadString(SlotItem, null),
                    Texts(fields, WeaponIdsItem),
                    fields.ReadFloat(RangeMultiplierItem, AttachmentSpec.DefaultMultiplier),
                    fields.ReadFloat(HipSpreadMultiplierItem, AttachmentSpec.DefaultMultiplier),
                    fields.ReadFloat(AdsSpreadMultiplierItem, AttachmentSpec.DefaultMultiplier),
                    fields.ReadFloat(MoveSpreadMultiplierItem, AttachmentSpec.DefaultMultiplier),
                    fields.ReadFloat(RecoilMultiplierItem, AttachmentSpec.DefaultMultiplier),
                    fields.ReadFloat(ReloadSecondsMultiplierItem, AttachmentSpec.DefaultMultiplier),
                    fields.ReadInt(MagazineSizeBonusItem, AttachmentSpec.DefaultMagazineSizeBonus),
                    fields.ReadFloat(ShotNoiseRadiusMultiplierItem, AttachmentSpec.DefaultMultiplier),
                    fields.ReadFloat(MoveSpeedMultiplierItem, AttachmentSpec.DefaultMultiplier),
                    zoomLevels);
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(AttachmentItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
            }
        }

        private static List<string> Texts(ServerDataJson.Fields fields, string name)
        {
            var nodes = fields.Array(name);
            var texts = new List<string>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                texts.Add(fields.ToText(fields.ElementPath(name, i), nodes[i]));
            }

            return texts;
        }

        private static bool Known(IReadOnlyList<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
