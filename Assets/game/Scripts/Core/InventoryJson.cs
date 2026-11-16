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
                    return new ItemCatalog(items, rule, starting);
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
            try
            {
                return new ItemSpec(
                    fields.ReadString(IdItem, null),
                    fields.ReadString(NameKeyItem, null),
                    fields.ReadString(IconIdItem, null),
                    fields.ReadInt(MaxStackItem, ItemSpec.DefaultMaxStack),
                    fields.ReadInt(MaxCarriedItem, ItemSpec.DefaultMaxCarried),
                    fields.ReadFloat(WeightItem, ItemSpec.DefaultWeight),
                    fields.ReadInt(WidthItem, ItemSpec.DefaultWidth),
                    fields.ReadInt(HeightItem, ItemSpec.DefaultHeight),
                    fields.ReadInt(UseHealthItem, ItemSpec.DefaultUseHealth),
                    fields.ReadInt(UseArmorItem, ItemSpec.DefaultUseArmor));
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(ItemItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
            }
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
