using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AircraftGroundSettings
    {
        public AircraftGroundSpec ToSpec()
        {
            return new AircraftGroundSpec();
        }

        public void CopyFrom(AircraftGroundSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
        }
    }
}
