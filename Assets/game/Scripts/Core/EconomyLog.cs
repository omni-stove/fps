using System.Globalization;

namespace Shooter.Core
{
    public static class EconomyLog
    {
        public const string Tag = "[Economy]";

        public static string Started(int players, EconomyRules rules)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} started players={1} start={2} max={3} items={4} resetOnSwap={5}",
                Tag,
                players,
                rules.Spec.StartMoney,
                rules.Spec.MaxMoney,
                rules.Catalog.Count,
                rules.ResetOnSwap ? "yes" : "no");
        }

        public static string BuyTime(bool open, int round)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} round={1} buy={2}", Tag, round, open ? "open" : "closed");
        }

        public static string KillReward(int killerId, int victimId, int amount, int money)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} player={1} kill victim={2} +{3} money={4}", Tag, killerId, victimId, amount, money);
        }

        public static string RoundReward(int playerId, Side side, Side? winner, int amount, int money, int lossStreak)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} player={1} side={2} round {3} +{4} money={5} lossStreak={6}",
                Tag,
                playerId,
                side.Id,
                !winner.HasValue ? "draw" : winner.Value == side ? "won" : "lost",
                amount,
                money,
                lossStreak);
        }

        public static string Swapped(bool reset)
        {
            return Tag + " sides swapped " + (reset ? "wallets reset" : "wallets kept");
        }

        public static string Bought(int playerId, BuyItem item, int money)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} player={1} bought item={2} kind={3} slot={4} price={5} money={6}",
                Tag,
                playerId,
                item.Id,
                item.Kind,
                item.Slot,
                item.Price,
                money);
        }

        public static string Refused(int playerId, string itemId, PurchaseVerdict verdict, int money)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} player={1} refused item={2} verdict={3} money={4}",
                Tag,
                playerId,
                string.IsNullOrEmpty(itemId) ? "-" : itemId,
                verdict,
                money);
        }

        public static string LostWeapons(int playerId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} player={1} lost the bought weapons", Tag, playerId);
        }

        public static string Equipped(int playerId, int weapons)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} player={1} equipped weapons={2}", Tag, playerId, weapons);
        }

        public static string EquipFailed(int playerId, string problem)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} player={1} cannot be equipped: {2}", Tag, playerId, problem);
        }

        public static string WeaponMissing(string weaponId, string problem)
        {
            return $"{Tag} the weapon '{weaponId}' in the buy list cannot be equipped: {(string.IsNullOrEmpty(problem) ? "not found" : problem)}";
        }

        public static string Off(string reason)
        {
            return Tag + " off: " + reason;
        }

        public static string NotSold(BuyItem item, string reason)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} item={1} kind={2} is not sold: {3}", Tag, item.Id, item.Kind, reason);
        }

        public static string Left(int playerId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} player={1} left; the wallet is dropped", Tag, playerId);
        }

        public static string NetWallet(WalletStatus? status)
        {
            return status.HasValue ? Tag + " net " + status.Value : Tag + " net no wallet";
        }

        public static string NetBuy(string itemId, bool sent)
        {
            return Tag + " net buy item=" + ItemText(itemId) + (sent ? string.Empty : " (not connected; not sent)");
        }

        public static string NetBuyRefused(string itemId, string reason)
        {
            return Tag + " net buy item=" + ItemText(itemId) + " (" + reason + "; not sent)";
        }

        public static string NetBuyAnswer(string itemId, PurchaseVerdict verdict)
        {
            return Tag + " net answer item=" + ItemText(itemId) + " verdict=" + verdict;
        }

        private static string ItemText(string itemId)
        {
            return string.IsNullOrEmpty(itemId) ? "-" : itemId;
        }
    }
}
