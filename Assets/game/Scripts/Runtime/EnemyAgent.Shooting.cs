using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed partial class EnemyAgent
    {
        private Weapon _weapon;
        private GameRules _weaponRules = GameRules.Default;
        private bool _unarmed;
        private readonly System.Random _random = new System.Random();
        private readonly RecoilState _recoil = new RecoilState();
        private int _spreadSeed;
        private int _shotCount;

        public RecoilState Recoil => _recoil;
        public int ShotCount => _shotCount;

        public event Action<Weapon> Fired;

        /// <summary>持っている武器。未初期化か武器を持っていなければ (<see cref="IsUnarmed"/>) null。</summary>
        public Weapon Weapon => _unarmed ? null : _weapon;

        /// <summary><see cref="Disarm"/> してから、武器を拾うか <see cref="EquipWeapon"/> するまで true。この間は撃たない。</summary>
        public bool IsUnarmed => _unarmed;

        /// <summary>
        /// 武器を持っていない状態にする。リロード中なら取り消す。武器を拾う (<see cref="AddWeapon"/>) か <see cref="EquipWeapon"/> すると持った状態に戻る。
        /// 未初期化でも呼べる (初期化しても持っていないまま)。もう持っていなければ何もせず false。
        /// </summary>
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

        /// <summary>持っている武器 (1 本) の今の予備弾。武器を持っていないか未初期化なら空。</summary>
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

        /// <summary>
        /// 拾った武器を持つ。bot は 1 本しか持たないので、別の武器を持っていればそれと持ち替える (<see cref="EquipWeapon"/>、今の <see cref="WeaponRules"/> で満タン)。
        /// 同じ Id の武器を持っていれば何もせず false。
        /// </summary>
        public bool AddWeapon(WeaponDefinition definition)
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

            EquipWeapon(definition, _weaponRules);
            return true;
        }

        /// <summary>持っている武器の Id が weaponId なら予備弾を amount 足し、実際に足した数を返す。違う武器か武器を持っていなければ 0。</summary>
        public int AddReserveAmmo(string weaponId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            var weapon = Weapon;
            return weapon != null && string.Equals(weapon.Spec.Id, weaponId, StringComparison.Ordinal) ? weapon.AddReserveAmmo(amount) : 0;
        }

        /// <summary>
        /// 撃つ武器の定義。<see cref="TryInitialize"/> がこれから <see cref="Weapon"/> を作るので、それより前に設定する
        /// (初期化後に変えても今の武器は変わらない。初期化後に持ち替えるなら <see cref="EquipWeapon"/>)。
        /// </summary>
        public WeaponDefinition WeaponDefinition
        {
            get => _weaponDefinition;
            set => _weaponDefinition = value;
        }

        /// <summary>武器を作るときの予備弾のルール。既定は <see cref="GameRules.Default"/>。<see cref="EquipWeapon"/> で変える。</summary>
        public GameRules WeaponRules => _weaponRules;

        /// <summary>
        /// 持つ武器を definition に替え、rules (null なら今の <see cref="WeaponRules"/>) の予備弾で満タンに作り直す。リロード中なら取り消す。
        /// 以後の <see cref="Respawn"/> もこの武器とルールで作り直す。未初期化なら定義とルールを覚えるだけで、<see cref="TryInitialize"/> がそれで作る。
        /// 武器を持っていなければ (<see cref="IsUnarmed"/>)、持った状態に戻す。
        /// 定義が不正なら <see cref="WeaponDefinitionException"/> を投げ、今の武器はそのまま残す。
        /// </summary>
        public void EquipWeapon(WeaponDefinition definition, GameRules rules = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var spec = definition.ToSpec();
            var weaponRules = rules ?? _weaponRules;
            _weaponDefinition = definition;
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

            _weapon = new Weapon(spec, weaponRules);
            _weapon.Reloaded += OnReloaded;
            _recoil.Reset();
            Debug.Log($"{LogTag} id={Id} equip weapon={spec.LogName} ammo={_weapon.AmmoInMagazine}", this);
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

        private void Shoot()
        {
            if (_target == null || _target.IsDead)
            {
                return;
            }

            if (!_weapon.TryFire())
            {
                if (_weapon.AmmoInMagazine == 0 && _weapon.TryStartReload())
                {
                    Debug.Log($"{LogTag} id={Id} reload", this);
                }

                return;
            }

            var eye = EyePosition;
            var direction = _target.AimPoint - eye;
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
            if (cone > 0f)
            {
                direction = SpreadPattern.Direction(direction.ToNumerics(), cone, _spreadSeed, shotIndex).ToUnity();
            }

            direction = WithRecoil(direction, _recoil, RecoilControl);
            _recoil.Kick(spec, _spreadSeed, shotIndex);

            var spread = _brain.NextShotSpreadDegrees;
            direction = BlindFireShot.Spread(direction.ToNumerics(), spread, _random).ToUnity();
            _brain.RecordShot();
            Fired?.Invoke(_weapon);

            var result = Hitscan.Fire(eye, direction, spec.Range, spec.Damage, spec.Falloff, transform, spec.ExplosionRadius, spec.Id);
            Debug.Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} id={1} fire ammo={2} spread={3:F1} cone={4:F1} hit={5} dmg={6}",
                    LogTag, Id, _weapon.AmmoInMagazine, spread, cone, DescribeHit(result), result.Damage),
                this);
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
