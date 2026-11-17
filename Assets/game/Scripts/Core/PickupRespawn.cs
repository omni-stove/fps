using System;

namespace Shooter.Core
{
    public sealed class PickupRespawn
    {
        private float _remaining;

        public PickupRespawn(float respawnSeconds)
        {
            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must be a non-negative finite number");
            }

            RespawnSeconds = respawnSeconds;
            IsAvailable = true;
        }

        public float RespawnSeconds { get; }

        public bool Respawns => RespawnSeconds > 0f;

        public bool IsAvailable { get; private set; }

        public float? RemainingSeconds => !IsAvailable && Respawns ? _remaining : (float?)null;

        public bool Collect()
        {
            if (!IsAvailable)
            {
                return false;
            }

            IsAvailable = false;
            _remaining = RespawnSeconds;
            return true;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (IsAvailable || !Respawns)
            {
                return false;
            }

            _remaining -= deltaSeconds;
            if (_remaining > 0f)
            {
                return false;
            }

            _remaining = 0f;
            IsAvailable = true;
            return true;
        }
    }
}
