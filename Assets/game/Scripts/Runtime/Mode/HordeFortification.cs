using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ホードで通貨を払って直す・置く防衛物。同じ GameObject の <see cref="DestructibleStructure"/> の部品を、壊れていない状態に戻すことで直す。
    /// まだ置かれていない防衛物 (<see cref="StartsBuilt"/> が false) は、始めに全部品を壊れた状態 (見えず、当たらず、道を塞がない) にしておき、
    /// 置くときに全部品を戻す。払うかどうかの判断と入力は <see cref="HordeRunner"/> が行う。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>直すのは壊れた部品だけ。削れただけの部品の耐久値は戻さない (同期の形 <see cref="DestructibleStructure.ApplyDestroyedBitset"/> で戻すため)。</item>
    /// <item>生きている人 (<see cref="Damageable"/>) の体と重なる部品は戻さない (人を壁に埋めないため)。その分は値段に入れない。</item>
    /// <item>近さは、この GameObject の位置からの水平の距離で測る。防衛物の真ん中に置くこと。</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DestructibleStructure))]
    public sealed class HordeFortification : MonoBehaviour
    {
        public const int DefaultBuildCost = 40;
        public const int DefaultRepairCostPerPiece = 4;
        public const float DefaultInteractRadius = 3.5f;

        /// <summary>部品と人の体が重なるかを見るとき、部品の箱をこの割合に縮める (隣に立っているだけの人を重なりとみなさないため)。</summary>
        public const float BlockCheckShrink = 0.9f;

        [SerializeField, Tooltip("始めから置かれているか。オフなら通貨を払って置く場所になる")]
        private bool _startsBuilt = true;

        [SerializeField, Min(0), Tooltip("置くときに払う通貨")]
        private int _buildCost = DefaultBuildCost;

        [SerializeField, Min(0), Tooltip("壊れた部品 1 つを直すときに払う通貨")]
        private int _repairCostPerPiece = DefaultRepairCostPerPiece;

        [SerializeField, Min(0.1f), Tooltip("この距離 (m) 以内にいる人が直す・置く操作をできる")]
        private float _interactRadius = DefaultInteractRadius;

        private readonly Collider[] _overlaps = new Collider[16];
        private DestructibleStructure _structure;
        private bool _prepared;
        private bool _built;

        /// <summary>壊れる部品を持つ建物。</summary>
        public DestructibleStructure Structure
        {
            get
            {
                if (_structure == null)
                {
                    _structure = GetComponent<DestructibleStructure>();
                }

                return _structure;
            }
        }

        /// <summary>始めから置かれているか。始める (<see cref="EnsurePrepared"/>) 前に設定する。</summary>
        public bool StartsBuilt
        {
            get => _startsBuilt;
            set => _startsBuilt = value;
        }

        public int BuildCost
        {
            get => _buildCost;
            set => _buildCost = Math.Max(0, value);
        }

        public int RepairCostPerPiece
        {
            get => _repairCostPerPiece;
            set => _repairCostPerPiece = Math.Max(0, value);
        }

        public float InteractRadius
        {
            get => _interactRadius;
            set => _interactRadius = Mathf.Max(0.1f, value);
        }

        /// <summary>置かれていれば true。置かれていない防衛物は <see cref="Build"/> の操作で置く。</summary>
        public bool IsBuilt
        {
            get
            {
                EnsurePrepared();
                return _built;
            }
        }

        /// <summary>今できること。置かれていなければ置く、壊れた部品があれば直す、どちらでもなければ何もしない。</summary>
        public HordeFortificationAction PendingAction
        {
            get
            {
                EnsurePrepared();
                if (!_built)
                {
                    return HordeFortificationAction.Build;
                }

                return Structure.Integrity.DestroyedCount > 0 ? HordeFortificationAction.Repair : HordeFortificationAction.None;
            }
        }

        private void Start()
        {
            EnsurePrepared();
        }

        /// <summary>
        /// 始めの状態にする。置かれていない防衛物は全部品を壊れた状態にする (破片は出さない)。2 度目からは何もしない。
        /// Play 中は Start で呼ばれる。Edit Mode では使う前に呼ぶか、ほかのメンバーに触れれば呼ばれる。
        /// </summary>
        public void EnsurePrepared()
        {
            if (_prepared)
            {
                return;
            }

            _prepared = true;
            _built = _startsBuilt;
            if (_built)
            {
                return;
            }

            var structure = Structure;
            var integrity = structure.Integrity;
            var bitset = new ulong[integrity.BitsetLength];
            for (var i = 0; i < integrity.Layout.PieceCount; i++)
            {
                bitset[i / 64] |= 1UL << (i % 64);
            }

            // まだ置かれていないだけなので、壊れたときの破片は出さない。
            var spawnDebris = structure.SpawnDebris;
            structure.SpawnDebris = false;
            try
            {
                structure.ApplyDestroyedBitset(bitset);
            }
            finally
            {
                structure.SpawnDebris = spawnDebris;
            }
        }

        /// <summary>position からこの防衛物までの水平の距離 (m)。</summary>
        public float HorizontalDistanceTo(Vector3 position)
        {
            var offset = position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        /// <summary>position が操作できる距離 (<see cref="InteractRadius"/>) 以内なら true。</summary>
        public bool IsInReach(Vector3 position) => HorizontalDistanceTo(position) <= _interactRadius;

        /// <summary>
        /// 今戻せる部品 (壊れていて、生きている人の体と重ならない部品) の添字を昇順で返す。
        /// </summary>
        public IReadOnlyList<int> RestorablePieces()
        {
            EnsurePrepared();
            var structure = Structure;
            var integrity = structure.Integrity;
            var pieces = structure.Pieces;
            var restorable = new List<int>();

            // 人を動かした直後でも重なりを正しく見るため。
            Physics.SyncTransforms();
            for (var i = 0; i < pieces.Count; i++)
            {
                if (integrity.IsDestroyed(i) && !IsBlocked(pieces[i]))
                {
                    restorable.Add(i);
                }
            }

            return restorable;
        }

        /// <summary>action で pieceCount 個の部品を戻すときに払う通貨。何もしないなら 0。</summary>
        public int CostOf(HordeFortificationAction action, int pieceCount)
        {
            if (pieceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceCount), pieceCount, "pieceCount must not be negative");
            }

            switch (action)
            {
                case HordeFortificationAction.Build:
                    return _buildCost;
                case HordeFortificationAction.Repair:
                    return (int)Math.Min(int.MaxValue, (long)_repairCostPerPiece * pieceCount);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// action (置く・直す) として pieces の部品を壊れていない状態に戻し、戻した数を返す。置いたら <see cref="IsBuilt"/> は true になる。
        /// 通貨は払わない (<see cref="HordeRunner"/> が払ってから呼ぶ)。action が None なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public int Restore(HordeFortificationAction action, IReadOnlyList<int> pieces)
        {
            if (action != HordeFortificationAction.Build && action != HordeFortificationAction.Repair)
            {
                throw new ArgumentOutOfRangeException(nameof(action), action, "only Build and Repair restore pieces");
            }

            if (pieces == null)
            {
                throw new ArgumentNullException(nameof(pieces));
            }

            EnsurePrepared();
            var structure = Structure;
            var integrity = structure.Integrity;
            var bitset = integrity.ToBitset();
            var restored = 0;
            foreach (var piece in pieces)
            {
                if (integrity.IsDestroyed(piece))
                {
                    bitset[piece / 64] &= ~(1UL << (piece % 64));
                    restored++;
                }
            }

            if (restored > 0)
            {
                structure.ApplyDestroyedBitset(bitset);
            }

            if (action == HordeFortificationAction.Build)
            {
                _built = true;
            }

            return restored;
        }

        private bool IsBlocked(DestructiblePiece piece)
        {
            if (piece == null)
            {
                return false;
            }

            piece.GetBox(out var center, out var rotation, out var size);
            var count = Physics.OverlapBoxNonAlloc(
                center, size * (BlockCheckShrink / 2f), _overlaps, rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var damageable = _overlaps[i].GetComponentInParent<Damageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
