using System;
using UnityEngine;
using UnityEngine.AI;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DestructiblePiece : MonoBehaviour
    {
        public const int DefaultMaxHealth = 100;

        public const string VisualName = "Visual";

        [SerializeField, Min(0), Tooltip("建造物の中での添字。建造物の部品で 0 から部品数-1 までを重ならないように振る")]
        private int _index;

        [SerializeField, Min(1), Tooltip("最大耐久値")]
        private int _maxHealth = DefaultMaxHealth;

        [SerializeField, Tooltip("地面に接していて、他の部品が無くても立っていられるか")]
        private bool _grounded;

        [SerializeField, Tooltip("当たり判定と隣接の計算に使うコライダー。空なら同じ GameObject から探す")]
        private Collider _collider;

        public int Index => _index;

        public int MaxHealth => _maxHealth;

        public bool Grounded => _grounded;

        public Collider Collider
        {
            get
            {
                if (_collider == null)
                {
                    _collider = GetComponent<Collider>();
                }

                return _collider;
            }
        }

        public bool IsBroken { get; private set; }

        public static DestructiblePiece Create(
            Transform parent,
            string pieceName,
            Vector3 localPosition,
            Vector3 size,
            int index,
            int maxHealth = DefaultMaxHealth,
            bool grounded = false,
            bool carveObstacle = true,
            GameObject visualPrefab = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (!(size.x > 0f) || !(size.y > 0f) || !(size.z > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "size must be positive on every axis");
            }

            var cube = visualPrefab != null ? new GameObject(pieceName, typeof(BoxCollider)) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                cube.name = pieceName;
                if (visualPrefab != null)
                {
                    // 隣接と当たりは部品の箱で決めるので、見た目の当たり判定は外す (残すと隣の部品と重なって支えの計算が変わる)。
                    VisualPrefab.Instantiate(visualPrefab, cube.transform, VisualName, stripColliders: true);
                }

                cube.transform.SetParent(parent, false);
                cube.transform.localPosition = localPosition;
                cube.transform.localRotation = Quaternion.identity;
                cube.transform.localScale = size;

                if (carveObstacle)
                {
                    var obstacle = cube.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.center = Vector3.zero;
                    // Obstacle の大きさは Transform の拡大を掛けたものになるので、1m の Cube と同じ 1 にする。
                    obstacle.size = Vector3.one;
                    obstacle.carving = true;
                }

                var piece = cube.AddComponent<DestructiblePiece>();
                piece.Initialize(index, maxHealth, grounded, cube.GetComponent<BoxCollider>());
                return piece;
            }
            catch
            {
                DestroySafely(cube);
                throw;
            }
        }

        public void Initialize(int index, int maxHealth, bool grounded, Collider collider = null)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must not be negative");
            }

            if (maxHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be at least 1");
            }

            _index = index;
            _maxHealth = maxHealth;
            _grounded = grounded;
            _collider = collider;
        }

        // 部品を GameObject ごと消さないのは、同期で「壊れていない」に戻されたときに元へ戻せるようにするため。
        public void SetBroken(bool broken)
        {
            IsBroken = broken;
            var collider = Collider;
            if (collider != null)
            {
                collider.enabled = !broken;
            }

            foreach (var child in GetComponentsInChildren<Collider>(true))
            {
                if (OwnsComponent(child))
                {
                    child.enabled = !broken;
                }
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (OwnsComponent(renderer))
                {
                    renderer.enabled = !broken;
                }
            }

            foreach (var obstacle in GetComponentsInChildren<NavMeshObstacle>(true))
            {
                if (OwnsComponent(obstacle))
                {
                    obstacle.enabled = !broken;
                }
            }
        }

        // 子孫に別の部品があるとき、その部品の分まで切り替えると支えの状態とずれるため。
        private bool OwnsComponent(Component component)
        {
            return component.GetComponentInParent<DestructiblePiece>(true) == this;
        }

        public void GetBox(out Vector3 center, out Quaternion rotation, out Vector3 size)
        {
            var collider = Collider;
            if (collider is BoxCollider box)
            {
                var t = box.transform;
                center = t.TransformPoint(box.center);
                rotation = t.rotation;
                size = Vector3.Scale(box.size, Abs(t.lossyScale));
                return;
            }

            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
            {
                var bounds = collider.bounds;
                center = bounds.center;
                rotation = Quaternion.identity;
                size = bounds.size;
                return;
            }

            center = transform.position;
            rotation = transform.rotation;
            size = Abs(transform.lossyScale);
        }

        public Material DebrisMaterial
        {
            get
            {
                var renderer = GetComponentInChildren<Renderer>(true);
                return renderer != null ? renderer.sharedMaterial : null;
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
