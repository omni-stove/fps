using System;

namespace Shooter.Core
{
    /// <summary>
    /// 近くで Interact したら何が起きるかの案内。どの物が出しているか (<see cref="Source"/>) と、何が起きるか (<see cref="Detail"/>) を持つ。
    /// 文にするのは UI で、<see cref="Detail"/> の型 (例: 拾う物の <see cref="PickupOutcome"/>) ごとに決める。
    /// </summary>
    public readonly struct InteractHint : IEquatable<InteractHint>
    {
        /// <param name="source">案内を出している物。出した物だけが自分の案内を消せるよう、見分けに使う。</param>
        /// <param name="detail">Interact したら何が起きるか。</param>
        /// <exception cref="ArgumentNullException">source か detail が null。</exception>
        public InteractHint(object source, object detail)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
        }

        /// <summary>案内を出している物。<c>default(InteractHint)</c> では null。</summary>
        public object Source { get; }

        /// <summary>Interact したら何が起きるか。<c>default(InteractHint)</c> では null。</summary>
        public object Detail { get; }

        public bool Equals(InteractHint other)
        {
            return ReferenceEquals(Source, other.Source) && Equals(Detail, other.Detail);
        }

        public override bool Equals(object obj)
        {
            return obj is InteractHint other && Equals(other);
        }

        public static bool operator ==(InteractHint left, InteractHint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InteractHint left, InteractHint right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Source != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Source) : 0, Detail);
        }
    }
}
