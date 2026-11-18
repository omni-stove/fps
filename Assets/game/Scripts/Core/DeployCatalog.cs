using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class DeployCatalog
    {
        private readonly Dictionary<string, DeployType> _byId = new Dictionary<string, DeployType>(StringComparer.Ordinal);

        public DeployCatalog(IEnumerable<DeployType> types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            var all = new List<DeployType>();
            foreach (var type in types)
            {
                if (type == null)
                {
                    throw new ArgumentException("types must not contain null", nameof(types));
                }

                if (!_byId.TryAdd(type.Id, type))
                {
                    throw new ArgumentException($"deployable type id '{type.Id}' is listed twice", nameof(types));
                }

                all.Add(type);
            }

            All = all.AsReadOnly();
        }

        // 置ける物はゲームごとの中身なので、既定では何も置けない。
        public static DeployCatalog Empty { get; } = new DeployCatalog(Array.Empty<DeployType>());

        public IReadOnlyList<DeployType> All { get; }

        public int Count => All.Count;

        public bool TryGet(string id, out DeployType type)
        {
            if (id == null)
            {
                type = null;
                return false;
            }

            return _byId.TryGetValue(id, out type);
        }
    }
}
