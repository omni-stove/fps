using System;
using System.Collections.Generic;
using System.Text;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 弾で壊せる建物。子孫の <see cref="DestructiblePiece"/> を部品として、耐久値と崩落の判断は <see cref="StructureIntegrity"/> に任せる。
    /// 部品の隣接は、部品のコライダーの範囲を <see cref="AdjacencyMargin"/> だけ広げて重なる組とみなして組み立て時に作る (手で設定しない)。
    /// 組み立ては Awake か最初に触れたときに行うので、Awake が走らない Edit Mode でも使える。
    /// 部品が壊れる (直接でも崩落でも) と、部品の当たり判定・見た目・NavMeshObstacle を無効にし、<see cref="DebrisPool"/> に破片を出す。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="DestructionEnabled"/> が false (既定) の間は弾が当たっても何もしない。</item>
    /// <item><see cref="HasAuthority"/> が false (ネットのクライアント) の間はダメージを無視し、<see cref="ApplyDestroyedBitset"/> だけを反映する。</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DestructibleStructure : MonoBehaviour, IStructureHittable
    {
        public const string LogTag = "[Destruction]";

        /// <summary>隣接とみなすために、部品のコライダーの範囲を各方向へ広げる量 (m)。</summary>
        public const float AdjacencyMargin = 0.05f;

        /// <summary>直接壊れた部品 1 つから出す小さな破片の数。</summary>
        public const int FragmentsPerDestroyedPiece = 4;

        /// <summary>小さな破片の一辺の上限と下限 (m)。部品の一番短い辺の半分を、この範囲に収める。</summary>
        public const float MaxFragmentSize = 0.3f;
        public const float MinFragmentSize = 0.05f;

        /// <summary>小さな破片を着弾点から遠ざける速さ (m/s)。</summary>
        public const float FragmentSpeed = 3f;

        [SerializeField, Tooltip("弾で壊れるか。false の間は当たっても何もしない")]
        private bool _destructionEnabled;

        [SerializeField, Tooltip("ダメージを計算する側か。false (ネットのクライアント) ならダメージを無視し、同期された状態だけを反映する")]
        private bool _hasAuthority = true;

        [SerializeField, Tooltip("壊れたときに破片を出すか")]
        private bool _spawnDebris = true;

        [SerializeField, Tooltip("破片を出す先。空ならシーンのものを探すか作る")]
        private DebrisPool _debrisPool;

        private StructureIntegrity _integrity;
        private DestructiblePiece[] _pieces = Array.Empty<DestructiblePiece>();
        private readonly Dictionary<Collider, int> _pieceByCollider = new Dictionary<Collider, int>();
        private bool _buildFailed;

        /// <summary>
        /// 権威側で部品が壊れたときに、<see cref="StructureIntegrity.ApplyDamage"/> の結果を渡して呼ばれる。
        /// 部品の見た目を消して破片を出した後に呼ぶ。<see cref="ApplyDestroyedBitset"/> では呼ばない。
        /// </summary>
        public event Action<StructureDamageResult> PiecesDestroyed;

        /// <summary>弾で壊れるか。既定は false。</summary>
        public bool DestructionEnabled
        {
            get => _destructionEnabled;
            set => _destructionEnabled = value;
        }

        /// <summary>ダメージを計算する側か。既定は true。false ならダメージを無視する。</summary>
        public bool HasAuthority
        {
            get => _hasAuthority;
            set => _hasAuthority = value;
        }

        /// <summary>壊れたときに破片を出すか。既定は true。見た目の要らない専用サーバーでは false にする。</summary>
        public bool SpawnDebris
        {
            get => _spawnDebris;
            set => _spawnDebris = value;
        }

        /// <summary>破片を出す先。未設定なら最初に破片を出すときに <see cref="DebrisPool.GetOrCreate"/> で決める。</summary>
        public DebrisPool DebrisPool
        {
            get => _debrisPool;
            set => _debrisPool = value;
        }

        /// <summary>部品の耐久値と壊れたかどうか。初めて触れたときに組み立てる。部品の設定が不正なら例外を投げる。</summary>
        public StructureIntegrity Integrity
        {
            get
            {
                if (_integrity == null)
                {
                    Build();
                }

                return _integrity;
            }
        }

        /// <summary>部品。添字の順に並ぶ。初めて触れたときに組み立てる。</summary>
        public IReadOnlyList<DestructiblePiece> Pieces
        {
            get
            {
                _ = Integrity;
                return Array.AsReadOnly(_pieces);
            }
        }

        /// <summary>壊れている部品の集合 (<see cref="StructureIntegrity.ToBitset"/>)。ネットの同期に使う。</summary>
        public ulong[] DestroyedBitset => Integrity.ToBitset();

        private void Awake()
        {
            // AddComponent 直後は部品がまだ無いので、ここで失敗扱いにせず最初に触れたときまで組み立てを待つ。
            if (GetComponentInChildren<DestructiblePiece>(true) == null)
            {
                return;
            }

            TryEnsureBuilt();
        }

        /// <summary>
        /// 子孫の部品を集め直し、すべて壊れていない状態に戻して、隣接を作り直す。部品を足したり動かしたりした後に呼ぶ。
        /// 部品の設定が不正なら例外を投げ、前の耐久値の状態を残す (部品の見た目は壊れていない状態に戻っていることがある)。
        /// 範囲の求まらない非アクティブな部品は、どの部品とも隣接しない。
        /// </summary>
        public void Build()
        {
            var pieces = CollectPieces();
            foreach (var piece in pieces)
            {
                piece.SetBroken(false);
            }

            // 生成直後や移動直後のコライダーの範囲は、物理側に反映するまで古いことがある。
            Physics.SyncTransforms();
            var layout = CreateLayout(pieces);

            _pieceByCollider.Clear();
            foreach (var piece in pieces)
            {
                _pieceByCollider[piece.Collider] = piece.Index;
            }

            _pieces = pieces;
            _integrity = new StructureIntegrity(layout);
            _buildFailed = false;
        }

        /// <summary>
        /// collider の部品に damage の被害を与える。<see cref="DestructionEnabled"/> が false か <see cref="HasAuthority"/> が false なら何もしない。
        /// この建物の部品でないコライダーなら何もしない。部品が壊れたら見た目を消し、破片を出し、ログを出して <see cref="PiecesDestroyed"/> を呼ぶ。
        /// 部品の設定が不正で組み立てられないときは、エラーを 1 度ログに出して以後は何もしない (撃った側を止めないため)。
        /// </summary>
        public void ApplyStructureHit(Collider collider, Vector3 point, int damage)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (!_destructionEnabled || !_hasAuthority || collider == null || !TryEnsureBuilt())
            {
                return;
            }

            if (!TryGetPieceIndex(collider, out var index))
            {
                return;
            }

            var result = _integrity.ApplyDamage(index, damage);
            if (!result.Any)
            {
                return;
            }

            foreach (var destroyed in result.Destroyed)
            {
                BreakPiece(destroyed, point, collapsed: false);
            }

            foreach (var collapsed in result.Collapsed)
            {
                BreakPiece(collapsed, point, collapsed: true);
            }

            Debug.Log(FormatDestroyedLog(name, result.Destroyed, result.Collapsed), this);
            PiecesDestroyed?.Invoke(result);
        }

        /// <summary>
        /// 権威側から受け取った壊れた部品の集合 (<see cref="DestroyedBitset"/> の形) をそのまま反映する。崩落の計算はしない。
        /// 新たに壊れた部品は見た目を消して破片を出し、壊れていないことになった部品は元に戻す。
        /// <see cref="DestructionEnabled"/> と <see cref="HasAuthority"/> に関わらず反映する。
        /// </summary>
        /// <param name="bitset">要素数は <see cref="StructureIntegrity.BitsetLength"/>。部品数以降のビットは 0。不正なら例外を投げ、状態は変えない。</param>
        public void ApplyDestroyedBitset(ulong[] bitset)
        {
            var integrity = Integrity;
            var before = new bool[_pieces.Length];
            for (var i = 0; i < before.Length; i++)
            {
                before[i] = integrity.IsDestroyed(i);
            }

            integrity.ApplyDestroyedSet(bitset);

            var broken = new List<int>();
            var restored = new List<int>();
            for (var i = 0; i < _pieces.Length; i++)
            {
                var now = integrity.IsDestroyed(i);
                if (now == before[i])
                {
                    continue;
                }

                if (now)
                {
                    broken.Add(i);
                    // 同期では直接壊れたか崩落したかが分からないので、どれも小さな破片にする。
                    BreakPiece(i, null, collapsed: false);
                }
                else
                {
                    restored.Add(i);
                    if (_pieces[i] != null)
                    {
                        _pieces[i].SetBroken(false);
                    }
                }
            }

            if (broken.Count > 0 || restored.Count > 0)
            {
                Debug.Log(FormatSyncedLog(name, broken, restored), this);
            }
        }

        /// <summary>権威側で部品が壊れたときのログ。</summary>
        public static string FormatDestroyedLog(string structureName, IReadOnlyList<int> destroyed, IReadOnlyList<int> collapsed)
        {
            return $"{LogTag} structure={structureName} destroyed={FormatIds(destroyed)} collapsed={FormatIds(collapsed)}";
        }

        /// <summary><see cref="ApplyDestroyedBitset"/> で状態が変わったときのログ。</summary>
        public static string FormatSyncedLog(string structureName, IReadOnlyList<int> destroyed, IReadOnlyList<int> restored)
        {
            return $"{LogTag} structure={structureName} synced destroyed={FormatIds(destroyed)} restored={FormatIds(restored)}";
        }

        private static string FormatIds(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return "-";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(ids[i]);
            }

            return builder.ToString();
        }

        private bool TryEnsureBuilt()
        {
            if (_integrity != null)
            {
                return true;
            }

            if (_buildFailed)
            {
                return false;
            }

            try
            {
                Build();
                return true;
            }
            catch (Exception e)
            {
                _buildFailed = true;
                Debug.LogError($"{LogTag} DestructibleStructure '{name}' の部品の設定が不正です: {e.Message}", this);
                return false;
            }
        }

        private bool TryGetPieceIndex(Collider collider, out int index)
        {
            if (_pieceByCollider.TryGetValue(collider, out index))
            {
                return true;
            }

            // 部品の子に付いた別のコライダーに当たったときは、親の部品が受ける。
            var piece = collider.GetComponentInParent<DestructiblePiece>();
            if (piece != null && piece.Index >= 0 && piece.Index < _pieces.Length && _pieces[piece.Index] == piece)
            {
                index = piece.Index;
                return true;
            }

            index = -1;
            return false;
        }

        private DestructiblePiece[] CollectPieces()
        {
            var found = new List<DestructiblePiece>();
            foreach (var piece in GetComponentsInChildren<DestructiblePiece>(true))
            {
                // 入れ子になった別の建物の部品は、その建物のもの。
                if (piece.GetComponentInParent<DestructibleStructure>(true) == this)
                {
                    found.Add(piece);
                }
            }

            if (found.Count == 0)
            {
                throw new InvalidOperationException("no DestructiblePiece found under the structure");
            }

            var pieces = new DestructiblePiece[found.Count];
            foreach (var piece in found)
            {
                if (piece.Index < 0 || piece.Index >= pieces.Length)
                {
                    throw new InvalidOperationException(
                        $"piece '{piece.name}' has index {piece.Index} but indices must be in 0..{pieces.Length - 1}");
                }

                if (pieces[piece.Index] != null)
                {
                    throw new InvalidOperationException(
                        $"pieces '{pieces[piece.Index].name}' and '{piece.name}' share index {piece.Index}");
                }

                if (piece.Collider == null)
                {
                    throw new InvalidOperationException($"piece '{piece.name}' has no collider");
                }

                pieces[piece.Index] = piece;
            }

            return pieces;
        }

        private static StructureLayout CreateLayout(DestructiblePiece[] pieces)
        {
            var count = pieces.Length;
            var maxHealth = new int[count];
            var grounded = new bool[count];
            var bounds = new Bounds[count];
            var hasBounds = new bool[count];
            for (var i = 0; i < count; i++)
            {
                maxHealth[i] = pieces[i].MaxHealth;
                grounded[i] = pieces[i].Grounded;
                var collider = pieces[i].Collider;

                // 非アクティブなコライダーの範囲は原点の大きさ 0 の箱になり、原点付近の部品と誤って隣接してしまう。
                hasBounds[i] = collider.enabled && collider.gameObject.activeInHierarchy;
                bounds[i] = collider.bounds;
                bounds[i].Expand(AdjacencyMargin * 2f);
            }

            // 部品数は建物 1 つで多くても数百なので、全組を比べる。
            var adjacency = new List<(int A, int B)>();
            for (var a = 0; a < count; a++)
            {
                for (var b = a + 1; b < count; b++)
                {
                    if (hasBounds[a] && hasBounds[b] && bounds[a].Intersects(bounds[b]))
                    {
                        adjacency.Add((a, b));
                    }
                }
            }

            return new StructureLayout(maxHealth, grounded, adjacency);
        }

        private void BreakPiece(int index, Vector3? hitPoint, bool collapsed)
        {
            var piece = _pieces[index];
            if (piece == null)
            {
                return;
            }

            // 破片の大きさは、当たり判定を消す前のコライダーから求める。
            piece.GetBox(out var center, out var rotation, out var size);
            var material = piece.DebrisMaterial;
            piece.SetBroken(true);

            if (!_spawnDebris)
            {
                return;
            }

            var pool = ResolveDebrisPool();
            if (collapsed)
            {
                if (size.x > 0f && size.y > 0f && size.z > 0f)
                {
                    pool.Spawn(center, rotation, size, material, Vector3.zero);
                }

                return;
            }

            var shortest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            var edge = Mathf.Clamp(shortest * 0.5f, MinFragmentSize, MaxFragmentSize);
            var fragmentSize = new Vector3(edge, edge, edge);
            for (var i = 0; i < FragmentsPerDestroyedPiece; i++)
            {
                var offset = Vector3.Scale(size * 0.5f, new Vector3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-1f, 1f)));
                var position = center + rotation * offset;
                var away = hitPoint.HasValue ? position - hitPoint.Value : offset;
                var direction = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.up;
                var velocity = (direction + Vector3.up * 0.5f).normalized * FragmentSpeed;
                pool.Spawn(position, UnityEngine.Random.rotation, fragmentSize, material, velocity);
            }
        }

        private DebrisPool ResolveDebrisPool()
        {
            if (_debrisPool == null)
            {
                _debrisPool = DebrisPool.GetOrCreate();
            }

            return _debrisPool;
        }
    }
}
