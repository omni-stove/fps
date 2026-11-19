using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // ラウンドの区切りは RoundSession から直に受け、キルだけはモードの Runner (ModeRunnerBase.OnKill) から Killed で受ける。
    // MonoBehaviour にしないのは、Runner が試合ごとに作って捨て、Edit Mode のテストでもシーンを組まずに試せるようにするため。
    public sealed class RoundEconomy : IBuySender, IDisposable
    {
        public delegate WeaponDefinition WeaponFinder(string weaponId, out string failure);

        private readonly Economy _economy;
        private readonly RoundSession _rounds;
        private readonly SideMember[] _members;
        private readonly IReadOnlyList<WeaponDefinition>[] _startWeapons;
        private readonly WeaponFinder _findWeapon;
        private readonly int? _localPlayerId;
        private readonly UnityEngine.Object _context;
        private WalletStatus? _publishedWallet;
        private bool _publishedSender;
        private bool _disposed;

        public RoundEconomy(
            EconomyRules rules,
            RoundSession rounds,
            IReadOnlyList<SideMember> members,
            int? localPlayerId,
            WeaponFinder findWeapon,
            UnityEngine.Object context = null)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _rounds = rounds ?? throw new ArgumentNullException(nameof(rounds));
            _findWeapon = findWeapon ?? throw new ArgumentNullException(nameof(findWeapon));
            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            if (localPlayerId.HasValue && (localPlayerId.Value < 0 || localPlayerId.Value >= members.Count || members[localPlayerId.Value] == null))
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerId), localPlayerId, "localPlayerId must be the index of one of the members");
            }

            _economy = new Economy(rules);
            _members = new SideMember[members.Count];
            _startWeapons = new IReadOnlyList<WeaponDefinition>[members.Count];
            for (var id = 0; id < members.Count; id++)
            {
                var member = members[id];
                if (member == null)
                {
                    continue;
                }

                _members[id] = member;
                // 倒れて買った武器を失った人を、シーンの持たせた並びに戻せるよう、始めに覚えておく。
                _startWeapons[id] = ModeLoadout.HeldBy(member);
                _economy.AddPlayer(id, member.Side);
            }

            _localPlayerId = localPlayerId;
            _context = context;

            _economy.WalletChanged += OnWalletChanged;
            _economy.BuyTimeChanged += OnBuyTimeChanged;
            _rounds.PreparationStarted += OnPreparationStarted;
            _rounds.RoundStarted += OnRoundStarted;
            _rounds.RoundEnded += OnRoundEnded;
            _rounds.SidesSwapped += OnSidesSwapped;
            _rounds.Ended += OnEnded;

            Debug.Log(EconomyLog.Started(CountMembers(), rules), _context);
            if (_localPlayerId.HasValue)
            {
                GameSignals.BuySender.Publish(this);
                _publishedSender = true;
                PublishWallet();
            }
        }

        public Economy Economy => _economy;

        public void Buy(string itemId)
        {
            if (_disposed || !_localPlayerId.HasValue)
            {
                return;
            }

            CombatEvents.RaisePurchaseAnswered(itemId, Buy(_localPlayerId.Value, itemId));
        }

        public PurchaseVerdict Buy(int playerId, string itemId)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RoundEconomy));
            }

            var member = MemberOf(playerId);
            if (member == null || !member.IsAlive)
            {
                Debug.Log(EconomyLog.Refused(playerId, itemId, PurchaseVerdict.CannotCarry, MoneyOf(playerId)), _context);
                return PurchaseVerdict.CannotCarry;
            }

            var verdict = _economy.Buy(playerId, itemId, item => CanCarry(playerId, member, item), out var bought);
            if (verdict != PurchaseVerdict.Bought)
            {
                Debug.Log(EconomyLog.Refused(playerId, itemId, verdict, _economy.MoneyOf(playerId)), _context);
                return verdict;
            }

            Debug.Log(EconomyLog.Bought(playerId, bought, _economy.MoneyOf(playerId)), _context);
            Apply(playerId, member, bought);
            return verdict;
        }

        public void Killed(SideMember killer, SideMember victim)
        {
            if (_disposed)
            {
                return;
            }

            var victimId = IdOf(victim);
            var killerId = IdOf(killer);
            if (killerId >= 0 && victimId >= 0)
            {
                var amount = _economy.RewardKill(killerId, victimId);
                if (amount > 0)
                {
                    Debug.Log(EconomyLog.KillReward(killerId, victimId, amount, _economy.MoneyOf(killerId)), _context);
                }
            }

            if (victimId >= 0 && _economy.LoseWeapons(victimId))
            {
                Debug.Log(EconomyLog.LostWeapons(victimId), _context);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _economy.WalletChanged -= OnWalletChanged;
            _economy.BuyTimeChanged -= OnBuyTimeChanged;
            _rounds.PreparationStarted -= OnPreparationStarted;
            _rounds.RoundStarted -= OnRoundStarted;
            _rounds.RoundEnded -= OnRoundEnded;
            _rounds.SidesSwapped -= OnSidesSwapped;
            _rounds.Ended -= OnEnded;

            // 別の Runner が後から出した値は消さない。
            if (_publishedSender)
            {
                GameSignals.BuySender.ClearIfCurrent(this);
            }

            if (_publishedWallet.HasValue)
            {
                GameSignals.Wallet.ClearIfCurrent(_publishedWallet);
            }

            _publishedSender = false;
            _publishedWallet = null;
        }

        private bool CanCarry(int playerId, SideMember member, BuyItem item)
        {
            switch (item.Kind)
            {
                case BuyItemKind.Weapon:
                    return member.GetComponentInChildren<WeaponController>(true) != null && !HoldsStartWeapon(playerId, item) && FindWeapon(item.Id) != null;
                case BuyItemKind.Throwable:
                    var grenades = member.GetComponent<GrenadeThrower>();
                    if (grenades == null || !grenades.UsesGrenades)
                    {
                        return false;
                    }

                    var index = grenades.Inventory.IndexOf(item.Id);
                    return index >= 0 && grenades.Inventory.RemainingAt(index) < grenades.Inventory.CapacityAt(index);
                case BuyItemKind.Armor:
                    var armor = member.GetComponent<IArmorHolder>();
                    return armor != null && armor.MaxArmor > 0 && armor.Armor < armor.MaxArmor;
                default:
                    return false;
            }
        }

        // 買った武器のある枠は Economy.Buy が同じ品かを見るので、ここでは買っていない枠に残る始めの武器だけを見る。
        private bool HoldsStartWeapon(int playerId, BuyItem item)
        {
            if (_economy.WeaponIn(playerId, item.Slot) != null)
            {
                return false;
            }

            var start = _startWeapons[playerId];
            return start != null && item.Slot >= 0 && item.Slot < start.Count && start[item.Slot] != null && start[item.Slot].Id == item.Id;
        }

        private void Apply(int playerId, SideMember member, BuyItem item)
        {
            switch (item.Kind)
            {
                case BuyItemKind.Weapon:
                    Equip(playerId);
                    break;
                case BuyItemKind.Throwable:
                    var grenades = member.GetComponent<GrenadeThrower>();
                    grenades.Inventory.RefillAt(grenades.Inventory.IndexOf(item.Id));
                    break;
                case BuyItemKind.Armor:
                    member.GetComponent<IArmorHolder>().AddArmor(item.Amount);
                    break;
            }
        }

        private void EquipAll()
        {
            for (var id = 0; id < _members.Length; id++)
            {
                if (_members[id] != null)
                {
                    Equip(id);
                }
            }
        }

        private void Equip(int playerId)
        {
            var member = _members[playerId];
            var bought = new List<KeyValuePair<int, WeaponDefinition>>();
            foreach (var pair in _economy.WeaponsOf(playerId))
            {
                var weapon = FindWeapon(pair.Value);
                if (weapon != null)
                {
                    bought.Add(new KeyValuePair<int, WeaponDefinition>(pair.Key, weapon));
                }
            }

            var weapons = ModeLoadout.Slotted(_startWeapons[playerId], bought);
            if (weapons.Count == 0)
            {
                return;
            }

            try
            {
                if (ModeLoadout.EquipController(member, weapons) == true)
                {
                    Debug.Log(EconomyLog.Equipped(playerId, weapons.Count), member);
                }
            }
            catch (Exception e) when (e is WeaponDefinitionException || e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogError(EconomyLog.EquipFailed(playerId, e.Message), member);
            }
        }

        private WeaponDefinition FindWeapon(string weaponId)
        {
            var weapon = _findWeapon(weaponId, out var failure);
            if (weapon == null)
            {
                Debug.LogError(EconomyLog.WeaponMissing(weaponId, failure), _context);
            }

            return weapon;
        }

        private void OnPreparationStarted(int round)
        {
            EquipAll();
            _economy.OpenBuyTime();
            Debug.Log(EconomyLog.BuyTime(true, round), _context);
        }

        private void OnRoundStarted(int round)
        {
            _economy.CloseBuyTime();
            Debug.Log(EconomyLog.BuyTime(false, round), _context);
        }

        private void OnRoundEnded(Side? winner)
        {
            _economy.CloseBuyTime();
            var before = new Dictionary<int, int>();
            foreach (var id in _economy.PlayerIds)
            {
                before.Add(id, _economy.MoneyOf(id));
            }

            _economy.EndRound(winner);
            var ids = new List<int>(before.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                var side = _economy.SideOf(id);
                var money = _economy.MoneyOf(id);
                Debug.Log(EconomyLog.RoundReward(id, side, winner, money - before[id], money, _economy.LossStreakOf(side)), _context);
            }
        }

        private void OnSidesSwapped()
        {
            _economy.SwapSides();
            Debug.Log(EconomyLog.Swapped(_economy.Rules.ResetOnSwap), _context);
        }

        private void OnEnded(Side winner)
        {
            _economy.CloseBuyTime();
        }

        private void OnWalletChanged(int playerId)
        {
            if (_localPlayerId == playerId)
            {
                PublishWallet();
            }
        }

        private void OnBuyTimeChanged(bool open)
        {
            if (_localPlayerId.HasValue)
            {
                PublishWallet();
            }
        }

        private void PublishWallet()
        {
            var status = _economy.StatusOf(_localPlayerId.Value);
            _publishedWallet = status;
            GameSignals.Wallet.Publish(status);
        }

        private int MoneyOf(int playerId)
        {
            return _economy.HasPlayer(playerId) ? _economy.MoneyOf(playerId) : 0;
        }

        private SideMember MemberOf(int playerId)
        {
            return playerId >= 0 && playerId < _members.Length ? _members[playerId] : null;
        }

        private int IdOf(SideMember member)
        {
            return member != null ? Array.IndexOf(_members, member) : -1;
        }

        private int CountMembers()
        {
            var count = 0;
            foreach (var member in _members)
            {
                if (member != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
