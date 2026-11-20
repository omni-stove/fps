using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Shooter.Runtime
{
    // 置く物の種類ごとに派生クラスを作り (例: ShieldDeployDefinition)、仮の形と種類ごとの部品だけを書かせる。
    // 置く・見本を出す・数を守る流れは DeployAbility にまとめ、種類ごとに書き直させない。
    public abstract class DeployAbilityDefinition : AbilityDefinition
    {
        [SerializeField, Min(1), Tooltip("1 人が同時に置ける数")]
        private int _maxActive = DeploySpec.DefaultMaxActive;

        [SerializeField, Tooltip("同時に置ける数を超えて置こうとしたとき。ReplaceOldest ならいちばん古い物を消す、Refuse なら置かせない")]
        private DeployOverflow _overflow = DeploySpec.DefaultOverflow;

        [SerializeField, Min(1), Tooltip("置いた物の耐久")]
        private int _maxHealth = DeploySpec.DefaultMaxHealth;

        [SerializeField, Min(0.01f), Tooltip("足元から置ける所までの水平距離の上限 (m)")]
        private float _maxRange = DeploySpec.DefaultMaxRange;

        [SerializeField, Range(0f, 89f), Tooltip("置ける足場の傾きの上限 (度)")]
        private float _maxSlopeDegrees = DeploySpec.DefaultMaxSlopeDegrees;

        [SerializeField, Min(0f), Tooltip("置く物の四隅が足場の面からずれてよい高さ (m)。これより大きい段差や縁には置けない")]
        private float _maxStepHeight = DeploySpec.DefaultMaxStepHeight;

        [SerializeField, Tooltip("置く物の幅・高さ・奥行き (m)。置けるかの判定と仮の形の大きさに使う")]
        private Vector3 _size = new Vector3(DeploySpec.DefaultWidth, DeploySpec.DefaultHeight, DeploySpec.DefaultDepth);

        [SerializeField, Tooltip("置く物の見た目と当たり判定。足元が原点、前が +Z。空なら仮の形を作る")]
        private GameObject _prefab;

        [SerializeField, Tooltip("置く前の見本の見た目。空なら置く物と同じ大きさの箱を作る")]
        private GameObject _previewPrefab;

        [SerializeField, Tooltip("置ける所での見本の色")]
        private Color _validColor = DeployPreview.DefaultValidColor;

        [SerializeField, Tooltip("置けない所での見本の色")]
        private Color _invalidColor = DeployPreview.DefaultInvalidColor;

        public int MaxActive
        {
            get => _maxActive;
            set => _maxActive = value;
        }

        public DeployOverflow Overflow
        {
            get => _overflow;
            set => _overflow = value;
        }

        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = value;
        }

        public float MaxRange
        {
            get => _maxRange;
            set => _maxRange = value;
        }

        public float MaxSlopeDegrees
        {
            get => _maxSlopeDegrees;
            set => _maxSlopeDegrees = value;
        }

        public float MaxStepHeight
        {
            get => _maxStepHeight;
            set => _maxStepHeight = value;
        }

        public Vector3 Size
        {
            get => _size;
            set => _size = value;
        }

        public GameObject Prefab
        {
            get => _prefab;
            set => _prefab = value;
        }

        public GameObject PreviewPrefab
        {
            get => _previewPrefab;
            set => _previewPrefab = value;
        }

        public Color ValidColor
        {
            get => _validColor;
            set => _validColor = value;
        }

        public Color InvalidColor
        {
            get => _invalidColor;
            set => _invalidColor = value;
        }

        public DeploySpec ToDeploySpec()
        {
            try
            {
                return new DeploySpec(
                    _maxActive, _overflow, _maxHealth, _maxRange, _maxSlopeDegrees, _maxStepHeight, _size.x, _size.y, _size.z);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: {e.Message}", e);
            }
        }

        // 置く物の設定が不正なら、使ったときでなく枠に入れたとき (AbilityHost.Equip) に分かるよう、ここで変換して確かめる。
        public override IAbility CreateAbility()
        {
            return new DeployAbility(this, ToDeploySpec());
        }

        public Deployable CreateDeployed(Pose pose, DeploySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var placed = _prefab != null ? Instantiate(_prefab) : CreatePlaceholder(spec);
            if (placed == null)
            {
                throw new InvalidOperationException($"deploy ability '{Id}' created no object (CreatePlaceholder returned null)");
            }

            placed.name = Id;
            placed.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var deployable = placed.GetComponent<Deployable>();
            if (deployable == null)
            {
                deployable = placed.AddComponent<Deployable>();
            }

            Configure(placed, spec);
            return deployable;
        }

        public DeployPreview CreatePreview(DeploySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var visual = _previewPrefab != null ? Instantiate(_previewPrefab) : CreateBox(Id + "Preview", spec, false);
            visual.name = Id + "Preview";
            return DeployPreview.Attach(visual, _validColor, _invalidColor);
        }

        // 床に立てる物と、壁や人に貼る物 (C4) とで置ける所が違うので、種類ごとに差し替えられるようにする。
        public virtual DeployVerdict Survey(Transform owner, Vector3 aimOrigin, Vector3 aimDirection, DeploySpec spec, out Pose pose, out Collider surface)
        {
            surface = null;
            return DeploySurveyor.Survey(owner, aimOrigin, aimDirection, spec, out pose);
        }

        public virtual bool Detonates => false;

        public virtual float DetonateTapSeconds => 0f;

        public virtual int Detonate(IReadOnlyList<Deployable> placed, AbilityContext context)
        {
            return 0;
        }

        // 足元が原点、前が +Z で、spec の大きさに収める。置けるかの判定がこの大きさで調べるため。
        protected abstract GameObject CreatePlaceholder(DeploySpec spec);

        protected virtual void Configure(GameObject placed, DeploySpec spec)
        {
        }

        // 持ち主の設定 (味方撃ちのルールなど) は置く物を作る時点 (CreateDeployed) には渡らないので、持ち主と陣営が決まった後に別に呼ぶ。
        protected virtual void OnPlaced(Deployable deployable, AbilityContext context)
        {
        }

        protected virtual void OnPlaced(Deployable deployable, AbilityContext context, Collider surface)
        {
            OnPlaced(deployable, context);
        }

        internal void NotifyPlaced(Deployable deployable, AbilityContext context, Collider surface)
        {
            OnPlaced(deployable, context, surface);
        }

        // 爆発させた持ち主のキルフィードに置いた物の名前を出せるよう、持ち主の能力の枠から Id で探す。
        protected static bool TryFindEquipped<T>(GameObject owner, string kindId, out T definition)
            where T : DeployAbilityDefinition
        {
            definition = null;
            if (owner == null || string.IsNullOrEmpty(kindId))
            {
                return false;
            }

            var host = owner.GetComponentInChildren<AbilityHost>(true);
            if (host == null)
            {
                return false;
            }

            for (var i = 0; i < AbilityHost.SlotCount; i++)
            {
                if (host.Definition(i) is T found && string.Equals(found.Id, kindId, StringComparison.Ordinal))
                {
                    definition = found;
                    return true;
                }
            }

            return false;
        }

        // 敵が置いた物に向かって歩き続けないよう、NavMesh に穴を開けて避けさせる。
        protected static void CarveNavMesh(GameObject placed, DeploySpec spec)
        {
            // 差し替えた見た目が自分の形の NavMeshObstacle を持っていれば、そちらを使う。
            if (placed.GetComponentInChildren<NavMeshObstacle>() != null)
            {
                return;
            }

            var obstacle = placed.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, spec.Height / 2f, 0f);
            obstacle.size = new Vector3(spec.Width, spec.Height, spec.Depth);
            obstacle.carving = true;
        }

        protected static GameObject CreateBox(string name, DeploySpec spec, bool withCollider)
        {
            var root = new GameObject(name);
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Box";
            if (!withCollider)
            {
                // Destroy は次のフレームまで残り、その間に物理に固い箱として現れるので、すぐに外す。
                DestroyImmediate(box.GetComponent<Collider>());
            }

            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = new Vector3(0f, spec.Height / 2f, 0f);
            box.transform.localScale = new Vector3(spec.Width, spec.Height, spec.Depth);
            return root;
        }
    }
}
