namespace Shooter.Runtime
{
    // 速さは演出の側が本体の位置の差から測るので、ここには持たせない。
    public interface ICharacterMotionSource
    {
        bool IsGrounded { get; }

        bool IsCrouched { get; }

        float WalkSpeed { get; }

        bool TryGetSprinting(out bool sprinting);
    }
}
