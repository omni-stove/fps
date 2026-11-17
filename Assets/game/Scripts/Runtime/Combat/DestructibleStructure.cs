using System;
using System.Collections.Generic;
using System.Text;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DestructibleStructure : MonoBehaviour, IStructureHittable
    {
        public const string LogTag = "[Destruction]";

        public const float AdjacencyMargin = 0.05f;

        public const int FragmentsPerDestroyedPiece = 4;

        public const float MaxFragmentSize = 0.3f;
        public const float MinFragmentSize = 0.05f;

        public const float FragmentSpeed = 3f;

        [SerializeField, Tooltip("弾で壊れるか。false の間は当たっても何もしない")]
        private bool _destructionEnabled;

        [SerializeField, Tooltip("ダメージを計算する側か。false (ネットのクライアント) ならダメージを無視し、同期された状態だけを反映する")]
        private bool _hasAuthority = true;

        [SerializeField, Tooltip("壊れたときに破片を出すか")]
        private bool _spawnDebris = true;

        [SerializeField, Tooltip("破片を出す先。空ならシーンのものを探すか作る")]
        private DebrisPool _debrisPool;

        [SerializeField, Tooltip("弾や爆発で壊したときの破片が、キャラクターに当たるとダメージを与えるか")]
        private bool _debrisCrushEnabled;

        [SerializeField, Min(0f), Tooltip("破片がこれ以下の速さ (m/s) で当たってもダメージを与えない")]
        private float _crushMinSpeed = DebrisCrushDamage.DefaultMinSpeed;

        [SerializeField, Min(0f), Tooltip("破片の運動量 1 kg·m/s あたりのダメージ")]
        private float _crushDamagePerMomentum = DebrisCrushDamage.DefaultDamagePerMomentum;

        [SerializeField, Min(0), Tooltip("破片 1 つが 1 回に与えるダメージの上限")]
        private int _crushMaxDamage = DebrisCrushDamage.DefaultMaxDamage;

        private StructureIntegrity _integrity;
        private DestructiblePiece[] _pieces = Array.Empty<DestructiblePiece>();
        private readonly Dictionary<Collider, int> _pieceByCollider = new Dictionary<Collider, int>();
        private bool _buildFailed;

        public event Action<StructureDamageResult> PiecesDestroyed;

        public bool DestructionEnabled
        {
            get => _destructionEnabled;
            set => _destructionEnabled = value;
        }

        public bool HasAuthority
        {
            get => _hasAuthority;
            set => _hasAuthority = value;
        }

        public bool SpawnDebris
        {
            get => _spawnDebris;
            set => _spawnDebris = value;
        }

        public DebrisPool DebrisPool
        {
            get => _debrisPool;
            set => _debrisPool = value;
        }

        // ApplyDestroyedBitset で出す破片に DebrisCrusher を付けないのは、ダメージを権威側だけが計算するため。
        public bool DebrisCrushEnabled
        {
            get => _debrisCrushEnabled;
            set => _debrisCrushEnabled = value;
        }

        public DebrisCrushDamage CrushDamage
        {
            get => new DebrisCrushDamage(_crushMinSpeed, _crushDamagePerMomentum, _crushMaxDamage);
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _crushMinSpeed = value.MinSpeed;
                _crushDamagePerMomentum = value.DamagePerMomentum;
                _crushMaxDamage = value.MaxDamage;
            }
        }

        // Awake が走らない Edit Mode でも使えるよう、初めて触れたときにも組み立てる。
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

        public IReadOnlyList<DestructiblePiece> Pieces
        {
            get
            {
                _ = Integrity;
                return Array.AsReadOnly(_pieces);
            }
        }

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

        // 部品の設定が不正で組み立てられないときに投げないのは、撃った側を止めないため。エラーを 1 度ログに出して以後は何もしない。
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

            var crush = ResolveCrush();
            foreach (var destroyed in result.Destroyed)
            {
                BreakPiece(destroyed, point, collapsed: false, crush: crush);
            }

            foreach (var collapsed in result.Collapsed)
            {
                BreakPiece(collapsed, point, collapsed: true, crush: crush);
            }

            Debug.Log(FormatDestroyedLog(name, result.Destroyed, result.Collapsed), this);
            PiecesDestroyed?.Invoke(result);
        }

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
                    BreakPiece(i, null, collapsed: false, crush: null);
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

        public static string FormatDestroyedLog(string structureName, IReadOnlyList<int> destroyed, IReadOnlyList<int> collapsed)
        {
            return $"{LogTag} structure={structureName} destroyed={FormatIds(destroyed)} collapsed={FormatIds(collapsed)}";
        }

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

        private Crush? ResolveCrush()
        {
            if (!_debrisCrushEnabled || !_spawnDebris)
            {
                return null;
            }

            try
            {
                return new Crush(CrushDamage, CombatEvents.CurrentAttribution);
            }
            catch (ArgumentOutOfRangeException e)
            {
                // 撃った側を止めないよう、破片を見た目だけにして続ける。
                Debug.LogError($"{LogTag} DestructibleStructure '{name}' の破片のダメージの設定が不正です: {e.Message}", this);
                return null;
            }
        }

        private void BreakPiece(int index, Vector3? hitPoint, bool collapsed, Crush? crush)
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
            var crushDamage = crush.HasValue ? crush.Value.Damage : null;
            var attribution = crush.HasValue ? crush.Value.Attribution : null;
            if (collapsed)
            {
                if (size.x > 0f && size.y > 0f && size.z > 0f)
                {
                    pool.Spawn(center, rotation, size, material, Vector3.zero, crushDamage, attribution, name);
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
                pool.Spawn(position, UnityEngine.Random.rotation, fragmentSize, material, velocity, crushDamage, attribution, name);
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

        private readonly struct Crush
        {
            public DebrisCrushDamage Damage { get; }

            public ShotAttribution? Attribution { get; }

            public Crush(DebrisCrushDamage damage, ShotAttribution? attribution)
            {
                Damage = damage;
                Attribution = attribution;
            }
        }
    }
}
