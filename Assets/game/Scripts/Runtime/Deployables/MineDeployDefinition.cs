using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "DeployMine", menuName = "Shooter/Abilities/Deploy Mine")]
    public sealed class MineDeployDefinition : DeployAbilityDefinition
    {
        public const string BodyName = "Body";

        [SerializeField, Tooltip("名前を引く表 (Localization の items.json) のキー。空なら名前は Id")]
        private string _nameKey = MineSpec.DefaultNameKey;

        [SerializeField, Min(0f), Tooltip("置いてから有効になるまでの秒数。それまでは敵が入っても爆発しない")]
        private float _armSeconds = MineSpec.DefaultArmSeconds;

        [SerializeField, Min(0.01f), Tooltip("敵がこの距離 (m) に入ると爆発する")]
        private float _triggerRadius = MineSpec.DefaultTriggerRadius;

        [SerializeField, Min(0), Tooltip("爆発の中心 (fullDamageRadius の内側) でのダメージ")]
        private int _maxDamage = MineSpec.DefaultMaxDamage;

        [SerializeField, Min(0.01f), Tooltip("爆発が届く半径 (m)。建造物への被害もこの半径")]
        private float _damageRadius = MineSpec.DefaultDamageRadius;

        [SerializeField, Min(0f), Tooltip("減らさずに最大のダメージを与える半径 (m)。damageRadius 未満")]
        private float _fullDamageRadius = MineSpec.DefaultFullDamageRadius;

        [SerializeField, Min(0), Tooltip("爆発の中心で建造物に与える被害")]
        private int _structureDamageAtCenter = MineSpec.DefaultStructureDamageAtCenter;

        [SerializeField, Tooltip("有効になったときに地雷の場所で鳴らす音。空なら鳴らさない")]
        private AudioClip _armedSound;

        [SerializeField, Range(0f, 1f), Tooltip("有効になったときの音の音量")]
        private float _armedVolume = 1f;

        // 基底の既定は盾の形と数なので、作った時点で地雷の既定に置き換える (アセットに保存された値があればそちらが勝つ)。
        public MineDeployDefinition()
        {
            MaxActive = MineSpec.DefaultMaxActive;
            MaxHealth = MineSpec.DefaultMaxHealth;
            Size = new Vector3(MineSpec.DefaultWidth, MineSpec.DefaultHeight, MineSpec.DefaultDepth);
        }

        public string NameKey
        {
            get => _nameKey;
            set => _nameKey = value;
        }

        public override string DisplayNameKey => _nameKey != null ? _nameKey.Trim() : string.Empty;

        public float ArmSeconds
        {
            get => _armSeconds;
            set => _armSeconds = value;
        }

        public float TriggerRadius
        {
            get => _triggerRadius;
            set => _triggerRadius = value;
        }

        public int MaxDamage
        {
            get => _maxDamage;
            set => _maxDamage = value;
        }

        public float DamageRadius
        {
            get => _damageRadius;
            set => _damageRadius = value;
        }

        public float FullDamageRadius
        {
            get => _fullDamageRadius;
            set => _fullDamageRadius = value;
        }

        public int StructureDamageAtCenter
        {
            get => _structureDamageAtCenter;
            set => _structureDamageAtCenter = value;
        }

        public AudioClip ArmedSound
        {
            get => _armedSound;
            set => _armedSound = value;
        }

        public float ArmedVolume
        {
            get => _armedVolume;
            set => _armedVolume = value;
        }

        public static bool TryFind(GameObject owner, string kindId, out MineDeployDefinition mine)
        {
            return TryFindEquipped(owner, kindId, out mine);
        }

        public MineSpec ToMineSpec()
        {
            if (!(_armedVolume >= 0f) || _armedVolume > 1f)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: armedVolume must be between 0 and 1");
            }

            try
            {
                var explosion = new ExplosionSpec(new ExplosionDamage(_maxDamage, _damageRadius, _fullDamageRadius), _structureDamageAtCenter);
                return new MineSpec(_armSeconds, _triggerRadius, explosion);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: {e.Message}", e);
            }
        }

        // 不正な値を、置いたときでなく枠に入れたとき (AbilityHost.Equip) に分かるようにする。
        public override IAbility CreateAbility()
        {
            ToMineSpec();
            return base.CreateAbility();
        }

        // 当たり判定を持たせるのは、敵の弾で壊せるようにするため。
        protected override GameObject CreatePlaceholder(DeploySpec spec)
        {
            var root = new GameObject(Id);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = BodyName;
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, spec.Height / 2f, 0f);

            // 円柱の素の形は高さ 2 m・直径 1 m。
            body.transform.localScale = new Vector3(spec.Width, spec.Height / 2f, spec.Depth);

            // 円柱に付くカプセルは、高さが直径より短いと球になって地面から突き出すので、箱に差し替える。
            var capsule = body.GetComponent<Collider>();
            if (capsule != null)
            {
                UnityEngine.Object.DestroyImmediate(capsule);
            }

            var box = body.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 2f, 1f);
            return root;
        }

        // 敵に踏ませるので、盾やタレットと違って NavMesh に穴を開けない。
        protected override void Configure(GameObject placed, DeploySpec spec)
        {
            if (placed.GetComponent<Mine>() == null)
            {
                placed.AddComponent<Mine>();
            }
        }

        protected override void OnPlaced(Deployable deployable, AbilityContext context)
        {
            var mine = deployable.GetComponent<Mine>();
            if (mine == null)
            {
                throw new InvalidOperationException($"deploy ability '{Id}' placed '{deployable.name}' without a Mine");
            }

            mine.Arm(ToMineSpec(), _armedSound, _armedVolume);
        }
    }
}
