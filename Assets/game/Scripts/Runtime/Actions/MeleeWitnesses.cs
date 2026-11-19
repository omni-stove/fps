using System;
using System.Collections.Generic;

namespace Shooter.Runtime
{
    public static class MeleeWitnesses
    {
        private static readonly List<IMeleeWitness> _active = new List<IMeleeWitness>();

        public static IReadOnlyList<IMeleeWitness> Active => _active;

        public static void Add(IMeleeWitness witness)
        {
            if (witness == null)
            {
                throw new ArgumentNullException(nameof(witness));
            }

            if (!_active.Contains(witness))
            {
                _active.Add(witness);
            }
        }

        public static bool Remove(IMeleeWitness witness)
        {
            return witness != null && _active.Remove(witness);
        }
    }
}
