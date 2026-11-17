using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class FakeCharacterMotorStateSource : MonoBehaviour, ICharacterMotorStateSource
    {
        public MotorSpec Spec { get; set; } = MotorSpec.Default;

        public bool HasState { get; set; } = true;

        public CharacterMotorState State { get; set; }

        public bool TryGetMotorState(out CharacterMotorState state)
        {
            state = State;
            return HasState;
        }

        public static CharacterMotorState Motor(float lean = 0f, bool swimming = false, WallRunSide wallRun = WallRunSide.None)
        {
            var wallNormal = wallRun == WallRunSide.Left
                ? new System.Numerics.Vector3(1f, 0f, 0f)
                : wallRun == WallRunSide.Right ? new System.Numerics.Vector3(-1f, 0f, 0f) : System.Numerics.Vector3.Zero;
            return new CharacterMotorState(
                System.Numerics.Vector3.Zero,
                System.Numerics.Vector3.Zero,
                !swimming && wallRun == WallRunSide.None,
                PlayerStance.Standing,
                CharacterCoverState.Out,
                DodgeSnapshot.Ready,
                CharacterVaultState.None,
                CharacterProneState.None,
                CharacterLadderState.None,
                CharacterKnockbackState.None,
                new CharacterLeanState(lean),
                new CharacterSwimState(swimming, 1f, false),
                new CharacterWallRunState(wallRun, wallRun == WallRunSide.None ? 0f : 0.3f, wallNormal),
                0);
        }
    }
}
