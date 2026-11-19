using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AircraftDamageSettings
    {
        public AircraftDamageSpec ToSpec()
        {
            return new AircraftDamageSpec();
        }

        public void CopyFrom(AircraftDamageSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
        }
    }
}
