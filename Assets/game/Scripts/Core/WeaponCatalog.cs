using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class WeaponCatalog
    {
        private readonly Dictionary<string, WeaponSpec> _byId;

        public IReadOnlyList<WeaponSpec> All { get; }

        public int Count => All.Count;

        public WeaponCatalog(IEnumerable<WeaponSpec> specs)
        {
            if (specs == null)
            {
                throw new ArgumentNullException(nameof(specs));
            }

            var list = new List<WeaponSpec>();
            var byId = new Dictionary<string, WeaponSpec>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                if (spec == null)
                {
                    throw new ArgumentException("specs must not contain null (index " + list.Count + ")", nameof(specs));
                }

                if (byId.ContainsKey(spec.Id))
                {
                    throw new ArgumentException("duplicate weapon id '" + spec.Id + "'", nameof(specs));
                }

                byId.Add(spec.Id, spec);
                list.Add(spec);
            }

            if (list.Count == 0)
            {
                throw new ArgumentException("specs must not be empty", nameof(specs));
            }

            _byId = byId;
            All = list.AsReadOnly();
        }

        public bool TryGet(string id, out WeaponSpec spec)
        {
            if (id == null)
            {
                spec = null;
                return false;
            }

            return _byId.TryGetValue(id, out spec);
        }

        public WeaponSpec Get(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (!_byId.TryGetValue(id, out var spec))
            {
                throw new KeyNotFoundException("unknown weapon id '" + id + "'");
            }

            return spec;
        }

        public bool Contains(string id)
        {
            return id != null && _byId.ContainsKey(id);
        }
    }
}
