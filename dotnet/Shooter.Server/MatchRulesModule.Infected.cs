using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // Infected の感染者の武器 (InfectedSpec.InfectedWeaponId) は、目的 (InfectedRules) からは武器の一覧を引けないので、
    // Gun Game の段の武器と同じくここでロードアウトと一覧から引き、感染者にはその武器 1 つだけを配る。それ以外の目的では何もしない
    internal sealed partial class MatchRulesModule
    {
        private readonly List<int> _infectedRearm = new List<int>();
        private readonly List<int> _infectedRearming = new List<int>();
        private InfectedRules _infectedRules;
        private WeaponSpec _infectedWeapon;

        // Infected の試合でないか、感染しても持っている武器のままにするルールなら null
        internal WeaponSpec InfectedWeapon => _infectedWeapon;

        // 感染者の武器を試合の最中に初めて探して見つからないことが無いよう、起動のうち (と次のルールを受け付けるとき) に引いておく
        private void PrepareInfected()
        {
            ForgetInfected();
            if (!(_objective is InfectedRules infected))
            {
                return;
            }

            _infectedWeapon = FindInfectedWeapon(_data);
            _infectedRules = infected;
            infected.Rearming += HandleInfectedRearming;
            if (_infectedWeapon != null)
            {
                _log.WriteLine(InfectedRulesLog.Weapon(_infectedWeapon.Id, _data.Name));
            }
        }

        private void ForgetInfected()
        {
            if (_infectedRules != null)
            {
                _infectedRules.Rearming -= HandleInfectedRearming;
            }

            _infectedRules = null;
            _infectedWeapon = null;
            _infectedRearm.Clear();
        }

        // 受け付けるとき (CheckNextRules) と作り直すときで同じ引き方を使う。Id が空なら null (持っている武器のまま)。
        // 一覧を先に見るのは、Gun Game の段 (FillGunGameWeapons) と同じく同じ Id なら一覧の値を使うため
        private static WeaponSpec FindInfectedWeapon(ServerRulesData data)
        {
            var spec = data.Settings.Infected;
            if (!spec.HasInfectedWeapon)
            {
                return null;
            }

            var id = spec.InfectedWeaponId;
            var file = data.File;
            if (file.Catalog != null && file.Catalog.TryGet(id, out var listed))
            {
                return listed;
            }

            foreach (var weapon in file.LoadoutWeapons)
            {
                if (string.Equals(weapon.Id, id, StringComparison.Ordinal))
                {
                    return weapon;
                }
            }

            throw new InvalidOperationException(InfectedRulesLog.MissingWeapon(id, data.Name));
        }

        // 感染者なら感染者の武器 1 つだけを持たせる (選んだロードアウトと買った武器は使わない)
        private bool TryInfectedWeapons(int playerId, out IReadOnlyList<WeaponSpec> weapons)
        {
            if (_infectedWeapon == null || _infectedRules == null || !_infectedRules.IsInfected(playerId))
            {
                weapons = null;
                return false;
            }

            weapons = new[] { _infectedWeapon };
            return true;
        }

        // 感染はキルの処理の途中 (Session の Killed) か目的の Tick の中で起きるので、武器はその処理を抜けた後の TickInfected で替える
        private void HandleInfectedRearming(int playerId)
        {
            if (_infectedWeapon != null && !_infectedRearm.Contains(playerId))
            {
                _infectedRearm.Add(playerId);
            }
        }

        // 倒されて感染した人は出直すとき (PlayerSpawned) に感染者の武器を持つので、ここで替えるのは立ったまま感染した人
        // (最初の感染者と、感染が始まった後に入った人) だけ
        private void TickInfected()
        {
            if (_infectedRearm.Count == 0)
            {
                return;
            }

            _infectedRearming.Clear();
            _infectedRearming.AddRange(_infectedRearm);
            _infectedRearm.Clear();
            foreach (var playerId in _infectedRearming)
            {
                if (_session.HasPlayer(playerId) && _session.IsAlive(playerId) && _infectedRules.IsInfected(playerId))
                {
                    _log.WriteLine(InfectedRulesLog.Rearm(playerId, _infectedWeapon.Id));
                    WeaponsChanged?.Invoke(playerId);
                }
            }

            _infectedRearming.Clear();
        }
    }
}
