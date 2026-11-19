using System;
using UnityEngine;

namespace Shooter.Runtime
{
    public readonly struct VehicleSeatInput
    {
        public static VehicleSeatInput Idle => default;

        public VehicleSeatInput(
            Vector2 move,
            float vertical,
            bool boost,
            bool fireHeld,
            bool aimHeld,
            bool reloadPressed,
            Vector3 aimOrigin,
            Vector3 aimDirection,
            bool countermeasurePressed = false)
        {
            if (!(Mathf.Abs(move.x) <= 1f && Mathf.Abs(move.y) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(move), move, "move components must be between -1 and 1");
            }

            if (!(Mathf.Abs(vertical) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(vertical), vertical, "vertical must be between -1 and 1");
            }

            if (!IsFinite(aimOrigin))
            {
                throw new ArgumentException($"aimOrigin must be finite: {aimOrigin}", nameof(aimOrigin));
            }

            if (!IsFinite(aimDirection))
            {
                throw new ArgumentException($"aimDirection must be finite: {aimDirection}", nameof(aimDirection));
            }

            Move = move;
            Vertical = vertical;
            Boost = boost;
            FireHeld = fireHeld;
            AimHeld = aimHeld;
            ReloadPressed = reloadPressed;
            AimOrigin = aimOrigin;
            AimDirection = aimDirection.sqrMagnitude > 1e-12f ? aimDirection.normalized : Vector3.zero;
            CountermeasurePressed = countermeasurePressed;
        }

        public Vector2 Move { get; }

        public float Vertical { get; }

        public bool Boost { get; }

        public bool FireHeld { get; }

        public bool AimHeld { get; }

        public bool ReloadPressed { get; }

        public Vector3 AimOrigin { get; }

        public Vector3 AimDirection { get; }

        // 押したフレームだけ true にするのは、押し続けてもフレア 1 回分しか撒かないようにするため。
        public bool CountermeasurePressed { get; }

        public bool HasAim => AimDirection != Vector3.zero;

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
