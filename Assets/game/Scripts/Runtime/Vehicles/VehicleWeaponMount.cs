using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 撃つのはプレイヤーと同じ撃ち方 (TriggerDriver・Weapon) と弾の届け方 (ShotDeliveries) で、撃った者は座っている人にする。
    // 減衰・部位・置いた物での止まり方・味方撃ち・倒したときの帰属がプレイヤーの射撃と同じ規則を通るように。
    // 時間は座っている人の入力 (OnSeatInput) で進め、入力が来なかったフレームだけ LateUpdate で進める。どちらが先に走っても 1 フレームに 2 度進めないため。
    [DisallowMultipleComponent]
    public sealed class VehicleWeaponMount : MonoBehaviour, IVehicleInputSink
    {
        public const string LogTag = VehicleLog.Tag;

        [SerializeField, Tooltip("付ける武器。空ならコードで SetWeapon する")]
        private WeaponDefinition _weapon;

        [SerializeField, Min(0), Tooltip("この武器を操作する座席の番号")]
        private int _seatIndex;

        [SerializeField, Range(-180f, 180f), Tooltip("左に向けられる端 (度、乗り物の正面から)")]
        private float _yawMinDegrees = VehicleWeaponSpec.DefaultYawMinDegrees;

        [SerializeField, Range(-180f, 180f), Tooltip("右に向けられる端 (度、乗り物の正面から)")]
        private float _yawMaxDegrees = VehicleWeaponSpec.DefaultYawMaxDegrees;

        [SerializeField, Range(-90f, 90f), Tooltip("下に向けられる端 (度)")]
        private float _pitchMinDegrees = VehicleWeaponSpec.DefaultPitchMinDegrees;

        [SerializeField, Range(-90f, 90f), Tooltip("上に向けられる端 (度)")]
        private float _pitchMaxDegrees = VehicleWeaponSpec.DefaultPitchMaxDegrees;

        [SerializeField, Min(0.01f), Tooltip("向きを変える速さ (度/秒)")]
        private float _turnSpeedDegreesPerSecond = VehicleWeaponSpec.DefaultTurnSpeedDegreesPerSecond;

        [SerializeField, Tooltip("弾の出る点。空ならこの Transform から撃つ")]
        private Transform _muzzle;

        private readonly TriggerDriver _trigger = new TriggerDriver();
        private readonly PelletHits<Damageable> _pelletHits = new PelletHits<Damageable>();
        private WeaponSpec _weaponSpec;
        private VehicleWeaponSpec _spec;
        private VehicleWeaponAim _aim;
        private Weapon _armed;
        private bool _wasHeld;
        private int _shotIndex;
        private int _inputFrame = -1;
        private bool _overheated;

        public event Action<VehicleWeaponMount> Fired;

        public Vehicle Vehicle { get; private set; }

        public int Index { get; private set; } = -1;

        public bool HasWeapon => WeaponSpec != null;

        public WeaponSpec WeaponSpec => _weaponSpec ??= _weapon != null ? _weapon.ToSpec() : null;

        public WeaponDefinition Definition => _weapon;

        public VehicleWeaponSpec Spec => _spec ??= BuildSpec();

        public Weapon Weapon => _armed ??= HasWeapon ? new Weapon(WeaponSpec) : null;

        public VehicleWeaponAim Aim => _aim ??= new VehicleWeaponAim(Spec);

        public Transform Muzzle => _muzzle != null ? _muzzle : transform;

        public int SeatIndex => Spec.SeatIndex;

        public Vector3 AimDirection
        {
            get
            {
                var local = Aim.LocalDirection.ToUnity();
                return Vehicle != null ? Vehicle.transform.rotation * local : transform.parent != null ? transform.parent.rotation * local : local;
            }
        }

        private void OnValidate()
        {
            _weaponSpec = null;
            _armed = null;
            _overheated = false;
            _spec = null;
            _aim = null;
        }

        private void LateUpdate()
        {
            if (_inputFrame != Time.frameCount)
            {
                Advance(Time.deltaTime);
                _trigger.Drop();
                _wasHeld = false;
            }
        }

        public void SetWeapon(WeaponSpec weapon)
        {
            _weaponSpec = weapon ?? throw new ArgumentNullException(nameof(weapon));
            _armed = null;
            _overheated = false;
            _spec = null;
            _aim = null;
        }

        public void SetWeapon(WeaponDefinition definition)
        {
            _weapon = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            _weaponSpec = null;
            _armed = null;
            _overheated = false;
            _spec = null;
            _aim = null;
        }

        public void SetMuzzle(Transform muzzle)
        {
            _muzzle = muzzle;
        }

        // 武器の Id が違う設定を受けると、どの武器の値で撃てばよいか分からないので、武器の値ごと受け取る形 (Vehicle.Configure(VehicleSpec, WeaponCatalog)) に任せる。
        public void ApplySpec(VehicleWeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!HasWeapon || !string.Equals(spec.WeaponId, WeaponSpec.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"'{name}' carries weapon '{(HasWeapon ? WeaponSpec.Id : "-")}' but the spec names '{spec.WeaponId}'", nameof(spec));
            }

            Apply(spec);
        }

        public void ApplySpec(VehicleWeaponSpec spec, WeaponSpec weapon)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            if (!string.Equals(spec.WeaponId, weapon.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException($"weapon '{weapon.Id}' does not match the spec's '{spec.WeaponId}'", nameof(weapon));
            }

            if (!ReferenceEquals(weapon, _weaponSpec))
            {
                _weaponSpec = weapon;
                _armed = null;
                _overheated = false;
            }

            Apply(spec);
        }

        public void OnSeatInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            if (!Operates(seat))
            {
                return;
            }

            _inputFrame = Time.frameCount;
            Advance(deltaSeconds);
            if (input.HasAim)
            {
                Aim.Step((Quaternion.Inverse(Vehicle.transform.rotation) * input.AimDirection).ToNumerics(), deltaSeconds);
            }

            ApplyPose();
            var weapon = Weapon;
            if (weapon == null)
            {
                return;
            }

            if (input.ReloadPressed)
            {
                weapon.TryStartReload();
            }

            var pressed = input.FireHeld && !_wasHeld;
            _wasHeld = input.FireHeld;
            Pull(seat.Occupant, input.FireHeld, pressed);
        }

        public void OnSeatVacated(VehicleSeat seat)
        {
            if (!Operates(seat))
            {
                return;
            }

            _trigger.Drop();
            _wasHeld = false;
        }

        public void Advance(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var weapon = Weapon;
            weapon?.Tick(deltaSeconds);
            _trigger.Bind(weapon?.Trigger);
            _trigger.Advance(deltaSeconds);
            NoteHeat(weapon);
        }

        public void ApplyPose()
        {
            var parent = Vehicle != null ? Vehicle.transform : transform.parent;
            var basis = parent != null ? parent.rotation : Quaternion.identity;
            transform.rotation = basis * Quaternion.LookRotation(Aim.LocalDirection.ToUnity(), Vector3.up);
        }

        internal void AttachTo(Vehicle vehicle, int index)
        {
            Vehicle = vehicle;
            Index = index;
        }

        internal void Detach()
        {
            Vehicle = null;
            Index = -1;
        }

        private bool Operates(VehicleSeat seat)
        {
            return seat != null && Vehicle != null && seat.Vehicle == Vehicle && HasWeapon && seat.Index == Spec.SeatIndex;
        }

        private void Pull(IVehicleRider rider, bool held, bool pressed)
        {
            var weapon = _armed;
            if (!weapon.Ammo.CanFire)
            {
                _trigger.Drop();
                if (pressed && WeaponController.IsDryFire(weapon))
                {
                    weapon.TryStartReload();
                }

                return;
            }

            var shots = _trigger.Pull(weapon.Spec.FireMode, held, pressed);
            for (var i = 0; i < shots && weapon.TryFire(); i++)
            {
                FireRound(rider, weapon);
            }

            NoteHeat(weapon);
        }

        // 熱が上限に届いた・冷めきった切り替わりだけを出す。毎フレーム出すと、撃てない間のログで発射のログが読めなくなる。
        private void NoteHeat(Weapon weapon)
        {
            if (!(weapon?.Ammo is HeatAmmo ammo) || ammo.IsOverheated == _overheated)
            {
                return;
            }

            _overheated = ammo.IsOverheated;
            var vehicle = Vehicle != null ? Vehicle.name : name;
            var seat = Spec.SeatIndex;
            Debug.Log(
                _overheated
                    ? VehicleLog.Overheated(vehicle, seat, weapon.Spec.Id, ammo.Heat.OverheatRemainingSeconds)
                    : VehicleLog.Cooled(vehicle, seat, weapon.Spec.Id),
                this);
        }

        private void FireRound(IVehicleRider rider, Weapon weapon)
        {
            var origin = Muzzle.position;
            var direction = AimDirection;
            var round = weapon.AmmoTypes.Round;
            var cone = SpreadPattern.ConeDegrees(weapon.Spec, 0f, 0f);
            var plan = ShotPlanner.Plan(round, origin.ToNumerics(), direction.ToNumerics(), cone, Index, _shotIndex);
            _shotIndex = unchecked(_shotIndex + 1);

            var riderRoot = rider != null ? rider.RiderRoot : null;
            var shooter = riderRoot != null ? riderRoot.gameObject : Vehicle.gameObject;
            SoundStimuli.Emit(shooter, new SoundStimulus(SoundCueKind.Gunshot, origin.ToNumerics(), weapon.Spec.ShotNoiseRadius, false));

            // オフラインで PlayerVehicleRider を持つのは手元のプレイヤーだけなので、その人が撃ったときだけヒットマーカーを出す。
            var confirms = rider is PlayerVehicleRider;
            var request = new ShotRequest(
                shooter,
                Vehicle.transform,
                origin,
                round,
                weapon.AmmoTypes.Impact,
                weapon.Spec.Id,
                Physics.DefaultRaycastLayers,
                weapon.Spec.Trigger.Charge,
                Mathf.Clamp01(_trigger.ShotChargeFraction),
                _weapon != null ? _weapon.Projectile : null,
                confirms ? (Action<ShotOutcome>)ConfirmLanded : null);
            var delivery = ShotDeliveries.For(round.Delivery);
            _pelletHits.Clear();
            string hit = null;
            var damage = 0;
            foreach (var planned in plan.Rounds)
            {
                var outcome = delivery.Deliver(request, planned);
                if (outcome.Hit && hit == null)
                {
                    hit = outcome.Owner != null ? outcome.Owner.Id : outcome.RaycastHit.collider != null ? outcome.RaycastHit.collider.name : null;
                }

                if (outcome.Hitbox != null && outcome.Owner != null && outcome.WasAlive)
                {
                    damage += outcome.Damage;
                    _pelletHits.Add(
                        outcome.Owner,
                        outcome.Damage,
                        outcome.Hitbox.Zone,
                        outcome.Killed ? DamageOutcome.Killed : DamageOutcome.Damaged,
                        outcome.RaycastHit.point.ToNumerics());
                }
            }

            Debug.Log(
                VehicleLog.WeaponFired(
                    Vehicle.name,
                    rider != null ? rider.RiderName : "-",
                    Spec.SeatIndex,
                    weapon.Spec.Id,
                    Aim.YawDegrees,
                    Aim.PitchDegrees,
                    hit,
                    damage,
                    weapon.AmmoInMagazine,
                    weapon.Ammo.HeatFraction),
                this);

            // ペレットごとに出すと 1 回の射撃でヒットマーカーが何度も出るので、相手ごとにまとめてから出す。
            if (confirms)
            {
                for (var i = 0; i < _pelletHits.Count; i++)
                {
                    CombatEvents.RaiseHitConfirmed(_pelletHits[i].ToHitConfirm());
                }
            }

            _pelletHits.Clear();
            Fired?.Invoke(this);
        }

        // 飛ぶ弾は撃った後のフレームで着くので、射撃ごとにまとめる先が無く、着いた弾ごとに知らせる。
        private static void ConfirmLanded(ShotOutcome outcome)
        {
            if (outcome.Hitbox == null || outcome.Owner == null || !outcome.WasAlive)
            {
                return;
            }

            CombatEvents.RaiseHitConfirmed(
                new HitConfirm(outcome.Damage, outcome.Hitbox.Zone, outcome.Killed, downed: false, outcome.RaycastHit.point.ToNumerics()));
        }

        private void Apply(VehicleWeaponSpec spec)
        {
            _spec = spec;
            _seatIndex = spec.SeatIndex;
            _yawMinDegrees = spec.YawMinDegrees;
            _yawMaxDegrees = spec.YawMaxDegrees;
            _pitchMinDegrees = spec.PitchMinDegrees;
            _pitchMaxDegrees = spec.PitchMaxDegrees;
            _turnSpeedDegreesPerSecond = spec.TurnSpeedDegreesPerSecond;
            _aim = null;
        }

        private VehicleWeaponSpec BuildSpec()
        {
            if (!HasWeapon)
            {
                throw new InvalidOperationException($"'{name}' has no weapon; set a WeaponDefinition or call SetWeapon");
            }

            return new VehicleWeaponSpec(
                WeaponSpec.Id,
                _seatIndex,
                _yawMinDegrees,
                _yawMaxDegrees,
                _pitchMinDegrees,
                _pitchMaxDegrees,
                _turnSpeedDegreesPerSecond);
        }
    }
}
