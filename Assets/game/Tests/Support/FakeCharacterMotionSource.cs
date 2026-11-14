using Fps.Runtime;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    public sealed class FakeCharacterMotionSource : MonoBehaviour, ICharacterMotionSource
    {
        public bool IsGrounded { get; set; } = true;

        public bool IsCrouched { get; set; }

        public float WalkSpeed { get; set; } = 5f;

        public bool TryGetSprinting(out bool sprinting)
        {
            sprinting = false;
            return false;
        }
    }
}
