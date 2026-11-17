using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // どの部品とも地面ともつながっていない部品も作れるが、StructureIntegrity では最初にどこかの部品が壊れた時点で崩落する。
    public sealed class StructureLayout
    {
        public int PieceCount { get; }

        public IReadOnlyList<(int A, int B)> Adjacency { get; }

        private readonly int[] _maxHealth;
        private readonly bool[] _grounded;
        private readonly int[][] _neighbors;

        public StructureLayout(IReadOnlyList<int> maxHealth, IReadOnlyList<bool> grounded, IReadOnlyList<(int A, int B)> adjacency)
        {
            if (maxHealth == null)
            {
                throw new ArgumentNullException(nameof(maxHealth));
            }

            if (grounded == null)
            {
                throw new ArgumentNullException(nameof(grounded));
            }

            if (adjacency == null)
            {
                throw new ArgumentNullException(nameof(adjacency));
            }

            var count = maxHealth.Count;
            if (count == 0)
            {
                throw new ArgumentException("a structure must have at least one piece", nameof(maxHealth));
            }

            if (grounded.Count != count)
            {
                throw new ArgumentException($"grounded has {grounded.Count} entries but there are {count} pieces", nameof(grounded));
            }

            _maxHealth = new int[count];
            _grounded = new bool[count];
            for (var i = 0; i < count; i++)
            {
                if (maxHealth[i] < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth[i], $"maxHealth of piece {i} must be at least 1");
                }

                _maxHealth[i] = maxHealth[i];
                _grounded[i] = grounded[i];
            }

            var pairs = new (int A, int B)[adjacency.Count];
            var seen = new HashSet<(int, int)>();
            var neighbors = new List<int>[count];
            for (var i = 0; i < count; i++)
            {
                neighbors[i] = new List<int>();
            }

            for (var i = 0; i < adjacency.Count; i++)
            {
                var (a, b) = adjacency[i];
                if (a < 0 || a >= count || b < 0 || b >= count)
                {
                    throw new ArgumentOutOfRangeException(nameof(adjacency), adjacency[i], $"adjacency {i} refers to a piece outside 0..{count - 1}");
                }

                if (a == b)
                {
                    throw new ArgumentException($"adjacency {i} connects piece {a} to itself", nameof(adjacency));
                }

                var pair = a < b ? (a, b) : (b, a);
                if (!seen.Add(pair))
                {
                    throw new ArgumentException($"adjacency {i} duplicates the pair ({pair.Item1}, {pair.Item2})", nameof(adjacency));
                }

                pairs[i] = pair;
                neighbors[a].Add(b);
                neighbors[b].Add(a);
            }

            Array.Sort(pairs);

            _neighbors = new int[count][];
            for (var i = 0; i < count; i++)
            {
                neighbors[i].Sort();
                _neighbors[i] = neighbors[i].ToArray();
            }

            PieceCount = count;
            Adjacency = Array.AsReadOnly(pairs);
        }

        public int MaxHealth(int piece)
        {
            CheckPiece(piece);
            return _maxHealth[piece];
        }

        public bool IsGrounded(int piece)
        {
            CheckPiece(piece);
            return _grounded[piece];
        }

        public IReadOnlyList<int> Neighbors(int piece)
        {
            CheckPiece(piece);
            return Array.AsReadOnly(_neighbors[piece]);
        }

        internal void CheckPiece(int piece)
        {
            if (piece < 0 || piece >= PieceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(piece), piece, $"piece must be in 0..{PieceCount - 1}");
            }
        }

        // 呼び出し側で添字を検査済みの BFS 用。Neighbors() は呼ぶたびに包みを作るので避ける
        internal int[] NeighborsUnchecked(int piece) => _neighbors[piece];
    }
}
