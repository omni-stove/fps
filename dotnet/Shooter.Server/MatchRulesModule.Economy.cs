using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 人ごとの財布 (Core の Economy) は、目的がラウンドの区切りを知らせ (IEconomyRounds)、買える品の一覧 (tuning/economy.json) が置かれているときだけ動かす。
    // 額は試合の設定 (NetMatchSettings.Economy) から、品と値段は一覧から読む。買える時間はラウンドの準備の間で、オフラインの RoundEconomy と同じ。
    // サーバーは投げ物を出現のたびに満たし直し、防具を持たないので、買えるのは武器だけにする
    internal sealed partial class MatchRulesModule
    {
        private readonly Dictionary<string, WeaponSpec> _economyWeapons = new Dictionary<string, WeaponSpec>(StringComparer.Ordinal);
        private readonly List<int> _economyRearm = new List<int>();
        private readonly List<int> _economyRearming = new List<int>();
        private Economy _economy;
        private IEconomyRounds _economyRounds;
        private float _nextEconomyRefusalLogTime;

        // 経済の無い試合なら null。試合ごとに作り直す
        internal Economy Economy => _economy;

        internal PurchaseVerdict ServerBuy(int playerId, string itemId)
        {
            if (_economy == null || !_economy.HasPlayer(playerId))
            {
                RefuseBuy(EconomyLog.Refused(playerId, null, PurchaseVerdict.NotBuyTime, 0));
                return PurchaseVerdict.NotBuyTime;
            }

            // 倒れている人は持ち替えられないので、買える時間でも断る (オフラインと同じ)
            if (!_session.HasPlayer(playerId) || !_session.IsAlive(playerId) || _session.IsDowned(playerId))
            {
                var dead = _economy.IsBuyTime ? PurchaseVerdict.CannotCarry : PurchaseVerdict.NotBuyTime;
                RefuseBuy(EconomyLog.Refused(playerId, ListedItemId(itemId), dead, _economy.MoneyOf(playerId)));
                return dead;
            }

            var verdict = _economy.Buy(playerId, itemId, item => CanCarryBought(playerId, item), out var bought);
            if (verdict != PurchaseVerdict.Bought)
            {
                RefuseBuy(EconomyLog.Refused(playerId, ListedItemId(itemId), verdict, _economy.MoneyOf(playerId)));
                return verdict;
            }

            _log.WriteLine(EconomyLog.Bought(playerId, bought, _economy.MoneyOf(playerId)));
            if (!_economyRearm.Contains(playerId))
            {
                _economyRearm.Add(playerId);
            }

            return verdict;
        }

        partial void OnBuyRequest(ServerPlayer player, BuyRequestMessage request)
        {
            TouchIdle(player.PlayerId);
            ServerBuy(player.PlayerId, request.ItemId);
        }

        // 起動のときと、次の試合でルールが替わって全員を入れ直した後に呼ぶ
        partial void PrepareEconomy()
        {
            StopEconomy();
            if (!(_objective is IEconomyRounds rounds))
            {
                return;
            }

            var rules = _data.Economy;
            if (rules == null)
            {
                _log.WriteLine(EconomyLog.Off("no buy list (" + ServerRulesData.TuningDirectoryName + "/" + ServerRulesData.EconomyTuningFileName
                    + ") is beside the rules " + _data.Name));
                return;
            }

            FillEconomyWeapons(rules);
            _economyRounds = rounds;
            rounds.PreparationStarted += HandleEconomyPreparationStarted;
            rounds.RoundStarted += HandleEconomyRoundStarted;
            rounds.RoundEnded += HandleEconomyRoundEnded;
            rounds.SidesSwapped += HandleEconomySidesSwapped;
            rounds.Ended += HandleEconomyEnded;
            _economy = CreateEconomy(rules);
            _log.WriteLine(EconomyLog.Started(CountWallets(), rules));
        }

        // 買った武器への持ち替えは、要求を受けた処理の途中で武器を替えないよう次の Tick で出す (Gun Game と同じ)
        partial void TickEconomy(float deltaSeconds)
        {
            if (_economyRearm.Count == 0)
            {
                return;
            }

            _economyRearming.Clear();
            _economyRearming.AddRange(_economyRearm);
            _economyRearm.Clear();
            foreach (var playerId in _economyRearming)
            {
                if (_economy != null && _session.HasPlayer(playerId) && _session.IsAlive(playerId))
                {
                    _log.WriteLine(EconomyLog.Equipped(playerId, LoadoutFor(playerId).Count));
                    WeaponsChanged?.Invoke(playerId);
                }
            }

            _economyRearming.Clear();
        }

        private void StopEconomy()
        {
            if (_economyRounds != null)
            {
                _economyRounds.PreparationStarted -= HandleEconomyPreparationStarted;
                _economyRounds.RoundStarted -= HandleEconomyRoundStarted;
                _economyRounds.RoundEnded -= HandleEconomyRoundEnded;
                _economyRounds.SidesSwapped -= HandleEconomySidesSwapped;
                _economyRounds.Ended -= HandleEconomyEnded;
                _economyRounds = null;
            }

            _economy = null;
            _economyWeapons.Clear();
            _economyRearm.Clear();
        }

        // 試合の点数を戻した直後 (続けて全員が出現する前) に呼ぶ。チームの入れ替えの後なので、今のチームで財布を作り直す
        private void EconomyMatchRestarted()
        {
            if (_economy == null)
            {
                return;
            }

            _economyRearm.Clear();
            _economy = CreateEconomy(_economy.Rules);
            _log.WriteLine(EconomyLog.Started(CountWallets(), _economy.Rules));
        }

        private void EconomyPlayerJoined(int playerId)
        {
            if (_economy == null || _economy.HasPlayer(playerId) || !_session.HasPlayer(playerId))
            {
                return;
            }

            _economy.AddPlayer(playerId, Side.FromTeam(_session.GetTeam(playerId)));
        }

        private void EconomyPlayerLeft(int playerId)
        {
            _economyRearm.Remove(playerId);
            if (_economy != null && _economy.RemovePlayer(playerId))
            {
                _log.WriteLine(EconomyLog.Left(playerId));
            }
        }

        // 倒された人は買った武器を失い、次の出現からロードアウトの武器に戻る
        private void EconomyPlayerKilled(int killerId, int victimId)
        {
            if (_economy == null)
            {
                return;
            }

            var amount = _economy.RewardKill(killerId, victimId);
            if (amount > 0)
            {
                _log.WriteLine(EconomyLog.KillReward(killerId, victimId, amount, _economy.MoneyOf(killerId)));
            }

            if (_economy.HasPlayer(victimId) && _economy.LoseWeapons(victimId))
            {
                _economyRearm.Remove(victimId);
                _log.WriteLine(EconomyLog.LostWeapons(victimId));
            }
        }

        // 他の人の所持金は見せないので、受け取る人の分だけを載せる。経済の無い試合と財布の無い人は 0
        private int MoneyOf(ServerPlayer receiver)
        {
            return receiver != null && _economy != null && _economy.HasPlayer(receiver.PlayerId) ? _economy.MoneyOf(receiver.PlayerId) : 0;
        }

        // 買った武器のある枠を置き換えた並び。経済が無いか何も買っていなければ、選んだ並び (無ければ既定の並び) のまま
        private IReadOnlyList<WeaponSpec> WithBoughtWeapons(int playerId, IReadOnlyList<WeaponSpec> chosen)
        {
            if (_economy == null || !_economy.HasPlayer(playerId))
            {
                return chosen;
            }

            var bought = _economy.WeaponsOf(playerId);
            if (bought.Count == 0)
            {
                return chosen;
            }

            var weapons = new WeaponSpec[chosen.Count];
            for (var i = 0; i < weapons.Length; i++)
            {
                weapons[i] = chosen[i];
            }

            foreach (var pair in bought)
            {
                if (pair.Key < weapons.Length && _economyWeapons.TryGetValue(pair.Value, out var weapon))
                {
                    weapons[pair.Key] = weapon;
                }
            }

            return weapons;
        }

        // 持っている武器と同じ物と、枠のルール (LoadoutRule) に合わない物は買わせない。持てない品にお金を払わせないため
        private bool CanCarryBought(int playerId, BuyItem item)
        {
            if (item.Kind != BuyItemKind.Weapon || !_economyWeapons.TryGetValue(item.Id, out var weapon))
            {
                return false;
            }

            var held = LoadoutFor(playerId);
            if (item.Slot >= held.Count || string.Equals(held[item.Slot].Id, item.Id, StringComparison.Ordinal))
            {
                return false;
            }

            var next = new List<WeaponSpec>(held);
            next[item.Slot] = weapon;
            return _loadout.Rule.Validate(next) == null;
        }

        // 試合の最中に初めて探して見つからないことが無いよう、作るときにロードアウトと武器の一覧から引いておき、持たせられない品はここで知らせる
        private void FillEconomyWeapons(EconomyRules rules)
        {
            _economyWeapons.Clear();
            var file = _data.File;
            var known = new Dictionary<string, WeaponSpec>(StringComparer.Ordinal);
            foreach (var weapon in file.LoadoutWeapons)
            {
                known[weapon.Id] = weapon;
            }

            if (file.Catalog != null)
            {
                foreach (var weapon in file.Catalog.All)
                {
                    known[weapon.Id] = weapon;
                }
            }

            foreach (var item in rules.Catalog.Items)
            {
                if (item.Kind != BuyItemKind.Weapon)
                {
                    _log.WriteLine(EconomyLog.NotSold(item, "the server only hands out bought weapons"));
                    continue;
                }

                if (!known.TryGetValue(item.Id, out var weapon))
                {
                    _log.WriteLine(EconomyLog.WeaponMissing(item.Id, "not in the loadout or the weapon catalog of rules " + _data.Name));
                    continue;
                }

                if (item.Slot >= _loadout.Rule.SlotCount)
                {
                    _log.WriteLine(EconomyLog.WeaponMissing(
                        item.Id, "slot " + Text(item.Slot) + " is not one of the " + Text(_loadout.Rule.SlotCount) + " loadout slots of rules " + _data.Name));
                    continue;
                }

                _economyWeapons[item.Id] = weapon;
            }
        }

        private Economy CreateEconomy(EconomyRules rules)
        {
            var economy = new Economy(rules);
            foreach (var player in _match.Players)
            {
                var playerId = player.PlayerId;
                if (_session.HasPlayer(playerId) && !economy.HasPlayer(playerId))
                {
                    economy.AddPlayer(playerId, Side.FromTeam(_session.GetTeam(playerId)));
                }
            }

            return economy;
        }

        private int CountWallets()
        {
            var count = 0;
            foreach (var id in _economy.PlayerIds)
            {
                count++;
            }

            return count;
        }

        // 一覧に無い Id は送ってきた側の文字列なので、ログに出さない (ServerLoadout.Resolve と同じ)
        private string ListedItemId(string itemId)
        {
            return _economy.Catalog.TryFind(itemId, out var item) ? item.Id : null;
        }

        private void RefuseBuy(string line)
        {
            if (_now < _nextEconomyRefusalLogTime)
            {
                return;
            }

            _nextEconomyRefusalLogTime = _now + _rejectWarningInterval;
            _log.WriteLine(line);
        }

        // 出直させた後に開けるので、ラウンドの初めの持ち替えは前のラウンドまでに買った武器で済んでいる
        private void HandleEconomyPreparationStarted(int round)
        {
            _economy?.OpenBuyTime();
            _log.WriteLine(EconomyLog.BuyTime(true, round));
        }

        private void HandleEconomyRoundStarted(int round)
        {
            _economy?.CloseBuyTime();
            _log.WriteLine(EconomyLog.BuyTime(false, round));
        }

        private void HandleEconomyRoundEnded(Side? winner)
        {
            if (_economy == null)
            {
                return;
            }

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
                _log.WriteLine(EconomyLog.RoundReward(id, side, winner, money - before[id], money, _economy.LossStreakOf(side)));
            }
        }

        private void HandleEconomySidesSwapped()
        {
            if (_economy == null)
            {
                return;
            }

            _economy.SwapSides();
            _log.WriteLine(EconomyLog.Swapped(_economy.Rules.ResetOnSwap));
        }

        private void HandleEconomyEnded(Side winner)
        {
            _economy?.CloseBuyTime();
        }
    }
}
