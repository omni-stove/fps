using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 建物1つ分の部品の耐久値と、壊れたかどうか。<see cref="Layout"/> の隣接を支えとして、
    /// 地面から切り離された部品を崩落させる。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>壊れた部品の耐久値は 0。壊れた部品は支えにならない。</item>
    /// <item>崩落の計算をするのは <see cref="ApplyDamage"/> だけ。<see cref="ApplyDestroyedSet(IReadOnlyList{bool})"/> は
    /// 権威側で計算済みの結果を受け取るためのもので、受け取った状態をそのまま反映する。</item>
    /// </list>
    /// </remarks>
    public sealed class StructureIntegrity
    {
        private const int BitsPerWord = 64;

        public StructureLayout Layout { get; }

        /// <summary>壊れている部品の数。</summary>
        public int DestroyedCount { get; private set; }

        /// <summary><see cref="ToBitset"/> の要素数。部品数を 64 で割って切り上げた値。</summary>
        public int BitsetLength => (Layout.PieceCount + BitsPerWord - 1) / BitsPerWord;

        private readonly int[] _health;
        private readonly bool[] _destroyed;

        /// <param name="layout">部品の並び。すべての部品が最大耐久値・壊れていない状態から始まる。</param>
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

        /// <summary>部品の今の耐久値。壊れていれば 0。</summary>
        public int Health(int piece)
        {
            Layout.CheckPiece(piece);
            return _health[piece];
        }

        /// <summary>部品が壊れているか。</summary>
        public bool IsDestroyed(int piece)
        {
            Layout.CheckPiece(piece);
            return _destroyed[piece];
        }

        /// <summary>
        /// 部品に amount のダメージを与える。耐久値が 0 になったら壊し、地面に接した部品から
        /// 壊れていない隣接だけを通ってたどり着けなくなった部品をすべて崩落させる。
        /// </summary>
        /// <param name="piece">ダメージを受ける部品。</param>
        /// <param name="amount">ダメージ量。0 以下なら何もしない。</param>
        /// <returns>壊れた部品と崩落した部品。部品がすでに壊れていたか、amount が 0 以下なら <see cref="StructureDamageResult.None"/>。</returns>
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

        /// <summary>
        /// 同期で受け取った壊れた部品の集合をそのまま反映する。崩落の計算はしない。
        /// 壊れている部品は耐久値 0 に、壊れていた部品が壊れていないことになったら最大耐久値に戻す。
        /// 壊れていないままの部品の耐久値は変えない。
        /// </summary>
        /// <param name="destroyed">部品ごとに壊れているか。要素数は部品数と同じ。</param>
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

        /// <summary>
        /// <see cref="ToBitset"/> の形で受け取った壊れた部品の集合を反映する。
        /// 扱いは <see cref="ApplyDestroyedSet(IReadOnlyList{bool})"/> と同じ。
        /// </summary>
        /// <param name="bitset">部品 i が壊れていれば要素 i / 64 のビット i % 64 が立つ。要素数は <see cref="BitsetLength"/>。部品数以降のビットは 0。</param>
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

            var usedBits = _destroyed.Length % BitsPerWord;
            if (usedBits != 0 && (bitset[bitset.Length - 1] >> usedBits) != 0UL)
            {
                throw new ArgumentException($"bitset has bits set beyond piece {_destroyed.Length - 1}", nameof(bitset));
            }

            for (var i = 0; i < _destroyed.Length; i++)
            {
                SetDestroyed(i, (bitset[i / BitsPerWord] & (1UL << (i % BitsPerWord))) != 0UL);
            }
        }

        /// <summary>壊れている部品の集合。部品 i が壊れていれば要素 i / 64 のビット i % 64 が立つ。</summary>
        public ulong[] ToBitset()
        {
            var bitset = new ulong[BitsetLength];
            for (var i = 0; i < _destroyed.Length; i++)
            {
                if (_destroyed[i])
                {
                    bitset[i / BitsPerWord] |= 1UL << (i % BitsPerWord);
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
