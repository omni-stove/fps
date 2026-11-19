using System;

namespace Shooter.Core
{
    public sealed class BeaconState
    {
        private readonly BeaconSpec _spec;

        public BeaconState(BeaconSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            UsesLeft = spec.Uses;
        }

        public int UsesLeft { get; private set; }

        public int Uses => _spec.Uses;

        public DeployRemoval? Over => UsesLeft <= 0 ? DeployRemoval.Expended : (DeployRemoval?)null;

        public bool TryUse()
        {
            if (UsesLeft <= 0)
            {
                return false;
            }

            UsesLeft--;
            return true;
        }
    }
}
