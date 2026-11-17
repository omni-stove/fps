using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 崩落の計算をするのは ApplyDamage だけ。ApplyDestroyedSet は権威側で計算済みの結果を受け取るためのもので、
    // 受け取った状態をそのまま反映する。
    public sealed class StructureIntegrity
    {
        private const int _bitsPerWord = 64;

        public StructureLayout Layout { get; }

        public int DestroyedCount { get; private set; }

        public int BitsetLength => (Layout.PieceCount + _bitsPerWord - 1) / _bitsPerWord;

        private readonly int[] _health;
        private readonly bool[] _destroyed;

        public StructureIntegrity(StructureLayout layout)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _health = new int[layout.PieceCount];
            _destroyed = new bool[layout.PieceCount];
            for (var i = 0; i < _health.Length; i++)
            {
                _health[i] = layout.MaxHealth(i);
            }
        }

        public int Health(int piece)
        {
            Layout.CheckPiece(piece);
            return _health[piece];
        }

        public bool IsDestroyed(int piece)
        {
            Layout.CheckPiece(piece);
            return _destroyed[piece];
        }

        public StructureDamageResult ApplyDamage(int piece, int amount)
        {
            Layout.CheckPiece(piece);
            if (amount <= 0 || _destroyed[piece])
            {
                return StructureDamageResult.None;
            }

            if (amount < _health[piece])
            {
                _health[piece] -= amount;
                return StructureDamageResult.None;
            }

            Destroy(piece);
            return new StructureDamageResult(new[] { piece }, CollapseUnsupported());
        }

        public void ApplyDestroyedSet(IReadOnlyList<bool> destroyed)
        {
            if (destroyed == null)
            {
                throw new ArgumentNullException(nameof(destroyed));
            }

            if (destroyed.Count != _destroyed.Length)
            {
                throw new ArgumentException($"destroyed has {destroyed.Count} entries but there are {_destroyed.Length} pieces", nameof(destroyed));
            }

            for (var i = 0; i < _destroyed.Length; i++)
            {
                SetDestroyed(i, destroyed[i]);
            }
        }

        public void ApplyDestroyedSet(ulong[] bitset)
        {
            if (bitset == null)
            {
                throw new ArgumentNullException(nameof(bitset));
            }

            if (bitset.Length != BitsetLength)
            {
                throw new ArgumentException($"bitset has {bitset.Length} words but {BitsetLength} are needed for {_destroyed.Length} pieces", nameof(bitset));
            }

            var usedBits = _destroyed.Length % _bitsPerWord;
            if (usedBits != 0 && (bitset[bitset.Length - 1] >> usedBits) != 0UL)
            {
                throw new ArgumentException($"bitset has bits set beyond piece {_destroyed.Length - 1}", nameof(bitset));
            }

            for (var i = 0; i < _destroyed.Length; i++)
            {
                SetDestroyed(i, (bitset[i / _bitsPerWord] & (1UL << (i % _bitsPerWord))) != 0UL);
            }
        }

        public ulong[] ToBitset()
        {
            var bitset = new ulong[BitsetLength];
            for (var i = 0; i < _destroyed.Length; i++)
            {
                if (_destroyed[i])
                {
                    bitset[i / _bitsPerWord] |= 1UL << (i % _bitsPerWord);
                }
            }

            return bitset;
        }

        private void Destroy(int piece)
        {
            _health[piece] = 0;
            _destroyed[piece] = true;
            DestroyedCount++;
        }

        private void SetDestroyed(int piece, bool destroyed)
        {
            if (destroyed == _destroyed[piece])
            {
                return;
            }

            if (destroyed)
            {
                Destroy(piece);
                return;
            }

            _destroyed[piece] = false;
            _health[piece] = Layout.MaxHealth(piece);
            DestroyedCount--;
        }

        private int[] CollapseUnsupported()
        {
            var count = _destroyed.Length;
            var supported = new bool[count];
            var queue = new Queue<int>();
            for (var i = 0; i < count; i++)
            {
                if (!_destroyed[i] && Layout.IsGrounded(i))
                {
                    supported[i] = true;
                    queue.Enqueue(i);
                }
            }

            while (queue.Count > 0)
            {
                foreach (var next in Layout.NeighborsUnchecked(queue.Dequeue()))
                {
                    if (!_destroyed[next] && !supported[next])
                    {
                        supported[next] = true;
                        queue.Enqueue(next);
                    }
                }
            }

            var collapsed = new List<int>();
            for (var i = 0; i < count; i++)
            {
                if (!_destroyed[i] && !supported[i])
                {
                    collapsed.Add(i);
                }
            }

            foreach (var piece in collapsed)
            {
                Destroy(piece);
            }

            return collapsed.ToArray();
        }
    }
}
