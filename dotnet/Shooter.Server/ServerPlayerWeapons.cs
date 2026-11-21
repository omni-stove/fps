using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerPlayerWeapons
    {
        // 押していた時間は届いた時刻の差で測るので、前の射撃だけ遅れて届くと短く見える。その揺れで正しく溜めた弾を削らないよう、この秒数だけ長く見る。
        public const double ChargeArrivalToleranceSeconds = 0.2d;

        private readonly int _playerId;
        private readonly TextWriter _log;
        private readonly Func<double> _now;
        private Loadout<ServerWeapon> _loadout;
        private double _reloadStartedAt;
        private float _chargeFraction;
        private double _chargeFrom;
        private int _burstRoundsLeft;
        private double _burstUntil;
        private Func<WeaponSpec, IAmmoPool> _poolFor;

        private IReadOnlyList<WeaponSpec> _baseSpecs = Array.Empty<WeaponSpec>();
        private readonly Dictionary<string, IReadOnlyList<string>> _attachments = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        public ServerPlayerWeapons(int playerId, TextWriter log, Func<double> now)
        {
            _playerId = playerId;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _now = now ?? throw new ArgumentNullException(nameof(now));
        }

        public bool IsEquipped => _loadout != null;

        public WeaponSpec CurrentSpec => _loadout?.Current.Spec;

        public bool IsSwitching => _loadout != null && _loadout.IsSwitching;

        // 盾の武器は弾を込めない (オフラインの WeaponController.StartReload と同じ)
        private bool HoldsShield => _loadout != null && _loadout.Current.Spec.IsShield;

        public IReadOnlyList<ServerWeapon> Items => _loadout?.Items ?? Array.Empty<ServerWeapon>();

        public IReadOnlyList<WeaponSpec> Specs { get; private set; } = Array.Empty<WeaponSpec>();

        public RoundSpec CurrentRound => _loadout == null ? null : CurrentAmmoType.Round;

        public ImpactSpec CurrentImpact => _loadout == null ? null : CurrentAmmoType.Impact;

        public byte AmmoTypeIndex => _loadout == null ? (byte)0 : (byte)(CurrentAmmoType.Index + 1);

        private AmmoTypeSelector CurrentAmmoType => _loadout.Current.AmmoTypes;

        public void Equip(IReadOnlyList<WeaponSpec> specs, float switchSeconds, GameRules rules)
        {
            ArgumentNullException.ThrowIfNull(specs);
            ArgumentNullException.ThrowIfNull(rules);
            if (specs.Count == 0 || specs.Count > OwnerSnapshot.MaxWeapons)
            {
                throw new ArgumentException("specs must hold 1-" + OwnerSnapshot.MaxWeapons + " weapons", nameof(specs));
            }

            // 前の並びの武器は受け皿 (持ち物) の Changed に購読が残るので、捨てる前に外す
            foreach (var old in Items)
            {
                old.UseAmmoPool(null);
            }

            var weapons = new ServerWeapon[specs.Count];
            for (var i = 0; i < specs.Count; i++)
            {
                weapons[i] = new ServerWeapon(specs[i] ?? throw new ArgumentException("specs must not contain null", nameof(specs)), rules);
                if (_poolFor != null)
                {
                    weapons[i].UseAmmoPool(_poolFor(specs[i]));
                }
            }

            _chargeFraction = 0f;
            _chargeFrom = _now();
            _burstRoundsLeft = 0;
            _baseSpecs = new List<WeaponSpec>(specs).AsReadOnly();
            _attachments.Clear();
            SetLoadout(new Loadout<ServerWeapon>(weapons, switchSeconds, HolsterWeapon, DrawSeconds, HolsterSeconds));
        }

        // 箱から拾った武器のように、出直しを待たずに外から持たせる口。今の武器を構えたまま並びの末尾に足す。
        // 同じ武器を持っている・並びが一杯なら足さない (持ち替えの並びは同じ Id を 2 つ持てないため)
        public bool Give(WeaponSpec spec, float switchSeconds, GameRules rules)
        {
            ArgumentNullException.ThrowIfNull(spec);
            ArgumentNullException.ThrowIfNull(rules);
            var items = Items;
            foreach (var held in items)
            {
                if (string.Equals(held.Spec.Id, spec.Id, StringComparison.Ordinal))
                {
                    _log.WriteLine(ServerWeaponsLog.Give(_playerId, spec.Id, "held"));
                    return false;
                }
            }

            if (items.Count >= OwnerSnapshot.MaxWeapons)
            {
                _log.WriteLine(ServerWeaponsLog.Give(_playerId, spec.Id, "full"));
                return false;
            }

            if (_loadout == null)
            {
                Equip(new[] { spec }, switchSeconds, rules);
                _log.WriteLine(ServerWeaponsLog.Give(_playerId, spec.Id, null));
                return true;
            }

            var weapon = new ServerWeapon(spec, rules);
            if (_poolFor != null)
            {
                weapon.UseAmmoPool(_poolFor(spec));
            }

            var weapons = new List<ServerWeapon>(items) { weapon };
            _baseSpecs = new List<WeaponSpec>(_baseSpecs) { spec }.AsReadOnly();
            SetLoadout(new Loadout<ServerWeapon>(weapons, switchSeconds, HolsterWeapon, DrawSeconds, HolsterSeconds, _loadout.CurrentIndex));
            _log.WriteLine(ServerWeaponsLog.Give(_playerId, spec.Id, null));
            return true;
        }

        // 武器をすべて手放す。次の Equip か Give まで撃てない (TryFire は NoWeapon を返す)
        public void Clear()
        {
            if (_loadout == null)
            {
                return;
            }

            var count = _loadout.Count;
            foreach (var weapon in _loadout.Items)
            {
                weapon.UseAmmoPool(null);
            }

            Unsubscribe();
            _loadout = null;
            Specs = Array.Empty<WeaponSpec>();
            _baseSpecs = Array.Empty<WeaponSpec>();
            _attachments.Clear();
            _chargeFraction = 0f;
            _burstRoundsLeft = 0;
            _log.WriteLine(ServerWeaponsLog.Clear(_playerId, count));
        }

        // 次の武器は持ち替えの待ちを課さずに構える。並びを作り直す Give・Refill と同じく、TrySelect で待たせると構える番号が先に動いてしまうため。
        // 付けた物を外した元の値を返すのは、Take と同じく、拾い直した先で付けた物を別に扱えるようにするため
        public bool Drop(string weaponId, out WeaponSpec spec)
        {
            spec = null;
            if (_loadout == null || weaponId == null)
            {
                return false;
            }

            var items = _loadout.Items;
            var index = -1;
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Spec.Id, weaponId, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return false;
            }

            spec = _baseSpecs[index];
            if (items.Count == 1)
            {
                Clear();
                _log.WriteLine(ServerWeaponsLog.Drop(_playerId, weaponId, 0));
                return true;
            }

            var current = _loadout.CurrentIndex;
            var weapons = new List<ServerWeapon>(items);
            var baseSpecs = new List<WeaponSpec>(_baseSpecs);
            weapons[index].UseAmmoPool(null);
            weapons.RemoveAt(index);
            baseSpecs.RemoveAt(index);
            _baseSpecs = baseSpecs.AsReadOnly();
            _attachments.Remove(weaponId);
            var selected = current > index ? current - 1 : Math.Min(current, weapons.Count - 1);
            _chargeFraction = 0f;
            _chargeFrom = _now();
            _burstRoundsLeft = 0;
            SetLoadout(new Loadout<ServerWeapon>(weapons, _loadout.SwitchSeconds, HolsterWeapon, DrawSeconds, HolsterSeconds, selected));
            _log.WriteLine(ServerWeaponsLog.Drop(_playerId, weaponId, weapons.Count));
            return true;
        }

        // 持っている武器を並びの順に取り出して手放す。付けた物を外した元の値と、付けていた物の Id を返すのは、
        // 取り出した先 (倒れた人の箱など) が付けた物を別の拾い物として扱えるようにするため
        public IReadOnlyList<(WeaponSpec Spec, IReadOnlyList<string> Attachments)> Take()
        {
            var taken = new List<(WeaponSpec Spec, IReadOnlyList<string> Attachments)>(_baseSpecs.Count);
            foreach (var spec in _baseSpecs)
            {
                taken.Add((spec, AttachmentsOf(spec.Id)));
            }

            Clear();
            return taken;
        }

        public IReadOnlyList<string> AttachmentsOf(string weaponId)
        {
            return weaponId != null && _attachments.TryGetValue(weaponId, out var attached) ? attached : WeaponAttachments.None;
        }

        public bool SetAttachments(string weaponId, IReadOnlyList<string> attachments, ItemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            var items = Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (!string.Equals(items[i].Spec.Id, weaponId, StringComparison.Ordinal))
                {
                    continue;
                }

                var kept = WeaponAttachments.Keep(catalog, weaponId, attachments);
                if (!items[i].ChangeSpec(WeaponAttachments.Apply(_baseSpecs[i], kept, catalog)))
                {
                    _log.WriteLine("[Net] attachments player=" + Text(_playerId) + " weapon=" + weaponId + " attached=" + WeaponAttachments.Describe(kept)
                        + " rejected=NoRoomForAmmo");
                    return false;
                }

                if (kept.Count == 0)
                {
                    _attachments.Remove(weaponId);
                }
                else
                {
                    _attachments[weaponId] = kept;
                }

                var specs = new WeaponSpec[items.Count];
                for (var s = 0; s < specs.Length; s++)
                {
                    specs[s] = items[s].Spec;
                }

                Specs = specs;
                _log.WriteLine("[Net] attachments player=" + Text(_playerId) + " weapon=" + weaponId + " attached=" + WeaponAttachments.Describe(kept)
                    + " magazine=" + Text(items[i].AmmoInMagazine) + "/" + Text(items[i].Spec.MagazineSize));
                return true;
            }

            return false;
        }

        // 武器ごとの予備弾の受け皿 (持ち物の弾薬) を決める。今の武器と、この後に持たせる (Equip) 武器に使う。null で外す
        public void UseAmmoPools(Func<WeaponSpec, IAmmoPool> poolFor)
        {
            _poolFor = poolFor;
            foreach (var weapon in Items)
            {
                weapon.UseAmmoPool(poolFor?.Invoke(weapon.Spec));
            }
        }

        // 予備弾を受け皿から出し入れしている武器と、その最初の予備弾
        public IReadOnlyList<(string WeaponId, int StartingReserveAmmo)> PooledAmmo()
        {
            var list = new List<(string WeaponId, int StartingReserveAmmo)>();
            foreach (var weapon in Items)
            {
                if (weapon.HasAmmoPool)
                {
                    list.Add((weapon.Spec.Id, weapon.StartingReserveAmmo));
                }
            }

            return list;
        }

        public void Refill()
        {
            if (_loadout == null)
            {
                return;
            }

            foreach (var weapon in _loadout.Items)
            {
                weapon.Refill();
            }

            // TrySelect(0) では持ち替えの待ちが始まるので、同じ武器で並びを作り直して待たずに 0 番を構える
            if (_loadout.CurrentIndex != 0 || _loadout.IsSwitching)
            {
                SetLoadout(new Loadout<ServerWeapon>(_loadout.Items, _loadout.SwitchSeconds, HolsterWeapon, DrawSeconds, HolsterSeconds));
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_loadout == null)
            {
                return;
            }

            _loadout.Tick(deltaSeconds);
            _loadout.Current.Tick(deltaSeconds);
        }

        public ShotRejection TryFire()
        {
            if (_loadout == null)
            {
                return ShotRejection.NoWeapon;
            }

            return _loadout.IsSwitching ? ShotRejection.Switching
                : HoldsShield ? ShotRejection.ShieldWeapon
                : _loadout.Current.TryFire();
        }

        public bool TryReload()
        {
            return _loadout != null && !_loadout.IsSwitching && !HoldsShield && _loadout.Current.TryStartReload();
        }

        public bool CancelReload()
        {
            if (_loadout == null || !_loadout.Current.IsReloading)
            {
                return false;
            }

            _loadout.Current.CancelReload();
            return true;
        }

        public bool TryActiveReload()
        {
            if (_loadout == null || _loadout.IsSwitching || HoldsShield || !_loadout.Current.TryActiveReload(out var result))
            {
                return false;
            }

            _log.WriteLine("[Net] active-reload player=" + Text(_playerId) + " " + (result == ActiveReloadResult.Success ? "success" : "fail"));
            return true;
        }

        public ShotRejection AcceptCharge(float requested, out float fraction)
        {
            var charge = CurrentSpec?.Trigger.Charge;
            if (charge == null)
            {
                _chargeFraction = 0f;
                fraction = 0f;
                return ShotRejection.None;
            }

            // バーストの続きの弾は離したときの 1 回の溜めで出るので、測り直さずに最初の弾の溜めを使う。
            // 測り直すと、続きの弾の間隔しか押していなかったことになり、最小に届かない弾として弾いてしまう。
            var now = _now();
            if (_burstRoundsLeft > 0 && now <= _burstUntil)
            {
                _burstRoundsLeft--;
                fraction = Math.Min(_chargeFraction, float.IsFinite(requested) ? Math.Max(0f, requested) : 0f);
                return ShotRejection.None;
            }

            // 弾かれた射撃でも、撃った側は離して溜め直しているので、ここから測り直す。
            _burstRoundsLeft = 0;
            var held = now - _chargeFrom + ChargeArrivalToleranceSeconds;
            _chargeFrom = now;
            fraction = ChargeTrigger.Reachable(charge, float.IsFinite(requested) ? requested : 0f, held);
            if (!ChargeTrigger.Fires(charge, fraction))
            {
                fraction = 0f;
                return ShotRejection.Undercharged;
            }

            _chargeFraction = fraction;
            var burst = CurrentSpec.Trigger.Burst;
            if (burst != null)
            {
                _burstRoundsLeft = burst.RoundsPerBurst - 1;
                _burstUntil = now + (_burstRoundsLeft * (double)burst.RoundIntervalSeconds) + ChargeArrivalToleranceSeconds;
            }

            return ShotRejection.None;
        }

        // Bot は人のように押して溜めないので、溜め切るまで撃たない (オフラインの敵の TriggerDriver.PullAsBot と同じ)。
        // 溜め切る前に撃たせると、最小に届かない弾として弾かれ続け、連射の枠と警告だけを使い続けるため。
        public bool IsChargedForBot()
        {
            var charge = CurrentSpec?.Trigger.Charge;
            return charge == null || ChargeTrigger.Fraction(charge, _now() - _chargeFrom) >= 1f;
        }

        public bool TrySwitchAmmoType()
        {
            if (_loadout == null || _loadout.IsSwitching || HoldsShield || !_loadout.Current.TrySwitchAmmoType())
            {
                return false;
            }

            _log.WriteLine("[Net] ammo-type player=" + Text(_playerId) + " weapon=" + _loadout.Current.Spec.Id + " index=" + Text(AmmoTypeIndex));
            return true;
        }

        public bool TrySwitch(string weaponId)
        {
            if (_loadout == null)
            {
                return false;
            }

            var items = _loadout.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Spec.Id, weaponId, StringComparison.Ordinal))
                {
                    return _loadout.TrySelect(i);
                }
            }

            _log.WriteLine("[Net] ignored switch client=" + Text(_playerId) + " weaponIdLength=" + Text(weaponId?.Length ?? 0) + " not in the loadout");
            return false;
        }

        // Unity の NetPlayerWeapon.BuildState と同じものを載せる。
        public WeaponSync Sync()
        {
            if (_loadout == null)
            {
                return default;
            }

            var weapon = _loadout.Current;
            var spec = weapon.Spec;
            var showsProgress = weapon.HasActiveReload && weapon.IsReloading;
            var windowOpen = showsProgress && !weapon.HasJudgedActiveReload;
            return new WeaponSync
            {
                AmmoInMagazine = weapon.AmmoInMagazine,
                ReserveAmmo = weapon.ReserveAmmo,
                IsReloading = weapon.IsReloading,
                EquippedIndex = (byte)_loadout.CurrentIndex,
                WeaponCount = (byte)_loadout.Count,
                IsSwitching = _loadout.IsSwitching,
                ReloadStartedAt = showsProgress ? _reloadStartedAt : 0d,
                ReloadSeconds = showsProgress ? weapon.ReloadDurationSeconds : 0f,
                ActiveReloadWindowStart = windowOpen ? spec.ActiveReloadWindowStart : 0f,
                ActiveReloadWindowEnd = windowOpen ? spec.ActiveReloadWindowStart + spec.ActiveReloadWindowLength : 0f,
                Heat = weapon.HeatFraction ?? 0f,
                IsOverheated = weapon.IsOverheated,
                ChargeFraction = spec.Trigger.Charge != null ? _chargeFraction : 0f,
                AmmoTypeIndex = AmmoTypeIndex,
            };
        }

        public void FillIds(ContentIdList ids)
        {
            ArgumentNullException.ThrowIfNull(ids);
            ids.Clear();
            foreach (var weapon in Items)
            {
                ids.Add(weapon.Spec.Id);
            }
        }

        private void SetLoadout(Loadout<ServerWeapon> loadout)
        {
            Unsubscribe();
            _loadout = loadout;
            var specs = new WeaponSpec[loadout.Items.Count];
            for (var i = 0; i < specs.Length; i++)
            {
                specs[i] = loadout.Items[i].Spec;
            }

            Specs = specs;
            _loadout.Switched += HandleSwitched;
            foreach (var weapon in _loadout.Items)
            {
                weapon.ReloadStarted += HandleReloadStarted;
                weapon.Reloaded += HandleReloaded;
            }
        }

        private void Unsubscribe()
        {
            if (_loadout == null)
            {
                return;
            }

            _loadout.Switched -= HandleSwitched;
            foreach (var weapon in _loadout.Items)
            {
                weapon.ReloadStarted -= HandleReloadStarted;
                weapon.Reloaded -= HandleReloaded;
            }
        }

        private static float? DrawSeconds(ServerWeapon weapon) => weapon.Spec.DrawSeconds;

        private static float HolsterSeconds(ServerWeapon weapon) => weapon.Spec.HolsterSeconds;

        private static void HolsterWeapon(ServerWeapon weapon)
        {
            weapon.CancelReload();
        }

        private void HandleSwitched(int index)
        {
            _chargeFraction = 0f;
            _chargeFrom = _now();
            _burstRoundsLeft = 0;
            _log.WriteLine("[Net] switch client=" + Text(_playerId) + " index=" + Text(index) + " weapon=" + _loadout.Items[index].Spec.Id);
        }

        // しまった武器のリロードは HolsterWeapon でやめ、Tick も今の武器だけなので、リロードが起きるのは今の武器だけ
        private void HandleReloadStarted(bool automatic)
        {
            _reloadStartedAt = _now();
            _burstRoundsLeft = 0;
            var weapon = _loadout.Current;
            _log.WriteLine("[Net] reload start client=" + Text(_playerId) + " auto=" + automatic + " ammo=" + Text(weapon.AmmoInMagazine)
                + " reserve=" + Text(weapon.ReserveAmmo));
        }

        private void HandleReloaded()
        {
            var weapon = _loadout.Current;
            _log.WriteLine("[Net] reloaded client=" + Text(_playerId) + " ammo=" + Text(weapon.AmmoInMagazine) + " reserve=" + Text(weapon.ReserveAmmo));
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
