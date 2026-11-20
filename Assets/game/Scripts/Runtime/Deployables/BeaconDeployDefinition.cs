using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "DeployBeacon", menuName = "Shooter/Abilities/Deploy Beacon")]
    public sealed class BeaconDeployDefinition : DeployAbilityDefinition
    {
        public const string PoleName = "Pole";
        public const string LampName = "Lamp";

        public const float LampHeightFraction = 0.25f;

        public const float PoleThicknessFraction = 0.5f;

        [SerializeField, Tooltip("名前を引く表 (Localization の items.json) のキー。空なら名前は Id")]
        private string _nameKey = BeaconSpec.DefaultNameKey;

        [SerializeField, Min(1), Tooltip("このビーコンから復活できる回数。使い切ると消える")]
        private int _uses = BeaconSpec.DefaultUses;

        [SerializeField, Min(0f), Tooltip("ビーコンの足元から、復活させる位置までの前 (+Z) への水平距離 (m)")]
        private float _spawnDistance = BeaconSpec.DefaultSpawnDistance;

        // 基底の既定は盾の形と数なので、作った時点でビーコンの既定に置き換える (アセットに保存された値があればそちらが勝つ)。
        public BeaconDeployDefinition()
        {
            MaxActive = BeaconSpec.DefaultMaxActive;
            MaxHealth = BeaconSpec.DefaultMaxHealth;
            Size = new Vector3(BeaconSpec.DefaultWidth, BeaconSpec.DefaultHeight, BeaconSpec.DefaultDepth);
        }

        public string NameKey
        {
            get => _nameKey;
            set => _nameKey = value;
        }

        public override string DisplayNameKey => _nameKey != null ? _nameKey.Trim() : string.Empty;

        public int Uses
        {
            get => _uses;
            set => _uses = value;
        }

        public float SpawnDistance
        {
            get => _spawnDistance;
            set => _spawnDistance = value;
        }

        public BeaconSpec ToBeaconSpec()
        {
            try
            {
                return new BeaconSpec(_uses, _spawnDistance);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: {e.Message}", e);
            }
        }

        // 不正な値を、置いたときでなく枠に入れたとき (AbilityHost.Equip) に分かるようにする。
        public override IAbility CreateAbility()
        {
            ToBeaconSpec();
            return base.CreateAbility();
        }

        // ネット対戦では置いた物の部品 (SpawnBeacon・Deployable) をサーバーが受け持つので、手元には形だけを出す。
        public GameObject CreateLook()
        {
            return CreatePlaceholder(ToDeploySpec());
        }

        // 柱と灯のどちらにも当たり判定を持たせるのは、敵の弾で壊せるようにするため。
        protected override GameObject CreatePlaceholder(DeploySpec spec)
        {
            var root = new GameObject(Id);
            var thickness = Mathf.Min(spec.Width, spec.Depth);
            var lampSize = Mathf.Min(spec.Height * LampHeightFraction, thickness);
            var poleHeight = spec.Height - lampSize;
            var poleThickness = thickness * PoleThicknessFraction;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = PoleName;
            pole.transform.SetParent(root.transform, false);
            pole.transform.localPosition = new Vector3(0f, poleHeight / 2f, 0f);

            // 円柱の素の形は高さ 2 m・直径 1 m。
            pole.transform.localScale = new Vector3(poleThickness, poleHeight / 2f, poleThickness);

            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = LampName;
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0f, poleHeight + (lampSize / 2f), 0f);
            lamp.transform.localScale = Vector3.one * lampSize;
            return root;
        }

        // 盾やタレットと同じく敵には避けて歩かせるが、隠れ場所 (CoverPoint) にはしない。
        protected override void Configure(GameObject placed, DeploySpec spec)
        {
            CarveNavMesh(placed, spec);
            if (placed.GetComponent<SpawnBeacon>() == null)
            {
                placed.AddComponent<SpawnBeacon>();
            }
        }

        protected override void OnPlaced(Deployable deployable, AbilityContext context)
        {
            var beacon = deployable.GetComponent<SpawnBeacon>();
            if (beacon == null)
            {
                throw new InvalidOperationException($"deploy ability '{Id}' placed '{deployable.name}' without a SpawnBeacon");
            }

            beacon.Arm(ToBeaconSpec());
        }
    }
}
