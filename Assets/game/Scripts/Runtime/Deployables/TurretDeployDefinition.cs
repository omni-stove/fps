using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [CreateAssetMenu(fileName = "DeployTurret", menuName = "Fps/Abilities/Deploy Turret")]
    public sealed class TurretDeployDefinition : DeployAbilityDefinition
    {
        public const string BaseName = "Base";
        public const string ShellName = "Shell";

        public const float BaseHeightFraction = 0.6f;
        public const float HeadWidthFraction = 0.7f;

        [SerializeField, Tooltip("撃つ武器。ダメージ・射程・減衰・連射の間隔・散らばりをこの武器の値で撃つ")]
        private WeaponDefinition _weapon;

        [SerializeField, Min(1), Tooltip("撃てる弾の数。撃ち尽くすと消える")]
        private int _rounds = TurretSpec.DefaultRounds;

        [SerializeField, Min(0.01f), Tooltip("置いてから消えるまでの秒数")]
        private float _lifetimeSeconds = TurretSpec.DefaultLifetimeSeconds;

        [SerializeField, Min(0.01f), Tooltip("敵を見つける距離 (m)。武器の射程より長くても、撃つのは射程の内だけ")]
        private float _sightRange = TurretSpec.DefaultSightRange;

        [SerializeField, Min(0.01f), Tooltip("頭が回る速さ (度/秒)")]
        private float _turnDegreesPerSecond = TurretSpec.DefaultTurnDegreesPerSecond;

        [SerializeField, Range(0f, 180f), Tooltip("相手の向きとのずれがこれ以下になったら撃つ (度)")]
        private float _aimToleranceDegrees = TurretSpec.DefaultAimToleranceDegrees;

        [SerializeField, Tooltip("置く物のプレハブが空のときに使う台の見た目と当たり判定。足元が原点、前が +Z。空なら仮の箱を作る")]
        private GameObject _basePrefab;

        [SerializeField, Tooltip("置く物のプレハブが空のときに使う、回る本体の見た目と当たり判定。原点が回る軸と撃つ位置 (台の上の真ん中)、前が +Z。空なら仮の箱を作る")]
        private GameObject _headPrefab;

        // 基底の既定は盾の形と数なので、作った時点でタレットの既定に置き換える (アセットに保存された値があればそちらが勝つ)。
        public TurretDeployDefinition()
        {
            MaxActive = TurretSpec.DefaultMaxActive;
            MaxHealth = TurretSpec.DefaultMaxHealth;
            Size = new Vector3(TurretSpec.DefaultWidth, TurretSpec.DefaultHeight, TurretSpec.DefaultDepth);
        }

        public WeaponDefinition Weapon
        {
            get => _weapon;
            set => _weapon = value;
        }

        public int Rounds
        {
            get => _rounds;
            set => _rounds = value;
        }

        public float LifetimeSeconds
        {
            get => _lifetimeSeconds;
            set => _lifetimeSeconds = value;
        }

        public float SightRange
        {
            get => _sightRange;
            set => _sightRange = value;
        }

        public float TurnDegreesPerSecond
        {
            get => _turnDegreesPerSecond;
            set => _turnDegreesPerSecond = value;
        }

        public float AimToleranceDegrees
        {
            get => _aimToleranceDegrees;
            set => _aimToleranceDegrees = value;
        }

        public GameObject BasePrefab
        {
            get => _basePrefab;
            set => _basePrefab = value;
        }

        public GameObject HeadPrefab
        {
            get => _headPrefab;
            set => _headPrefab = value;
        }

        public TurretSpec ToTurretSpec()
        {
            try
            {
                return new TurretSpec(_rounds, _lifetimeSeconds, _sightRange, _turnDegreesPerSecond, _aimToleranceDegrees);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: {e.Message}", e);
            }
        }

        public WeaponSpec ToWeaponSpec()
        {
            if (_weapon == null)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' has no weapon to fire");
            }

            try
            {
                return _weapon.ToSpec();
            }
            catch (WeaponDefinitionException e)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' has an invalid weapon: {e.Message}", e);
            }
        }

        // 武器が無い・不正なことを、置いたときでなく枠に入れたとき (AbilityHost.Equip) に分かるようにする。
        public override IAbility CreateAbility()
        {
            ToTurretSpec();
            ToWeaponSpec();
            return base.CreateAbility();
        }

        protected override GameObject CreatePlaceholder(DeploySpec spec)
        {
            var root = new GameObject(Id);
            var baseHeight = spec.Height * BaseHeightFraction;
            if (_basePrefab != null)
            {
                Instantiate(_basePrefab, root.transform, false).name = BaseName;
            }
            else
            {
                var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stand.name = BaseName;
                stand.transform.SetParent(root.transform, false);
                stand.transform.localPosition = new Vector3(0f, baseHeight / 2f, 0f);
                stand.transform.localScale = new Vector3(spec.Width, baseHeight, spec.Depth);
            }

            // 回す軸 (Head) はプレハブの有無によらずここで作る。Turret は Head の名前で探して回すので、本体のプレハブに名前を揃えさせない。
            // 頭は回るので、台の上の真ん中に置き、回っても足元の大きさ (spec の幅と奥行き) をはみ出さない大きさにする。
            var headHeight = spec.Height - baseHeight;
            var head = new GameObject(Turret.HeadName);
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, baseHeight + (headHeight / 2f), 0f);
            if (_headPrefab != null)
            {
                Instantiate(_headPrefab, head.transform, false).name = ShellName;
            }
            else
            {
                var headSide = Mathf.Min(spec.Width, spec.Depth) * HeadWidthFraction;
                var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shell.name = ShellName;
                shell.transform.SetParent(head.transform, false);
                shell.transform.localScale = new Vector3(headSide, headHeight, headSide);
            }

            return root;
        }

        // 盾と同じく敵には避けて歩かせるが、隠れ場所 (CoverPoint) にはしない。
        protected override void Configure(GameObject placed, DeploySpec spec)
        {
            CarveNavMesh(placed, spec);
            if (placed.GetComponent<Turret>() == null)
            {
                placed.AddComponent<Turret>();
            }
        }

        protected override void OnPlaced(Deployable deployable, AbilityContext context)
        {
            var turret = deployable.GetComponent<Turret>();
            if (turret == null)
            {
                throw new InvalidOperationException($"deploy ability '{Id}' placed '{deployable.name}' without a Turret");
            }

            turret.Arm(ToTurretSpec(), ToWeaponSpec(), context.Host.FriendlyFire);
        }
    }
}
