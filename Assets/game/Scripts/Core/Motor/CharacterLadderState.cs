using System;

namespace Fps.Core
{
    /// <summary>
    /// はしごの上り下りの中身 (<see cref="LadderClimb"/>) と、離れてから次につかまれるまでの待ち。はしごはマップのはしごの並び
    /// (<see cref="CharacterMotorSimulation.Ladders"/>) の番号で指す。
    /// </summary>
    public readonly struct CharacterLadderState : IEquatable<CharacterLadderState>
    {
        public const int NoLadder = -1;

        // 0 を「つかまっていない」にして、default がつかまっていない状態になるようにする
        private readonly int _slot;

        private CharacterLadderState(
            int index, LadderEntry entry, float height, bool isInputLatched, int latchedDownSign, float regrabRemaining)
        {
            _slot = index + 1;
            Entry = entry;
            Height = height;
            IsInputLatched = isInputLatched;
            LatchedDownSign = latchedDownSign;
            RegrabRemaining = regrabRemaining;
        }

        public static CharacterLadderState None => default;

        public int Index => _slot - 1;

        public bool IsClimbing => _slot > 0;

        public LadderEntry Entry { get; }

        /// <summary>足元の、はしごの下端からの高さ (m)。</summary>
        public float Height { get; }

        public bool IsInputLatched { get; }

        public int LatchedDownSign { get; }

        /// <summary>次につかまれるまでの残りの秒数。</summary>
        public float RegrabRemaining { get; }

        public bool IsValid => IsFinite(Height) && IsFinite(RegrabRemaining) && RegrabRemaining >= 0f
            && LatchedDownSign >= -1 && LatchedDownSign <= 1
            && (!IsClimbing || Entry == LadderEntry.Front || Entry == LadderEntry.Top);

        public static CharacterLadderState Climbing(
            int index, LadderEntry entry, float height, bool isInputLatched, int latchedDownSign)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must not be negative");
            }

            if (entry != LadderEntry.Front && entry != LadderEntry.Top)
            {
                throw new ArgumentOutOfRangeException(nameof(entry), entry, "entry must be Front or Top");
            }

            if (!IsFinite(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be finite");
            }

            if (latchedDownSign < -1 || latchedDownSign > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(latchedDownSign), latchedDownSign, "latchedDownSign must be -1, 0 or 1");
            }

            return new CharacterLadderState(index, entry, height, isInputLatched, latchedDownSign, 0f);
        }

        public static CharacterLadderState Off(float regrabRemaining)
        {
            if (!(regrabRemaining >= 0f) || float.IsInfinity(regrabRemaining))
            {
                throw new ArgumentOutOfRangeException(nameof(regrabRemaining), regrabRemaining, "regrabRemaining must be finite and not negative");
            }

            return new CharacterLadderState(NoLadder, LadderEntry.None, 0f, false, 0, regrabRemaining);
        }

        public bool Equals(CharacterLadderState other)
        {
            return _slot == other._slot
                && Entry == other.Entry
                && Height.Equals(other.Height)
                && IsInputLatched == other.IsInputLatched
                && LatchedDownSign == other.LatchedDownSign
                && RegrabRemaining.Equals(other.RegrabRemaining);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterLadderState other && Equals(other);
        }

        public static bool operator ==(CharacterLadderState left, CharacterLadderState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterLadderState left, CharacterLadderState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_slot * 397) ^ ((int)Entry << 4) ^ Height.GetHashCode() ^ (IsInputLatched ? 8 : 0)
                    ^ (LatchedDownSign << 6) ^ RegrabRemaining.GetHashCode();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
