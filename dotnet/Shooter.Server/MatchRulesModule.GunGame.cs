using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // Gun Game は段ごとの武器を全員に持たせ、倒した武器の Id で段を進めるので、目的の実装 (GunGameRules) にキルの武器を渡し、
    // 段が変わった人の武器を持たせ直させる。それ以外の目的では何もしない
    internal sealed partial class MatchRulesModule
    {
        private readonly Dictionary<string, WeaponSpec> _gunGameWeapons = new Dictionary<string, WeaponSpec>(StringComparer.Ordinal);
        private readonly List<int> _gunGameRearm = new List<int>();
        private readonly List<int> _gunGameRearming = new List<int>();

        // 段が変わって持つ武器が替わった人。ServerCombat が持たせ直す。キルの処理の途中で武器を替えないよう、次の Tick で出す
        public event Action<int> WeaponsChanged;

        // ガンゲームの試合でなければ null
        internal GunGame GunGame => (_objective as GunGameRules)?.GunGame;

        // 段の武器を試合の最中に初めて探して見つからないことが無いよう、起動のうちにロードアウトと一覧から全部の段を引いておく
        private void PrepareGunGame()
        {
            _gunGameWeapons.Clear();
            _gunGameRearm.Clear();
            if (!(_objective is GunGameRules))
            {
                return;
            }

            FillGunGameWeapons(_data, _gunGameWeapons);
        }

        // 受け付けるとき (CheckNextRules) と作り直すときで同じ確認を使う
        private static void FillGunGameWeapons(ServerRulesData data, Dictionary<string, WeaponSpec> weapons)
        {
            var file = data.File;
            foreach (var weapon in file.LoadoutWeapons)
            {
                weapons[weapon.Id] = weapon;
            }

            if (file.Catalog != null)
            {
                foreach (var weapon in file.Catalog.All)
                {
                    weapons[weapon.Id] = weapon;
                }
            }

            var tiers = data.Settings.GunGame.TierWeaponIds;
            for (var i = 0; i < tiers.Count; i++)
            {
                if (!weapons.ContainsKey(tiers[i]))
                {
                    throw new InvalidOperationException(GunGameRulesLog.MissingTierWeapon(i + 1, tiers[i], data.Name));
                }
            }
        }

        // ガンゲームなら、今の段の武器 1 つだけを持たせる (選んだロードアウトは使わない)
        private bool TryGunGameWeapons(int playerId, out IReadOnlyList<WeaponSpec> weapons)
        {
            if (!(_objective is GunGameRules gunGame) || !_gunGameWeapons.TryGetValue(gunGame.WeaponIdFor(playerId), out var weapon))
            {
                weapons = null;
                return false;
            }

            weapons = new[] { weapon };
            return true;
        }

        private void CreditGunGameKill(KillFeedEntry entry)
        {
            if (!(_objective is GunGameRules gunGame))
            {
                return;
            }

            var killerBefore = gunGame.WeaponIdFor(entry.KillerId);
            gunGame.CreditKill(entry);
            if (!string.Equals(killerBefore, gunGame.WeaponIdFor(entry.KillerId), StringComparison.Ordinal)
                && !_gunGameRearm.Contains(entry.KillerId))
            {
                _gunGameRearm.Add(entry.KillerId);
            }
        }

        // 倒された人の段が下がっても、出直すとき (PlayerSpawned) に新しい段の武器を持つので、ここで替えるのは倒した人だけ
        private void TickGunGame()
        {
            if (_gunGameRearm.Count == 0)
            {
                return;
            }

            _gunGameRearming.Clear();
            _gunGameRearming.AddRange(_gunGameRearm);
            _gunGameRearm.Clear();
            foreach (var playerId in _gunGameRearming)
            {
                if (_session.HasPlayer(playerId) && _session.IsAlive(playerId))
                {
                    _log.WriteLine(GunGameRulesLog.Rearm(playerId, ((GunGameRules)_objective).WeaponIdFor(playerId)));
                    WeaponsChanged?.Invoke(playerId);
                }
            }

            _gunGameRearming.Clear();
        }
    }
}
