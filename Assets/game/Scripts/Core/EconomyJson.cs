using System.Collections.Generic;

namespace Shooter.Core
{
    public static class EconomyJson
    {
        public const string ResourcePath = "Tuning/economy";

        // 額の項目名はネット対戦の設定 (NetMatchFile の economy) とそろえ、同じ値を写せるようにする。
        public const string StartMoneyItem = NetMatchFile.StartMoneyItem;
        public const string MaxMoneyItem = NetMatchFile.MaxMoneyItem;
        public const string KillRewardItem = NetMatchFile.KillRewardItem;
        public const string RoundWinRewardItem = NetMatchFile.RoundWinRewardItem;
        public const string RoundLossRewardItem = NetMatchFile.RoundLossRewardItem;
        public const string LossStreakBonusItem = NetMatchFile.LossStreakBonusItem;
        public const string MaxLossRewardItem = NetMatchFile.MaxLossRewardItem;
        public const string ResetOnSwapItem = "resetOnSwap";
        public const string ItemsItem = "items";

        public const string IdItem = "id";
        public const string KindItem = "kind";
        public const string PriceItem = "price";
        public const string SlotItem = "slot";
        public const string AmountItem = "amount";
        public const string NameKeyItem = "nameKey";

        private const string _kind = "economy JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            StartMoneyItem,
            MaxMoneyItem,
            KillRewardItem,
            RoundWinRewardItem,
            RoundLossRewardItem,
            LossStreakBonusItem,
            MaxLossRewardItem,
            ResetOnSwapItem,
            ItemsItem,
        };

        public static IReadOnlyList<string> BuyItemNames { get; } = new[] { IdItem, KindItem, PriceItem, SlotItem, AmountItem, NameKeyItem };

        public static EconomyRules Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var startMoney = fields.ReadInt(StartMoneyItem, EconomySpec.DefaultStartMoney);
                var maxMoney = fields.ReadInt(MaxMoneyItem, EconomySpec.DefaultMaxMoney);
                var killReward = fields.ReadInt(KillRewardItem, EconomySpec.DefaultKillReward);
                var roundWinReward = fields.ReadInt(RoundWinRewardItem, EconomySpec.DefaultRoundWinReward);
                var roundLossReward = fields.ReadInt(RoundLossRewardItem, EconomySpec.DefaultRoundLossReward);
                var lossStreakBonus = fields.ReadInt(LossStreakBonusItem, EconomySpec.DefaultLossStreakBonus);
                var maxLossReward = fields.ReadInt(MaxLossRewardItem, EconomySpec.DefaultMaxLossReward);
                var resetOnSwap = fields.ReadBool(ResetOnSwapItem, EconomyRules.DefaultResetOnSwap);
                var items = SpecJsonReader.ReadList(fields, ItemsItem, BuyItemNames, ReadItem);
                var spec = SpecJsonReader.Build(
                    fields,
                    ItemNames,
                    () => new EconomySpec(startMoney, maxMoney, killReward, roundWinReward, roundLossReward, lossStreakBonus, maxLossReward));
                var catalog = items == null ? BuyCatalog.Empty : SpecJsonReader.Build(fields, ItemNames, () => new BuyCatalog(items));
                return new EconomyRules(spec, catalog, resetOnSwap);
            });
        }

        // 種類と値段は書き忘れると別の品になってしまうので必須にし、枠・量・表示のキーだけ省けるようにする。
        private static BuyItem ReadItem(ServerDataJson.Fields item)
        {
            var id = SpecJsonReader.RequireText(item, IdItem);
            var kind = item.ToEnum<BuyItemKind>(item.ItemPath(KindItem), item.Require(KindItem));
            var price = item.ToInt(item.ItemPath(PriceItem), item.Require(PriceItem));
            var slot = item.ReadInt(SlotItem, 0);
            var amount = item.ReadInt(AmountItem, 0);
            var nameKey = item.ReadString(NameKeyItem, string.Empty);
            return new BuyItem(id, kind, price, slot, amount, nameKey);
        }
    }
}
