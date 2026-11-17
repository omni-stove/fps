using System;

namespace Fps.Core
{
    /// <summary>
    /// 弾薬箱の近くで出す案内の中身 (<see cref="InteractHint.Detail"/>)。補給できる武器があるか (<see cref="HasRoom"/>) と、
    /// どの入力 (<see cref="InputActionPath"/>) で使うかを持つ。文にするのは UI。
    /// </summary>
    public readonly struct AmmoCrateHint : IEquatable<AmmoCrateHint>
    {
        /// <param name="hasRoom">予備弾を足せる武器があるか。無ければどの武器も満タン。</param>
        /// <param name="inputActionPath">使う入力のアクション (例: <c>Player/Interact</c>)。null は空文字にする。</param>
        public AmmoCrateHint(bool hasRoom, string inputActionPath)
        {
            HasRoom = hasRoom;
            InputActionPath = inputActionPath ?? string.Empty;
        }

        public bool HasRoom { get; }

        public string InputActionPath { get; }

        public bool Equals(AmmoCrateHint other)
        {
            return HasRoom == other.HasRoom && string.Equals(InputActionPath, other.InputActionPath, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AmmoCrateHint other && Equals(other);
        }

        public static bool operator ==(AmmoCrateHint left, AmmoCrateHint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AmmoCrateHint left, AmmoCrateHint right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HasRoom, InputActionPath);
        }

        public override string ToString()
        {
            return $"ammoCrate room={HasRoom} input={InputActionPath}";
        }
    }
}
