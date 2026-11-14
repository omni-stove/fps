using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>テスト用の <see cref="ICharacterPoseSource"/>。決めた状態を返し、渡された速さを記録する。</summary>
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

        /// <summary>最後の <see cref="ReadPose"/> に渡された速さ。</summary>
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
