using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "DeployDecoy", menuName = "Shooter/Abilities/Deploy Decoy")]
    public sealed class DecoyDeployDefinition : DeployAbilityDefinition
    {
        public const string BodyName = "Body";
        public const string HeadName = "Head";

        public const float HeadHeightFraction = 0.2f;

        [SerializeField, Min(0.01f), Tooltip("置いてから消えるまでの秒数")]
        private float _lifetimeSeconds = DecoySpec.DefaultLifetimeSeconds;

        [SerializeField, Min(0.01f), Tooltip("呼び声を出す間隔 (秒)。置いた時点で 1 回呼ぶ")]
        private float _callIntervalSeconds = DecoySpec.DefaultCallIntervalSeconds;

        [SerializeField, Min(0f), Tooltip("呼び声が敵に聞こえる距離 (m)。聞いた敵は囮の場所を調べに来る。0 なら見えた敵だけを引きつける")]
        private float _callRadius = DecoySpec.DefaultCallRadius;

        [SerializeField, Tooltip("呼ぶたびに囮の場所で鳴らす音。空なら鳴らさない (敵には聞こえる)")]
        private AudioClip _callSound;

        [SerializeField, Range(0f, 1f), Tooltip("呼び声の音量")]
        private float _callVolume = 1f;

        // 基底の既定は盾の形と数なので、作った時点で囮の既定に置き換える (アセットに保存された値があればそちらが勝つ)。
        public DecoyDeployDefinition()
        {
            MaxActive = DecoySpec.DefaultMaxActive;
            MaxHealth = DecoySpec.DefaultMaxHealth;
            Size = new Vector3(DecoySpec.DefaultWidth, DecoySpec.DefaultHeight, DecoySpec.DefaultDepth);
        }

        public float LifetimeSeconds
        {
            get => _lifetimeSeconds;
            set => _lifetimeSeconds = value;
        }

        public float CallIntervalSeconds
        {
            get => _callIntervalSeconds;
            set => _callIntervalSeconds = value;
        }

        public float CallRadius
        {
            get => _callRadius;
            set => _callRadius = value;
        }

        public AudioClip CallSound
        {
            get => _callSound;
            set => _callSound = value;
        }

        public float CallVolume
        {
            get => _callVolume;
            set => _callVolume = value;
        }

        public DecoySpec ToDecoySpec()
        {
            if (!(_callVolume >= 0f) || _callVolume > 1f)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: callVolume must be between 0 and 1");
            }

            try
            {
                return new DecoySpec(_lifetimeSeconds, _callIntervalSeconds, _callRadius);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: {e.Message}", e);
            }
        }

        // 不正な値を、置いたときでなく枠に入れたとき (AbilityHost.Equip) に分かるようにする。
        public override IAbility CreateAbility()
        {
            ToDecoySpec();
            return base.CreateAbility();
        }

        // 胴と頭のどちらにも当たり判定を持たせるのは、敵の視線と弾を受けるため。
        protected override GameObject CreatePlaceholder(DeploySpec spec)
        {
            var root = new GameObject(Id);
            var thickness = Mathf.Min(spec.Width, spec.Depth);
            var headSize = Mathf.Min(spec.Height * HeadHeightFraction, thickness);
            var bodyHeight = spec.Height - headSize;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = BodyName;
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, bodyHeight / 2f, 0f);

            // カプセルの素の形は高さ 2 m・直径 1 m。
            body.transform.localScale = new Vector3(thickness, bodyHeight / 2f, thickness);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = HeadName;
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, bodyHeight + (headSize / 2f), 0f);
            head.transform.localScale = Vector3.one * headSize;
            return root;
        }

        // 盾やタレットと同じく敵には避けて歩かせるが、隠れ場所 (CoverPoint) にはしない。
        protected override void Configure(GameObject placed, DeploySpec spec)
        {
            CarveNavMesh(placed, spec);
            if (placed.GetComponent<Decoy>() == null)
            {
                placed.AddComponent<Decoy>();
            }
        }

        protected override void OnPlaced(Deployable deployable, AbilityContext context)
        {
            var decoy = deployable.GetComponent<Decoy>();
            if (decoy == null)
            {
                throw new InvalidOperationException($"deploy ability '{Id}' placed '{deployable.name}' without a Decoy");
            }

            decoy.Arm(ToDecoySpec(), _callSound, _callVolume);
        }
    }
}
