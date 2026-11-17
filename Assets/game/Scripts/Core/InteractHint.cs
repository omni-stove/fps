using System;

namespace Shooter.Core
{
    public readonly struct InteractHint : IEquatable<InteractHint>
    {
        public InteractHint(object source, object detail)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
        }

        // 出した物を持つのは、出した物だけが自分の案内を消せるよう見分けるため。
        public object Source { get; }

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
