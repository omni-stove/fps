using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 削れただけの部品の耐久値を戻さないのは、同期の形 (DestructibleStructure.ApplyDestroyedBitset) で戻すため。
    // 生きている人の体と重なる部品は、人を壁に埋めないよう戻さない。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DestructibleStructure))]
    public sealed class HordeFortification : MonoBehaviour
    {
        public const int DefaultBuildCost = HordeSpec.DefaultBuildCost;
        public const int DefaultRepairCostPerPiece = HordeSpec.DefaultRepairCostPerPiece;
        public const float DefaultInteractRadius = HordeSpec.DefaultInteractRadius;

        // 部品の箱を縮めて重なりを見るのは、隣に立っているだけの人を重なりとみなさないため。専用サーバーと同じ値
        public const float BlockCheckShrink = MapFortification.BlockCheckShrink;

        [SerializeField, Tooltip("始めから置かれているか。オフなら通貨を払って置く場所になる")]
        private bool _startsBuilt = true;

        [SerializeField, Min(0), Tooltip("置くときに払う通貨。HordeRunner に HordeDefinition があれば、始めるときにその値で置き換わる")]
        private int _buildCost = DefaultBuildCost;

        [SerializeField, Min(0), Tooltip("壊れた部品 1 つを直すときに払う通貨。HordeRunner に HordeDefinition があれば、始めるときにその値で置き換わる")]
        private int _repairCostPerPiece = DefaultRepairCostPerPiece;

        [SerializeField, Min(0.1f), Tooltip("この距離 (m) 以内にいる人が直す・置く操作をできる。HordeRunner に HordeDefinition があれば、始めるときにその値で置き換わる")]
        private float _interactRadius = DefaultInteractRadius;

        private readonly Collider[] _overlaps = new Collider[16];
        private DestructibleStructure _structure;
        private bool _prepared;
        private bool _built;

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

        public void ApplySpec(HordeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            BuildCost = spec.BuildCost;
            RepairCostPerPiece = spec.RepairCostPerPiece;
            InteractRadius = spec.InteractRadius;
        }

        public bool IsBuilt
        {
            get
            {
                EnsurePrepared();
                return _built;
            }
        }

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

        public float HorizontalDistanceTo(Vector3 position)
        {
            var offset = position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        public bool IsInReach(Vector3 position) => HorizontalDistanceTo(position) <= _interactRadius;

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
