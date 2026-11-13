using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 遮蔽物に隠れたまま狙わずに撃つブラインドファイアを受け持つ、プレイヤーのコンポーネント。
    /// <see cref="Configure"/> でルールの <see cref="ActionRules.BlindFire"/> が <see cref="BlindFire.On"/> のときだけ有効にし、
    /// 有効な間は <see cref="WeaponController.ShotFilter"/> に自分を差し込む。
    /// カバー中 (<see cref="CoverController.IsInCover"/>) で覗いていない (<see cref="CoverController.IsPeeking"/> でない) ときの射撃を、
    /// 低い壁では壁の上から、高い壁では端の横から、武器の <see cref="WeaponSpec.BlindFireSpreadDegrees"/> で散らして撃つ (<see cref="BlindFireShot"/>)。
    /// 体は出さないので、しゃがみ・位置・撃たれる側の当たり判定は変えない。弾倉の消費と連射の間隔は通常の射撃と同じ。
    /// 高い壁で端にいないときは撃てないので、その間は WeaponController の Attack を止める (弾を消費させない)。
    /// 参照はコードで解決するので Inspector の配線は要らない: <see cref="CoverController"/> と <see cref="ViewRig"/> (無くてもよい) は同じ GameObject、
    /// <see cref="WeaponController"/> は子 (プレイヤーのカメラ)。
    /// </summary>
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

        // 端にいない高い壁で止めている Attack。止めていなければ null。
        private InputAction _blockedAttack;

        public CoverController Cover => _cover;

        public WeaponController Weapon => _weapon;

        public bool IsInitialized => _cover != null && _weapon != null;

        /// <summary>弾の向きを散らす乱数。テストで固定するために差し替える。null は渡せない。</summary>
        public System.Random Random
        {
            get => _random;
            set => _random = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>今 Attack すればブラインドファイアになるか (有効・カバー中・覗いていない)。</summary>
        public bool IsBlindFiring => enabled && IsInitialized && _cover.IsInCover && !_cover.IsPeeking;

        /// <summary>ブラインドファイアになるのに、高い壁で端にいないので撃てないか。この間は Attack を止める。</summary>
        public bool IsFireBlocked => IsBlindFiring && _cover.CoverHeight == CoverHeight.High && EdgeSide() == 0;

        /// <summary>Attack を止めているか。</summary>
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

        /// <summary>
        /// rules の <see cref="ActionRules.BlindFire"/> が <see cref="BlindFire.On"/> なら有効に、それ以外なら無効にし、
        /// <see cref="WeaponController.ShotFilter"/> への差し込みを揃える。
        /// </summary>
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

        /// <summary>
        /// 同じ GameObject の <see cref="CoverController"/> と子の <see cref="WeaponController"/> を探して初期化する。
        /// 見つからなければ理由を Debug.LogError に出して false を返す。
        /// </summary>
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

        /// <summary>
        /// 参照を直接渡して初期化する。cover はこの GameObject のもの、weapon はこの GameObject かその子のもの。
        /// <see cref="ViewRig"/> は同じ GameObject にあれば、両側とも端の柱で出る側 (肩の側) を決めるのに使う。
        /// </summary>
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

        /// <summary>
        /// 高い壁で端にいない (<see cref="IsFireBlocked"/>) 間は WeaponController の Attack を無効にし、そうでなくなったら有効に戻す。
        /// 毎フレーム WeaponController より先に呼ぶ (<see cref="Update"/>)。止めている間に Attack を押し続けていたら、戻った後は押し直すまで撃たない。
        /// </summary>
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

        /// <summary>
        /// ブラインドファイアになるときだけ、origin を壁の上か端の横へ移し、direction を武器の半角で散らして
        /// <c>[Combat] blind-fire spread=..</c> をログに出す。ならないとき (高い壁で端にいないときも) は何も変えない。
        /// </summary>
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
