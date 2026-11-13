using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="DodgeMotion"/> の中身 1 回分 (<see cref="DodgeMotion.Capture"/> で写し、<see cref="DodgeMotion.Restore"/> で戻す)。
    /// ネット対戦の予測で過去の tick へ巻き戻して同じ入力を再シミュレーションするために、転がりの進みと待ち時間を含める。
    /// <c>default</c> は転がっておらず、待ち時間も無い。
    /// </summary>
    public readonly struct DodgeSnapshot : IEquatable<DodgeSnapshot>
    {
        /// <param name="isRolling">転がっている最中か。</param>
        /// <param name="direction">転がっている向き (ワールドの水平の単位ベクトル)。転がっていなければ使わない。</param>
        /// <param name="elapsed">今の転がりを始めてからの秒数。転がっていなければ使わない。</param>
        /// <param name="cooldownRemaining">次を始められるまでの残りの秒数。</param>
        public DodgeSnapshot(bool isRolling, Vector2 direction, float elapsed, float cooldownRemaining)
        {
            IsRolling = isRolling;
            Direction = direction;
            Elapsed = elapsed;
            CooldownRemaining = cooldownRemaining;
        }

        /// <summary>転がっておらず、待ち時間も無い。</summary>
        public static DodgeSnapshot Ready => default;

        public bool IsRolling { get; }

        /// <summary><see cref="DodgeMotion.Direction"/>。</summary>
        public Vector2 Direction { get; }

        /// <summary><see cref="DodgeMotion.Elapsed"/>。</summary>
        public float Elapsed { get; }

        /// <summary><see cref="DodgeMotion.CooldownRemaining"/>。</summary>
        public float CooldownRemaining { get; }

        /// <summary>
        /// <see cref="DodgeMotion.Restore"/> に渡せる値か。数値がすべて有限で、秒数が負でなく、転がっている最中なら向きが 0 でない。
        /// ネットワークから受け取った値の検査に使う。
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (!IsFinite(Direction.X) || !IsFinite(Direction.Y) || !IsFinite(Elapsed) || !IsFinite(CooldownRemaining))
                {
                    return false;
                }

                if (Elapsed < 0f || CooldownRemaining < 0f)
                {
                    return false;
                }

                return !IsRolling || Direction.LengthSquared() > 0f;
            }
        }

        public bool Equals(DodgeSnapshot other)
        {
            return IsRolling == other.IsRolling
                && Direction.Equals(other.Direction)
                && Elapsed.Equals(other.Elapsed)
                && CooldownRemaining.Equals(other.CooldownRemaining);
        }

        public override bool Equals(object obj)
        {
            return obj is DodgeSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsRolling, Direction, Elapsed, CooldownRemaining);
        }

        public static bool operator ==(DodgeSnapshot left, DodgeSnapshot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DodgeSnapshot left, DodgeSnapshot right)
        {
            return !left.Equals(right);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
