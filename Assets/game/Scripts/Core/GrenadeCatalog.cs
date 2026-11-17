using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class GrenadeCatalog
    {
        // スナップショットが持ち主に送れる種類の数 (GrenadePouchSync.MaxTypes) に合わせる
        public const int MaxCarried = 16;

        public static readonly GrenadeCatalog Default = Of(GrenadeSpec.Default);

        public GrenadeCatalog(IEnumerable<GrenadeSpec> types, IEnumerable<string> carriedIds = null)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            var list = new List<GrenadeSpec>();
            foreach (var type in types)
            {
                if (type == null)
                {
                    throw new ArgumentException("types must not contain null", nameof(types));
                }

                if (IndexIn(list, type.Id) >= 0)
                {
                    throw new ArgumentException("grenade type " + type.Id + " is listed twice", nameof(types));
                }

                list.Add(type);
            }

            if (list.Count == 0)
            {
                throw new ArgumentException("types must have at least one grenade type", nameof(types));
            }

            var carried = new List<GrenadeSpec>();
            if (carriedIds == null)
            {
                carried.Add(list[0]);
            }
            else
            {
                foreach (var id in carriedIds)
                {
                    var at = IndexIn(list, id);
                    if (at < 0)
                    {
                        throw new ArgumentException("carried grenade type " + id + " is not one of the types", nameof(carriedIds));
                    }

                    if (IndexIn(carried, id) >= 0)
                    {
                        throw new ArgumentException("carried grenade type " + id + " is listed twice", nameof(carriedIds));
                    }

                    carried.Add(list[at]);
                }
            }

            if (carried.Count > MaxCarried)
            {
                throw new ArgumentException("at most " + MaxCarried + " grenade types can be carried", nameof(carriedIds));
            }

            Types = list.AsReadOnly();
            Carried = carried.AsReadOnly();
        }

        public IReadOnlyList<GrenadeSpec> Types { get; }

        public IReadOnlyList<GrenadeSpec> Carried { get; }

        public static GrenadeCatalog Of(GrenadeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return new GrenadeCatalog(new[] { spec });
        }

        public bool TryFind(string id, out GrenadeSpec spec)
        {
            var at = IndexIn(Types, id);
            spec = at >= 0 ? Types[at] : null;
            return spec != null;
        }

        private static int IndexIn(IReadOnlyList<GrenadeSpec> specs, string id)
        {
            for (var i = 0; i < specs.Count; i++)
            {
                if (string.Equals(specs[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
