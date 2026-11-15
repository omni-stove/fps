using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// 1 人ぶんの持たせている武器の並び (Unity の NetPlayerWeapon のサーバー側)。弾倉・予備弾・リロード・アクティブリロード・持ち替えを
    /// Core の <see cref="ServerWeapon"/> と <see cref="Loadout{T}"/> で進め、持ち主に送る <see cref="WeaponSync"/> を作る。
    /// 弾薬の種類は武器ごとの <see cref="AmmoTypeSelector"/> で選び、撃つ弾の値 (<see cref="CurrentRound"/>・<see cref="CurrentImpact"/>) はその種類で上書きする。
    /// </summary>
    internal sealed class ServerPlayerWeapons
    {
        // 押していた時間は届いた時刻の差で測るので、前の射撃だけ遅れて届くと短く見える。その揺れで正しく溜めた弾を削らないよう、この秒数だけ長く見る。
        public const double ChargeArrivalToleranceSeconds = 0.2d;

        private readonly int _playerId;
        private readonly TextWriter _log;
        private readonly Func<double> _now;
        private Loadout<ServerWeapon> _loadout;
        private AmmoTypeSelector[] _ammoTypes = Array.Empty<AmmoTypeSelector>();
        private double _reloadStartedAt;
        private float _chargeFraction;
        private double _chargeFrom;
        private int _burstRoundsLeft;

        public ServerPlayerWeapons(int playerId, TextWriter log, Func<double> now)
        {
            _playerId = playerId;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _now = now ?? throw new ArgumentNullException(nameof(now));
        }

        public bool IsEquipped => _loadout != null;

        /// <summary>今持っている武器 (持ち替え中は持ち替え先)。装備前は null。</summary>
        public WeaponSpec CurrentSpec => _loadout?.Current.Spec;

        public IReadOnlyList<ServerWeapon> Items => _loadout?.Items ?? Array.Empty<ServerWeapon>();

        /// <summary>今の武器で撃つ弾の値 (選んだ弾薬の種類で上書きした後)。装備前は null。</summary>
        public RoundSpec CurrentRound => _loadout == null ? null : CurrentAmmoType.Round;

        /// <summary>今の武器の着弾の値 (選んだ弾薬の種類で上書きした後)。装備前は null。</summary>
        public ImpactSpec CurrentImpact => _loadout == null ? null : CurrentAmmoType.Impact;

        /// <summary>
        /// 今の武器の弾薬の種類を <see cref="WeaponSync.AmmoTypeIndex"/> の値にしたもの。0 は種類を選んでいない (武器そのものの弾)、
        /// 1 からは <see cref="WeaponSpec.AmmoTypes"/> の番号 + 1。
        /// </summary>
        public byte AmmoTypeIndex => _loadout == null ? (byte)0 : (byte)(CurrentAmmoType.Index + 1);

        private AmmoTypeSelector CurrentAmmoType => _ammoTypes[_loadout.CurrentIndex];

        /// <summary>specs をこの並びで満タンで持たせ、0 番を構えた状態にする。前の武器 (リロード中・持ち替え中を含む) は捨てる。</summary>
        public void Equip(IReadOnlyList<WeaponSpec> specs, float switchSeconds, GameRules rules)
        {
            ArgumentNullException.ThrowIfNull(specs);
            ArgumentNullException.ThrowIfNull(rules);
            if (specs.Count == 0 || specs.Count > OwnerSnapshot.MaxWeapons)
            {
                throw new ArgumentException("specs must hold 1-" + OwnerSnapshot.MaxWeapons + " weapons", nameof(specs));
            }

            var weapons = new ServerWeapon[specs.Count];
            var ammoTypes = new AmmoTypeSelector[specs.Count];
            for (var i = 0; i < specs.Count; i++)
            {
                weapons[i] = new ServerWeapon(specs[i] ?? throw new ArgumentException("specs must not contain null", nameof(specs)), rules);
                ammoTypes[i] = new AmmoTypeSelector(specs[i]);
            }

            _ammoTypes = ammoTypes;
            _chargeFraction = 0f;
            _chargeFrom = _now();
            SetLoadout(new Loadout<ServerWeapon>(weapons, switchSeconds, HolsterWeapon));
        }

        /// <summary>全武器を満タンに戻してリロードをやめ、持ち替え中でも 0 番を構えた状態に戻す。試合の開始時に呼ぶ。</summary>
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
                SetLoadout(new Loadout<ServerWeapon>(_loadout.Items, _loadout.SwitchSeconds, HolsterWeapon));
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

        /// <summary>今の武器で 1 発ぶん消費する。弾倉が空なら (予備弾があれば) リロードを始めて <see cref="ShotRejection.OutOfAmmo"/>。</summary>
        public ShotRejection TryFire()
        {
            if (_loadout == null)
            {
                return ShotRejection.NoWeapon;
            }

            return _loadout.IsSwitching ? ShotRejection.Switching : _loadout.Current.TryFire();
        }

        public bool TryReload()
        {
            return _loadout != null && !_loadout.IsSwitching && _loadout.Current.TryStartReload();
        }

        public bool TryActiveReload()
        {
            if (_loadout == null || _loadout.IsSwitching || !_loadout.Current.TryActiveReload(out var result))
            {
                return false;
            }

            _log.WriteLine("[Net] active-reload player=" + Text(_playerId) + " " + (result == ActiveReloadResult.Success ? "success" : "fail"));
            return true;
        }

        /// <summary>
        /// 射撃の要求の溜め requested を今の武器に照らして受け入れ、着弾の倍率に使う値を fraction に返す。溜めの無い武器は 0。
        /// 溜めは前に溜めを使い切った時 (前の射撃・持ち替え・装備) からの秒数で届く分までに下げ、下げた値が最小に届かず、
        /// 届かない弾を撃たない武器なら <see cref="ShotRejection.Undercharged"/>。受け入れた値は持ち主に <see cref="WeaponSync.ChargeFraction"/> で返す。
        /// </summary>
        public ShotRejection AcceptCharge(float requested, out float fraction)
        {
            return AcceptCharge(requested, TriggerEdge.Released, true, out fraction);
        }

        /// <inheritdoc cref="AcceptCharge(float, out float)"/>
        public ShotRejection AcceptCharge(float requested, TriggerEdge trigger, bool measured, out float fraction)
        {
            var spec = CurrentSpec?.Trigger;
            var charge = spec?.Charge;
            if (charge == null)
            {
                _chargeFraction = 0f;
                fraction = 0f;
                return ShotRejection.None;
            }

            var now = _now();
            // バーストの 2 発目以降は離したときの溜めで撃つ。押していた時間は 1 発目で使い切っており、測り直すと必ず短く見えるため。
            // 引き継ぐのは 1 発目で受け入れたバーストの残りの発数まで。時間の窓で見ると、細工したクライアントが満タンの溜めを使い回せるため。
            if (spec.Burst != null && trigger == TriggerEdge.None && _burstRoundsLeft > 0)
            {
                fraction = _chargeFraction;
                _burstRoundsLeft--;
                return ShotRejection.None;
            }

            if (!measured)
            {
                // Bot は溜めを持たず、溜めた時間を測れないので申告をそのまま認める。
                fraction = requested;
                _chargeFraction = fraction;
                return ShotRejection.None;
            }

            // 弾かれた射撃でも、撃った側は離して溜め直しているので、ここから測り直す。
            var held = now - _chargeFrom + ChargeArrivalToleranceSeconds;
            _chargeFrom = now;
            _burstRoundsLeft = 0;
            fraction = ChargeTrigger.Reachable(charge, float.IsFinite(requested) ? requested : 0f, held);
            if (!ChargeTrigger.Fires(charge, fraction))
            {
                fraction = 0f;
                return ShotRejection.Undercharged;
            }

            _chargeFraction = fraction;
            if (spec.Burst != null)
            {
                _burstRoundsLeft = spec.Burst.RoundsPerBurst - 1;
            }

            return ShotRejection.None;
        }

        /// <summary>今の武器の弾薬の種類を次へ切り替える。持ち替え中・装備前・切り替えられない武器なら false。</summary>
        public bool TrySwitchAmmoType()
        {
            if (_loadout == null || _loadout.IsSwitching || !CurrentAmmoType.TrySwitch())
            {
                return false;
            }

            _log.WriteLine("[Net] ammo-type player=" + Text(_playerId) + " weapon=" + _loadout.Current.Spec.Id + " index=" + Text(AmmoTypeIndex));
            return true;
        }

        /// <summary>持っている weaponId (大文字・小文字は区別する) へ持ち替える。並びに無ければ警告を出して false。</summary>
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

        /// <summary>
        /// 持ち主に送る今の武器の状態。アクティブリロードのルールが On でリロード中なら、開始時刻と長さを、まだ判定していなければ成功の幅も載せる
        /// (Unity の NetPlayerWeapon.BuildState と同じ)。
        /// </summary>
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
            if (_loadout != null)
            {
                _loadout.Switched -= HandleSwitched;
                foreach (var weapon in _loadout.Items)
                {
                    weapon.ReloadStarted -= HandleReloadStarted;
                    weapon.Reloaded -= HandleReloaded;
                }
            }

            _loadout = loadout;
            _loadout.Switched += HandleSwitched;
            foreach (var weapon in _loadout.Items)
            {
                weapon.ReloadStarted += HandleReloadStarted;
                weapon.Reloaded += HandleReloaded;
            }
        }

        private static void HolsterWeapon(ServerWeapon weapon)
        {
            weapon.CancelReload();
        }

        private void HandleSwitched(int index)
        {
            _chargeFraction = 0f;
            _chargeFrom = _now();
            _log.WriteLine("[Net] switch client=" + Text(_playerId) + " index=" + Text(index) + " weapon=" + _loadout.Items[index].Spec.Id);
        }

        // しまった武器のリロードは HolsterWeapon でやめ、Tick も今の武器だけなので、リロードが起きるのは今の武器だけ
        private void HandleReloadStarted(bool automatic)
        {
            _reloadStartedAt = _now();
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
