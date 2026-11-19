using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class EnemyAgent
    {
        private Weapon _weapon;
        private GameRules _weaponRules = GameRules.Default;
        private bool _unarmed;
        private readonly System.Random _random = new System.Random();
        private readonly RecoilState _recoil = new RecoilState();
        private IReadOnlyList<string> _weaponAttachments = Shooter.Core.WeaponAttachments.None;
        private readonly TriggerDriver _trigger = new TriggerDriver();
        private int _spreadSeed;
        private int _shotCount;
        private bool _loggedAim;

        public RecoilState Recoil => _recoil;
        public int ShotCount => _shotCount;

        public event Action<Weapon> Fired;

        public Weapon Weapon => _unarmed ? null : _weapon;

        public bool IsUnarmed => _unarmed;

        public bool Disarm()
        {
            if (_unarmed)
            {
                return false;
            }

            _weapon?.CancelReload();
            _unarmed = true;
            Debug.Log($"{LogTag} id={Id} disarmed", this);
            return true;
        }

        public IReadOnlyList<PickupWeaponState> DescribeWeapons(out string currentWeaponId)
        {
            var weapon = Weapon;
            if (weapon == null)
            {
                currentWeaponId = null;
                return Array.Empty<PickupWeaponState>();
            }

            currentWeaponId = weapon.Spec.Id;
            return new[] { PickupWeaponState.Of(weapon) };
        }

        // bot は 1 本しか持たず、拾えば持ち替える (AddWeapon) ので、拾う判定には上限なしとして見せる。
        public int MaxWeapons => 0;

        public bool AddWeapon(WeaponDefinition definition, WeaponAmmo ammo = null, IReadOnlyList<string> attachments = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var spec = definition.ToSpec();
            var current = Weapon;
            if (current != null && string.Equals(current.Spec.Id, spec.Id, StringComparison.Ordinal))
            {
                return false;
            }

            EquipWeapon(definition, _weaponRules, ammo, attachments);
            return true;
        }

        public bool SwapWeapon(
            string droppedWeaponId, WeaponDefinition definition, WeaponAmmo ammo, IReadOnlyList<string> attachments, out HeldWeapon dropped)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            dropped = default;
            var current = Weapon;
            if (current == null || _weaponDefinition == null || !string.Equals(current.Spec.Id, droppedWeaponId, StringComparison.Ordinal))
            {
                return false;
            }

            var leaving = new HeldWeapon(_weaponDefinition, WeaponAmmo.Of(current), _weaponAttachments);
            if (!AddWeapon(definition, ammo, attachments))
            {
                return false;
            }

            dropped = leaving;
            return true;
        }

        public int AddReserveAmmo(string weaponId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            var weapon = Weapon;
            return weapon != null && string.Equals(weapon.Spec.Id, weaponId, StringComparison.Ordinal) ? weapon.AddReserveAmmo(amount) : 0;
        }

        public WeaponDefinition WeaponDefinition
        {
            get => _weaponDefinition;
            set => _weaponDefinition = value;
        }

        public IReadOnlyList<string> HeldAttachments => _weaponAttachments;

        public GameRules WeaponRules => _weaponRules;

        public void EquipWeapon(WeaponDefinition definition, GameRules rules = null, WeaponAmmo ammo = null, IReadOnlyList<string> attachments = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var spec = definition.ToSpec();
            var kept = WeaponAttachments.Keep(KitTuning.Items, spec.Id, attachments);
            spec = WeaponAttachments.Apply(spec, kept, KitTuning.Items);
            var weaponRules = rules ?? _weaponRules;
            _weaponDefinition = definition;
            _weaponAttachments = kept;
            _weaponRules = weaponRules;
            _unarmed = false;
            if (_brain == null)
            {
                return;
            }

            if (_weapon != null)
            {
                _weapon.Reloaded -= OnReloaded;
            }

            _weapon = ammo != null
                ? new Weapon(spec, Math.Min(ammo.AmmoInMagazine, spec.MagazineSize), ammo.ReserveAmmo, weaponRules)
                : new Weapon(spec, weaponRules);
            _weapon.Reloaded += OnReloaded;
            _recoil.Reset();
            Debug.Log($"{LogTag} id={Id} equip weapon={spec.LogName} ammo={_weapon.AmmoInMagazine} attachments={WeaponAttachments.Describe(kept)}", this);
        }

        public static Vector3 WithRecoil(Vector3 direction, RecoilState recoil, float control)
        {
            if (recoil == null)
            {
                throw new ArgumentNullException(nameof(recoil));
            }

            var keep = 1f - Mathf.Clamp01(control);
            var pitch = recoil.PitchUpDegrees * keep;
            var yaw = recoil.YawDegrees * keep;
            if (pitch == 0f && yaw == 0f)
            {
                return direction;
            }

            var flat = new Vector3(direction.x, 0f, direction.z);
            var right = flat.sqrMagnitude > 1e-8f ? Vector3.Cross(Vector3.up, flat).normalized : Vector3.right;
            return Quaternion.AngleAxis(yaw, Vector3.up) * (Quaternion.AngleAxis(-pitch, right) * direction);
        }

        // プレイヤーと同じく、撃ち合いの合間 (戦っていないときと遮蔽点に隠れている間) に減った弾倉を詰め直す。
        private void TopUpMagazine()
        {
            if (_unarmed || _weapon.IsReloading || _weapon.AmmoInMagazine >= _weapon.Spec.MagazineSize)
            {
                return;
            }

            var state = _brain.State;
            var lull = state == EnemyState.Idle || state == EnemyState.Patrol || state == EnemyState.Search
                || (state == EnemyState.TakeCover && _brain.CoverPhase == EnemyCoverPhase.Hiding);
            if (lull && _weapon.TryStartReload())
            {
                Debug.Log($"{LogTag} id={Id} reload", this);
            }
        }

        // 敵も撃ち方 (Weapon.Trigger) を通して撃つ。溜める撃ち方は溜め切ってから離し、Auto は撃つ間押し続ける (TriggerDriver.PullAsBot)。
        private void Shoot()
        {
            var target = PerceivedTarget;
            _trigger.Bind(_weapon.Trigger);
            if (target == null || target.IsDead)
            {
                _trigger.Drop();
                return;
            }

            if (!_weapon.Ammo.CanFire)
            {
                _trigger.Drop();
                ReloadIfEmpty();
                return;
            }

            var shots = _trigger.PullAsBot(_weapon.Spec);
            var charge = _trigger.ShotChargeFraction;
            for (var i = 0; i < shots; i++)
            {
                if (!_weapon.TryFire())
                {
                    ReloadIfEmpty();
                    return;
                }

                FireRound(target, charge);
            }
        }

        public bool IsFacing(System.Numerics.Vector3? lookAt)
        {
            if (!lookAt.HasValue || _brain == null)
            {
                return true;
            }

            var tolerance = _brain.Spec.FireToleranceDegrees;
            if (tolerance >= EnemySpec.MaxFireToleranceDegrees)
            {
                return true;
            }

            var direction = lookAt.Value.ToUnity() - transform.position;
            direction.y = 0f;
            var forward = transform.forward;
            forward.y = 0f;
            if (direction.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f)
            {
                return true;
            }

            return Vector3.Angle(forward, direction) <= tolerance;
        }

        private void LogAimStart()
        {
            if (!_aiming)
            {
                _loggedAim = false;
                return;
            }

            if (_loggedAim)
            {
                return;
            }

            _loggedAim = true;
            var target = PerceivedTarget;
            var distance = target != null ? Vector3.Distance(EyePosition, target.AimPoint) : 0f;
            Debug.Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} id={1} aim state={2} target={3} distance={4:F1} wait={5:F2}",
                    LogTag,
                    Id,
                    _brain.State,
                    target != null ? target.Id : "none",
                    distance,
                    _brain.AimRemainingSeconds),
                this);
        }

        // 撃ち合いをやめた間は引き金を離す。押したままにすると、次に撃ち始めた最初の弾が押しっぱなしの撃ち方の間隔ぶん遅れる。
        private void ReleaseTrigger()
        {
            _trigger.Bind(_weapon?.Trigger);
            _trigger.Drop();
        }

        private void ReloadIfEmpty()
        {
            if (_weapon.AmmoInMagazine == 0 && _weapon.TryStartReload())
            {
                Debug.Log($"{LogTag} id={Id} reload", this);
            }
        }

        private void FireRound(Damageable target, float chargeFraction)
        {
            var eye = EyePosition;
            var direction = target.AimPoint - eye;
            if (direction.sqrMagnitude < 1e-6f)
            {
                direction = transform.forward;
            }

            // プレイヤー (WeaponController.Fire) と同じ順: 武器のばらつき → たまった反動 → この弾の反動を次の弾へ足す。
            // 判断の散らばり (撃ち始めは外しやすい) はその上に重ねる。
            var spec = _weapon.Spec;
            var shotIndex = _shotCount;
            _shotCount = unchecked(_shotCount + 1);
            var cone = SpreadPattern.ConeDegrees(spec, _aiming ? 1f : 0f, MoveFraction());
            var round = _weapon.AmmoTypes.Round;
            var plan = ShotPlanner.Plan(round, eye.ToNumerics(), direction.ToNumerics(), cone, _spreadSeed, shotIndex);
            var directions = new Vector3[plan.Rounds.Count];
            for (var i = 0; i < directions.Length; i++)
            {
                directions[i] = WithRecoil(plan.Rounds[i].Direction.ToUnity(), _recoil, _brain.Spec.RecoilControl);
            }

            _recoil.Kick(spec, _spreadSeed, shotIndex);

            var spread = Mathf.Min(
                EnemySpec.MaxSpreadDegrees, _brain.NextShotSpreadDegrees + _brain.Spec.RangeSpreadDegreesAt(Vector3.Distance(eye, target.AimPoint)));
            for (var i = 0; i < directions.Length; i++)
            {
                directions[i] = BlindFireShot.Spread(directions[i].ToNumerics(), spread, _random).ToUnity();
            }

            _brain.RecordShot();
            Fired?.Invoke(_weapon);

            var impact = ScaleImpact(_weapon.AmmoTypes.Impact, _shotDamageMultiplier);
            var request = new ShotRequest(
                gameObject,
                transform,
                transform.position,
                round,
                impact,
                spec.Id,
                Physics.DefaultRaycastLayers,
                spec.Trigger.Charge,
                Mathf.Clamp01(chargeFraction),
                _weaponDefinition != null ? _weaponDefinition.Projectile : null);
            var hits = new string[directions.Length];
            var damage = 0;
            for (var i = 0; i < directions.Length; i++)
            {
                var result = Hitscan.Fire(request, new PlannedRound(eye.ToNumerics(), directions[i].ToNumerics(), plan.Rounds[i].Index));
                hits[i] = DescribeHit(result);
                damage += result.Damage;
            }

            SoundStimuli.Emit(gameObject, new SoundStimulus(SoundCueKind.Gunshot, eye.ToNumerics(), spec.ShotNoiseRadius, false));
            Debug.Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} id={1} fire ammo={2} spread={3:F1} cone={4:F1} hit={5} dmg={6}",
                    LogTag, Id, _weapon.AmmoInMagazine, spread, cone, string.Join(",", hits), damage),
                this);
        }

        // 倍率が 1 の普通の敵のために毎発 ImpactSpec を作り直さない。
        private static ImpactSpec ScaleImpact(ImpactSpec impact, float multiplier)
        {
            if (multiplier == 1f)
            {
                return impact;
            }

            return new ImpactSpec(
                ScaleDamage(impact.Damage, multiplier),
                impact.Falloff,
                impact.StructureRadius,
                impact.Explosion,
                impact.StatusEffect,
                impact.AreaEffect);
        }

        private float MoveFraction()
        {
            var velocity = _agent.enabled ? _agent.velocity : Vector3.zero;
            return SpreadPattern.MoveFraction(new Vector2(velocity.x, velocity.z).magnitude, _walkSpeed);
        }

        private static string DescribeHit(HitscanResult result)
        {
            if (!result.Hit)
            {
                return "none";
            }

            if (result.Hitbox != null && result.Hitbox.Owner != null)
            {
                return result.Hitbox.Owner.Id;
            }

            return result.RaycastHit.collider.name;
        }

        private void OnReloaded()
        {
            Debug.Log($"{LogTag} id={Id} reloaded ammo={_weapon.AmmoInMagazine}", this);
        }
    }
}
