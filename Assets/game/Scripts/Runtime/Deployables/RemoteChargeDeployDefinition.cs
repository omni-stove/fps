using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "DeployRemoteCharge", menuName = "Shooter/Abilities/Deploy Remote Charge")]
    public sealed class RemoteChargeDeployDefinition : DeployAbilityDefinition
    {
        public const string BodyName = "Body";

        private const int MaxHits = 32;

        private static readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

        [SerializeField, Tooltip("名前を引く表 (Localization の items.json) のキー。空なら名前は Id")]
        private string _nameKey = RemoteChargeSpec.DefaultNameKey;

        [SerializeField, Min(0f), Tooltip("置いた物があるとき、押してからこの秒数より前に離すと起爆する。押し続けると狙って置く")]
        private float _detonateTapSeconds = RemoteChargeSpec.DefaultDetonateTapSeconds;

        [SerializeField, Min(0), Tooltip("爆発の中心 (fullDamageRadius の内側) でのダメージ")]
        private int _maxDamage = RemoteChargeSpec.DefaultMaxDamage;

        [SerializeField, Min(0.01f), Tooltip("爆発が届く半径 (m)。建造物への被害もこの半径")]
        private float _damageRadius = RemoteChargeSpec.DefaultDamageRadius;

        [SerializeField, Min(0f), Tooltip("減らさずに最大のダメージを与える半径 (m)。damageRadius 未満")]
        private float _fullDamageRadius = RemoteChargeSpec.DefaultFullDamageRadius;

        [SerializeField, Min(0), Tooltip("爆発の中心で建造物に与える被害")]
        private int _structureDamageAtCenter = RemoteChargeSpec.DefaultStructureDamageAtCenter;

        [SerializeField, Tooltip("貼ったときに C4 の場所で鳴らす音。空なら鳴らさない")]
        private AudioClip _attachedSound;

        [SerializeField, Range(0f, 1f), Tooltip("貼ったときの音の音量")]
        private float _attachedVolume = 1f;

        // 基底の既定は盾の形と数なので、作った時点で C4 の既定に置き換える (アセットに保存された値があればそちらが勝つ)。
        public RemoteChargeDeployDefinition()
        {
            MaxActive = RemoteChargeSpec.DefaultMaxActive;
            Overflow = RemoteChargeSpec.DefaultOverflow;
            MaxHealth = RemoteChargeSpec.DefaultMaxHealth;
            MaxRange = RemoteChargeSpec.DefaultMaxRange;
            Size = new Vector3(RemoteChargeSpec.DefaultWidth, RemoteChargeSpec.DefaultHeight, RemoteChargeSpec.DefaultDepth);
        }

        public string NameKey
        {
            get => _nameKey;
            set => _nameKey = value;
        }

        public override string DisplayNameKey => _nameKey != null ? _nameKey.Trim() : string.Empty;

        public float TapSeconds
        {
            get => _detonateTapSeconds;
            set => _detonateTapSeconds = value;
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

        public AudioClip AttachedSound
        {
            get => _attachedSound;
            set => _attachedSound = value;
        }

        public float AttachedVolume
        {
            get => _attachedVolume;
            set => _attachedVolume = value;
        }

        public override bool Detonates => true;

        public override float DetonateTapSeconds => _detonateTapSeconds;

        public static bool TryFind(GameObject owner, string kindId, out RemoteChargeDeployDefinition charge)
        {
            return TryFindEquipped(owner, kindId, out charge);
        }

        public RemoteChargeSpec ToRemoteChargeSpec()
        {
            if (!(_attachedVolume >= 0f) || _attachedVolume > 1f)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: attachedVolume must be between 0 and 1");
            }

            try
            {
                var explosion = new ExplosionSpec(new ExplosionDamage(_maxDamage, _damageRadius, _fullDamageRadius), _structureDamageAtCenter);
                return new RemoteChargeSpec(_detonateTapSeconds, explosion);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: {e.Message}", e);
            }
        }

        // 不正な値を、置いたときでなく枠に入れたとき (AbilityHost.Equip) に分かるようにする。
        public override IAbility CreateAbility()
        {
            ToRemoteChargeSpec();
            return base.CreateAbility();
        }

        // 床に立てる物と違い、狙った先の面 (壁・床・天井・人や標的の体) にそのまま貼る。距離は体の中心から測る。
        public override DeployVerdict Survey(Transform owner, Vector3 aimOrigin, Vector3 aimDirection, DeploySpec spec, out Pose pose, out Collider surface)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            surface = null;
            var body = BodyCenterOf(owner);
            var direction = aimDirection.sqrMagnitude > 1e-8f ? aimDirection.normalized : owner.forward;
            var reach = Vector3.Distance(aimOrigin, body) + spec.MaxRange;
            if (!TryNearest(aimOrigin, direction, reach, owner, out var hit) || Vector3.Distance(hit.point, body) > spec.MaxRange)
            {
                // 置けなくても見本を出せるよう、届く所の端に置こうとしたことにする。
                pose = new Pose(body + (direction * spec.MaxRange), Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f));
                return DeployVerdict.NoGround;
            }

            pose = new Pose(hit.point, SurfaceRotation(hit.normal, direction));

            // 置いた物どうし (盾や別の C4) には貼らない。盾ごと壊れて爆発しなくなるため。
            if (Deployable.Of(hit.collider) != null)
            {
                return DeployVerdict.Blocked;
            }

            surface = hit.collider;
            return DeployVerdict.Ok;
        }

        public override int Detonate(IReadOnlyList<Deployable> placed, AbilityContext context)
        {
            return RemoteCharge.DetonateAll(placed);
        }

        public static Quaternion SurfaceRotation(Vector3 normal, Vector3 aimDirection)
        {
            var up = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up;
            var forward = Vector3.ProjectOnPlane(aimDirection, up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.up, up);
            }

            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            return Quaternion.LookRotation(forward.normalized, up);
        }

        // 当たり判定を持たせるのは、敵の弾で壊せるようにするため。
        protected override GameObject CreatePlaceholder(DeploySpec spec)
        {
            var root = new GameObject(Id);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = BodyName;
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, spec.Height / 2f, 0f);
            body.transform.localScale = new Vector3(spec.Width, spec.Height, spec.Depth);
            return root;
        }

        // 小さく壁や人に付く物なので、盾やタレットと違って NavMesh に穴を開けない。
        protected override void Configure(GameObject placed, DeploySpec spec)
        {
            if (placed.GetComponent<RemoteCharge>() == null)
            {
                placed.AddComponent<RemoteCharge>();
            }
        }

        protected override void OnPlaced(Deployable deployable, AbilityContext context, Collider surface)
        {
            var charge = deployable.GetComponent<RemoteCharge>();
            if (charge == null)
            {
                throw new InvalidOperationException($"deploy ability '{Id}' placed '{deployable.name}' without a RemoteCharge");
            }

            charge.Arm(ToRemoteChargeSpec(), surface, _attachedSound, _attachedVolume);
        }

        private static Vector3 BodyCenterOf(Transform owner)
        {
            var controller = owner.GetComponent<CharacterController>();
            return controller != null && controller.enabled ? controller.bounds.center : owner.position;
        }

        private static bool TryNearest(Vector3 origin, Vector3 direction, float distance, Transform owner, out RaycastHit nearest)
        {
            var count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            nearest = default;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var candidate = _hits[i];
                if (candidate.collider.transform.IsChildOf(owner))
                {
                    continue;
                }

                if (!found || candidate.distance < nearest.distance)
                {
                    nearest = candidate;
                    found = true;
                }
            }

            Array.Clear(_hits, 0, count);
            return found;
        }
    }
}
