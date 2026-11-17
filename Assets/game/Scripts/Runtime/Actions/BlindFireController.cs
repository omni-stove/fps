using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // 高い壁で端にいないときは撃てないので、その間は WeaponController の Attack を止めて弾を消費させない。
    [DisallowMultipleComponent]
    // Attack を止めるのは、WeaponController が同じフレームに Attack を読むより先でないと効かない。
    [DefaultExecutionOrder(-10)]
    public sealed class BlindFireController : MonoBehaviour, IShotFilter
    {
        private const string LogTag = "[BlindFire]";

        private CoverController _cover;
        private WeaponController _weapon;
        private ViewRig _rig;
        private System.Random _random = new System.Random();

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _blockedAttack;

        public CoverController Cover => _cover;

        public WeaponController Weapon => _weapon;

        public bool IsInitialized => _cover != null && _weapon != null;

        // テストで乱数を固定できるよう差し替えられるようにする。
        public System.Random Random
        {
            get => _random;
            set => _random = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool IsBlindFiring => enabled && IsInitialized && _cover.IsInCover && !_cover.IsPeeking;

        public bool IsFireBlocked => IsBlindFiring && _cover.CoverHeight == CoverHeight.High && EdgeSide() == 0;

        public float SpreadDegrees
        {
            get
            {
                var weapon = IsBlindFiring ? _weapon.Weapon : null;
                return weapon != null ? weapon.Spec.BlindFireSpreadDegrees : 0f;
            }
        }

        public bool IsAttackBlocked => _blockedAttack != null;

        private void Awake()
        {
            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            SyncShotFilter();
        }

        private void OnDisable()
        {
            ReleaseAttack();
            SyncShotFilter();
        }

        private void OnDestroy()
        {
            ReleaseAttack();
            if (_weapon != null && ReferenceEquals(_weapon.ShotFilter, this))
            {
                _weapon.ShotFilter = null;
            }
        }

        private void Update()
        {
            RefreshAttackBlock();
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            enabled = rules.Actions.BlindFire == BlindFire.On;

            // Edit Mode では enabled を変えても OnEnable / OnDisable が走らないので、差し込みと Attack の止めはここでも揃える。
            if (!enabled)
            {
                ReleaseAttack();
            }

            SyncShotFilter();
        }

        public bool TryInitializeFromHierarchy()
        {
            var cover = GetComponent<CoverController>();
            if (cover == null)
            {
                Debug.LogError($"{LogTag} '{name}' に CoverController がありません。ブラインドファイアはカバー中にだけ撃てます。", this);
                return false;
            }

            var weapon = GetComponentInChildren<WeaponController>(true);
            if (weapon == null)
            {
                Debug.LogError($"{LogTag} '{name}' の子に WeaponController がありません。ブラインドファイアはその射撃に差し込みます。", this);
                return false;
            }

            Initialize(cover, weapon);
            return true;
        }

        public void Initialize(CoverController cover, WeaponController weapon)
        {
            if (cover == null)
            {
                throw new ArgumentNullException(nameof(cover));
            }

            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            if (cover.gameObject != gameObject)
            {
                throw new ArgumentException($"cover must be on '{name}'", nameof(cover));
            }

            if (!weapon.transform.IsChildOf(transform))
            {
                throw new ArgumentException($"weapon must be on '{name}' or its children", nameof(weapon));
            }

            ReleaseAttack();
            if (_weapon != null && _weapon != weapon && ReferenceEquals(_weapon.ShotFilter, this))
            {
                _weapon.ShotFilter = null;
            }

            _cover = cover;
            _weapon = weapon;
            _rig = GetComponent<ViewRig>();
            SyncShotFilter();
        }

        public void RefreshAttackBlock()
        {
            if (IsFireBlocked)
            {
                BlockAttack();
            }
            else
            {
                ReleaseAttack();
            }
        }

        public void Filter(ref Vector3 origin, ref Vector3 direction)
        {
            if (!IsBlindFiring)
            {
                return;
            }

            var weapon = _weapon.Weapon;
            var body = _cover.Player != null ? _cover.Player.Controller : null;
            if (weapon == null || body == null)
            {
                return;
            }

            if (!BlindFireShot.TryResolveOrigin(
                    _cover.CoverHeight,
                    _cover.LastProbe.Edges,
                    PreferredSide(),
                    _cover.CoverNormal.ToNumerics(),
                    _cover.CoverTopY,
                    BodyRadius(body),
                    origin.ToNumerics(),
                    out var blindOrigin))
            {
                return;
            }

            var spread = weapon.Spec.BlindFireSpreadDegrees;
            origin = blindOrigin.ToUnity();
            direction = BlindFireShot.Spread(direction.ToNumerics(), spread, _random).ToUnity();
            Debug.Log(BlindFireShot.FormatLog(spread), this);
        }

        private int EdgeSide()
        {
            return BlindFireShot.EdgeSide(_cover.LastProbe.Edges, PreferredSide());
        }

        // 両側とも端の柱では、カバーが左端で左肩に替えるのと揃えて、今の肩の側へ出る。
        private float PreferredSide()
        {
            return _rig != null && _rig.Shoulder == ShoulderSide.Left ? -1f : 1f;
        }

        private float BodyRadius(CharacterController body)
        {
            var scale = body.transform.lossyScale;
            return body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        private void BlockAttack()
        {
            if (_blockedAttack != null)
            {
                return;
            }

            var attack = _weapon.AttackAction;
            if (attack == null || !attack.enabled)
            {
                return;
            }

            attack.Disable();
            _blockedAttack = attack;
        }

        private void ReleaseAttack()
        {
            var attack = _blockedAttack;
            if (attack == null)
            {
                return;
            }

            _blockedAttack = null;

            // 止めている間に WeaponController が無効になったか入力を差し替えたなら、Attack を有効にしておく者はもういない。
            if (_weapon != null && _weapon.isActiveAndEnabled && ReferenceEquals(_weapon.AttackAction, attack) && !attack.enabled)
            {
                attack.Enable();
            }
        }

        private void SyncShotFilter()
        {
            if (_weapon == null)
            {
                return;
            }

            if (enabled && IsInitialized)
            {
                if (_weapon.ShotFilter != null && !ReferenceEquals(_weapon.ShotFilter, this))
                {
                    Debug.LogWarning($"{LogTag} '{_weapon.name}' の ShotFilter ({_weapon.ShotFilter.GetType().Name}) をブラインドファイアに差し替えます。", this);
                }

                _weapon.ShotFilter = this;
            }
            else if (ReferenceEquals(_weapon.ShotFilter, this))
            {
                _weapon.ShotFilter = null;
            }
        }
    }
}
