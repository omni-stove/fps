using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class AircraftStickSettings
    {
        public AircraftStickSpec ToSpec()
        {
            return new AircraftStickSpec();
        }

        public void CopyFrom(AircraftStickSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
        }
    }
}
