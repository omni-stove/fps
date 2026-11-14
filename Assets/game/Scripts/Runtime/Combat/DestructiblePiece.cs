using System;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="DestructibleStructure"/> の部品 1 つ。建造物の子孫に置き、添字・最大耐久値・地面に接しているか・当たり判定のコライダーを持つ。
    /// 隣接は建造物がコライダーの範囲の重なりから作るので、ここでは設定しない。
    /// 壊れると (<see cref="SetBroken"/>) 自分と子孫のコライダー・Renderer・<see cref="NavMeshObstacle"/> を無効にする。
    /// 部品を GameObject ごと消さないのは、同期で「壊れていない」に戻されたときに元へ戻せるようにするため。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructiblePiece : MonoBehaviour
    {
        public const int DefaultMaxHealth = 100;

        /// <summary><see cref="Create"/> に渡したプレハブを置く子の名前。</summary>
        public const string VisualName = "Visual";

        [SerializeField, Min(0), Tooltip("建造物の中での添字。建造物の部品で 0 から部品数-1 までを重ならないように振る")]
        private int _index;

        [SerializeField, Min(1), Tooltip("最大耐久値")]
        private int _maxHealth = DefaultMaxHealth;

        [SerializeField, Tooltip("地面に接していて、他の部品が無くても立っていられるか")]
        private bool _grounded;

        [SerializeField, Tooltip("当たり判定と隣接の計算に使うコライダー。空なら同じ GameObject から探す")]
        private Collider _collider;

        /// <summary>建造物の中での添字。</summary>
        public int Index => _index;

        /// <summary>最大耐久値。</summary>
        public int MaxHealth => _maxHealth;

        /// <summary>地面に接しているか。</summary>
        public bool Grounded => _grounded;

        /// <summary>当たり判定のコライダー。未設定なら同じ GameObject のもの (無ければ null)。</summary>
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

        /// <summary><see cref="SetBroken"/> で壊れた状態にされていれば true。</summary>
        public bool IsBroken { get; private set; }

        /// <summary>
        /// 1m の Cube の部品を作る。size に合わせた <see cref="BoxCollider"/> の付いた見た目と、
        /// carveObstacle なら同じ大きさで NavMesh を削る <see cref="NavMeshObstacle"/> を付ける。
        /// visualPrefab があれば、見た目だけをそのプレハブにする (当たり判定は同じ大きさの箱のまま)。
        /// 途中で失敗したら作りかけを片付けて例外を投げ直す。
        /// </summary>
        /// <param name="parent">親 (建造物か、その子孫)。null 不可。</param>
        /// <param name="pieceName">GameObject の名前。</param>
        /// <param name="localPosition">親から見た中心の位置。</param>
        /// <param name="size">大きさ (m)。各軸 0 より大きい。</param>
        /// <param name="index">建造物の中での添字。0 以上。</param>
        /// <param name="maxHealth">最大耐久値。1 以上。</param>
        /// <param name="grounded">地面に接しているか。</param>
        /// <param name="carveObstacle">NavMesh を削る NavMeshObstacle を付けるか。</param>
        /// <param name="visualPrefab">
        /// 見た目。原点を中心にした大きさ 1m の形にする (部品と一緒に size まで拡大する)。当たり判定を外して <see cref="VisualName"/> の子に置く。
        /// null なら仮の Cube を作る。
        /// </param>
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

        /// <summary>添字・最大耐久値・地面に接しているか・コライダーを設定する。建造物の組み立て (Build) より前に呼ぶ。</summary>
        /// <param name="index">建造物の中での添字。0 以上。</param>
        /// <param name="maxHealth">最大耐久値。1 以上。</param>
        /// <param name="grounded">地面に接しているか。</param>
        /// <param name="collider">当たり判定のコライダー。null なら同じ GameObject から探す。</param>
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

        /// <summary>
        /// 壊れた状態にする (コライダー・Renderer・NavMeshObstacle を無効にする) か、壊れていない状態に戻す (有効にする)。
        /// 今と同じ状態を渡したときも、各コンポーネントは渡した状態に揃える。
        /// </summary>
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

        /// <summary>
        /// 破片の見た目に使う、部品の中心・向き・大きさ。BoxCollider ならその箱 (回転を含む)、
        /// それ以外はコライダーの範囲 (ワールド軸に揃えた箱)。コライダーが無ければ Transform の位置と拡大。
        /// </summary>
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

        /// <summary>破片に使う材質。自分か子孫の最初の Renderer の共有材質。無ければ null。</summary>
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
