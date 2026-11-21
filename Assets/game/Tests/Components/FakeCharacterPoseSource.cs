using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class FakeCharacterPoseSource : MonoBehaviour, ICharacterPoseSource
    {
        public float WalkSpeed { get; set; } = 5f;

        public float SprintSpeed { get; set; } = 8f;

        public float CrouchSpeed { get; set; } = 2.5f;

        public bool Crouching { get; set; }

        public bool Aiming { get; set; }

        public bool Downed { get; set; }

        public bool Dead { get; set; }

        public WeaponCategory? Weapon { get; set; }

        public float LastSpeed { get; private set; } = -1f;

        public CharacterPoseInput ReadPose(float horizontalSpeed)
        {
            LastSpeed = horizontalSpeed;
            return new CharacterPoseInput(
                horizontalSpeed,
                isGrounded: true,
                isCrouching: Crouching,
                isSprinting: horizontalSpeed > WalkSpeed,
                isAiming: Aiming,
                coverHeight: null,
                isDowned: Downed,
                isDead: Dead,
                weaponCategory: Weapon);
        }
    }
}
