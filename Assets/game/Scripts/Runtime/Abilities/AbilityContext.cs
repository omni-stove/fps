using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class AbilityContext
    {
        private AbilityMovementOverride _movement;

        internal AbilityContext(AbilityHost host, int slotIndex, AbilityDefinition definition, AbilitySlot slot)
        {
            Host = host;
            SlotIndex = slotIndex;
            Definition = definition;
            Slot = slot;
        }

        public AbilityHost Host { get; }

        public GameObject Owner => Host.gameObject;

        public PlayerController Mover => Host.Mover;

        public int SlotIndex { get; }

        public AbilityDefinition Definition { get; }

        public AbilitySlot Slot { get; }

        public AbilityPhase Phase => Slot.Phase;

        public bool IsEffective => Slot.Phase == AbilityPhase.Active;

        public bool EffectStartedThisFrame { get; internal set; }

        public bool IsHeld => (Host.HeldCommands & AbilityHost.CommandFor(SlotIndex)) != 0;

        public bool IsOverridingMovement => _movement != null;

        public bool OverrideMovement(IMovementOverride movement)
        {
            if (movement == null)
            {
                throw new ArgumentNullException(nameof(movement));
            }

            if (!Slot.IsRunning)
            {
                throw new InvalidOperationException("the ability can only override movement while it is running");
            }

            StopMovementOverride();
            var mover = Mover;
            if (mover == null)
            {
                return false;
            }

            _movement = new AbilityMovementOverride(movement);
            mover.RegisterMovementOverride(_movement);
            return true;
        }

        public void StopMovementOverride()
        {
            var movement = _movement;
            if (movement == null)
            {
                return;
            }

            _movement = null;
            var mover = Mover;
            if (mover != null)
            {
                mover.UnregisterMovementOverride(movement);
            }
        }
    }
}
