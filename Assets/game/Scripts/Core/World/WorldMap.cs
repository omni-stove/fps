using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public sealed class WorldMap
    {
        private readonly WorldCollider[] _colliders;

        public WorldMap(IReadOnlyList<WorldCollider> colliders)
        {
            if (colliders == null)
            {
                throw new ArgumentNullException(nameof(colliders));
            }

            _colliders = new WorldCollider[colliders.Count];
            var ids = new HashSet<int>();
            for (var i = 0; i < _colliders.Length; i++)
            {
                var collider = colliders[i] ?? throw new ArgumentException("collider must not be null", nameof(colliders));
                if (!ids.Add(collider.Id))
                {
                    throw new ArgumentException("duplicate collider id " + collider.Id, nameof(colliders));
                }

                _colliders[i] = collider;
            }
        }

        public IReadOnlyList<WorldCollider> Colliders => _colliders;
    }
}
