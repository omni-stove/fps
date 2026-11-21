using Shooter.Core;

namespace Shooter.Runtime
{
    // Shooter.Runtime はネット対戦のアセンブリを参照できないので、ネット対戦の本体の状態はこの形で受け取る。
    // 速度は全員には同期されないため、水平の速さは CharacterAnimator が本体の位置の変化から求めて渡す。
    public interface ICharacterPoseSource
    {
        float WalkSpeed { get; }

        float SprintSpeed { get; }

        float CrouchSpeed { get; }

        CharacterPoseInput ReadPose(float horizontalSpeed);
    }
}
