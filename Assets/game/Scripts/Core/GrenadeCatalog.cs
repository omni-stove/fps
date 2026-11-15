using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>投げ物の種類の並び (<see cref="Types"/>) と、出撃のたびに持つ種類 (<see cref="Carried"/>)。</summary>
    public sealed class GrenadeCatalog
    {
        // スナップショットが持ち主に送れる種類の数 (GrenadePouchSync.MaxTypes) に合わせる
        public const int MaxCarried = 16;

        public static readonly GrenadeCatalog Default = Of(GrenadeSpec.Default);

        /// <param name="types">種類。1 つ以上で、Id が重ならない。</param>
        /// <param name="carriedIds">持つ種類の Id。null なら先頭の種類だけを持つ。空なら何も持たない。</param>
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
