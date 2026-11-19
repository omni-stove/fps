using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // Horde の通貨はチームで 1 つなので使い回さず、人ごとに財布を持つ。
    // 投げ物と防具は使えば減り、残りは持ち主の部品にしか無いので、ここでは持ち続ける武器だけを覚え、持てるかは Buy の canCarry で聞く。
    public sealed class Economy
    {
        public event Action<int> WalletChanged;

        public event Action<bool> BuyTimeChanged;

        private readonly Dictionary<int, Wallet> _wallets = new Dictionary<int, Wallet>();
        private readonly Dictionary<Side, int> _lossStreaks = new Dictionary<Side, int>();

        public Economy(EconomyRules rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public EconomyRules Rules { get; }

        public EconomySpec Spec => Rules.Spec;

        public BuyCatalog Catalog => Rules.Catalog;

        public bool IsBuyTime { get; private set; }

        public IEnumerable<int> PlayerIds => _wallets.Keys;

        public void AddPlayer(int playerId, Side side)
        {
            if (!_wallets.TryAdd(playerId, new Wallet(side, Spec.StartMoney)))
            {
                throw new ArgumentException("player " + playerId + " already has a wallet", nameof(playerId));
            }

            _lossStreaks.TryAdd(side, 0);
            WalletChanged?.Invoke(playerId);
        }

        // 連敗の数は陣営のものなので、最後の 1 人が抜けても残す。
        public bool RemovePlayer(int playerId)
        {
            return _wallets.Remove(playerId);
        }

        public bool HasPlayer(int playerId)
        {
            return _wallets.ContainsKey(playerId);
        }

        public int MoneyOf(int playerId)
        {
            return WalletOf(playerId).Money;
        }

        public Side SideOf(int playerId)
        {
            return WalletOf(playerId).Side;
        }

        public WalletStatus StatusOf(int playerId)
        {
            return new WalletStatus(MoneyOf(playerId), Spec.MaxMoney, IsBuyTime);
        }

        public int LossStreakOf(Side side)
        {
            return _lossStreaks.TryGetValue(side, out var streak) ? streak : 0;
        }

        public string WeaponIn(int playerId, int slot)
        {
            return WalletOf(playerId).Weapons.TryGetValue(slot, out var id) ? id : null;
        }

        // 枠の番号の順に並べて返すのは、持たせる側が枠の順に武器を並べるため。
        public IReadOnlyList<KeyValuePair<int, string>> WeaponsOf(int playerId)
        {
            var list = new List<KeyValuePair<int, string>>(WalletOf(playerId).Weapons);
            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            return list;
        }

        public void OpenBuyTime()
        {
            SetBuyTime(true);
        }

        public void CloseBuyTime()
        {
            SetBuyTime(false);
        }

        // 渡した額を返す。味方や自分を倒したとき、財布の無い人が倒したときは渡さない。
        public int RewardKill(int killerId, int victimId)
        {
            if (killerId == victimId || !_wallets.TryGetValue(killerId, out var killer) || !_wallets.TryGetValue(victimId, out var victim))
            {
                return 0;
            }

            return killer.Side == victim.Side ? 0 : Add(killerId, Spec.KillReward);
        }

        // 引き分け (勝ちが無い) は、どちらの連敗も進めず、どちらにも負けの基本の額を渡す。
        public void EndRound(Side? winner)
        {
            if (winner.HasValue)
            {
                var sides = new List<Side>(_lossStreaks.Keys);
                foreach (var side in sides)
                {
                    _lossStreaks[side] = side == winner.Value ? 0 : _lossStreaks[side] + 1;
                }
            }

            var ids = new List<int>(_wallets.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                Add(id, RoundRewardOf(_wallets[id].Side, winner));
            }
        }

        public int RoundRewardOf(Side side, Side? winner)
        {
            if (!winner.HasValue)
            {
                return Spec.RoundLossReward;
            }

            return side == winner.Value ? Spec.RoundWinReward : Spec.LossReward(Math.Max(1, LossStreakOf(side)));
        }

        public void SwapSides()
        {
            if (Rules.ResetOnSwap)
            {
                ResetWallets();
            }
        }

        public void Reset()
        {
            IsBuyTime = false;
            ResetWallets();
        }

        public bool LoseWeapons(int playerId)
        {
            var weapons = WalletOf(playerId).Weapons;
            if (weapons.Count == 0)
            {
                return false;
            }

            weapons.Clear();
            WalletChanged?.Invoke(playerId);
            return true;
        }

        public PurchaseVerdict Buy(int playerId, string itemId, Func<BuyItem, bool> canCarry = null)
        {
            return Buy(playerId, itemId, canCarry, out _);
        }

        public PurchaseVerdict Buy(int playerId, string itemId, Func<BuyItem, bool> canCarry, out BuyItem item)
        {
            var wallet = WalletOf(playerId);
            Catalog.TryFind(itemId, out item);
            if (!IsBuyTime)
            {
                return PurchaseVerdict.NotBuyTime;
            }

            if (item == null)
            {
                return PurchaseVerdict.UnknownItem;
            }

            if (item.Kind == BuyItemKind.Weapon
                && wallet.Weapons.TryGetValue(item.Slot, out var held)
                && string.Equals(held, item.Id, StringComparison.Ordinal))
            {
                return PurchaseVerdict.CannotCarry;
            }

            if (canCarry != null && !canCarry(item))
            {
                return PurchaseVerdict.CannotCarry;
            }

            if (wallet.Money < item.Price)
            {
                return PurchaseVerdict.NotEnoughMoney;
            }

            wallet.Money -= item.Price;
            if (item.Kind == BuyItemKind.Weapon)
            {
                wallet.Weapons[item.Slot] = item.Id;
            }

            WalletChanged?.Invoke(playerId);
            return PurchaseVerdict.Bought;
        }

        private int Add(int playerId, int amount)
        {
            var wallet = _wallets[playerId];
            var added = (int)Math.Max(0L, Math.Min((long)Spec.MaxMoney, (long)wallet.Money + amount) - wallet.Money);
            if (added == 0)
            {
                return 0;
            }

            wallet.Money += added;
            WalletChanged?.Invoke(playerId);
            return added;
        }

        private void ResetWallets()
        {
            var sides = new List<Side>(_lossStreaks.Keys);
            foreach (var side in sides)
            {
                _lossStreaks[side] = 0;
            }

            var ids = new List<int>(_wallets.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                var wallet = _wallets[id];
                wallet.Money = Spec.StartMoney;
                wallet.Weapons.Clear();
                WalletChanged?.Invoke(id);
            }
        }

        private void SetBuyTime(bool open)
        {
            if (IsBuyTime == open)
            {
                return;
            }

            IsBuyTime = open;
            BuyTimeChanged?.Invoke(open);
        }

        private Wallet WalletOf(int playerId)
        {
            if (!_wallets.TryGetValue(playerId, out var wallet))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "player has no wallet");
            }

            return wallet;
        }

        private sealed class Wallet
        {
            public Wallet(Side side, int money)
            {
                Side = side;
                Money = money;
            }

            public Side Side { get; }

            public int Money { get; set; }

            public Dictionary<int, string> Weapons { get; } = new Dictionary<int, string>();
        }
    }
}
