using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 武器の一覧。<see cref="WeaponSpec.Id"/> で引ける。生成後は変わらない。
    /// </summary>
    public sealed class WeaponCatalog
    {
        private readonly Dictionary<string, WeaponSpec> _byId;

        /// <summary>定義順の武器。</summary>
        public IReadOnlyList<WeaponSpec> All { get; }

        /// <summary>武器の数。1 以上。</summary>
        public int Count => All.Count;

        /// <param name="specs">武器の定義。null 要素・Id の重複・空は不可。</param>
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

        /// <summary>id の武器があれば true と武器を返す。無い・null なら false と null。</summary>
        public bool TryGet(string id, out WeaponSpec spec)
        {
            if (id == null)
            {
                spec = null;
                return false;
            }

            return _byId.TryGetValue(id, out spec);
        }

        /// <summary>id の武器を返す。無ければ <see cref="KeyNotFoundException"/>。</summary>
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

        /// <summary>id の武器があれば true。null なら false。</summary>
        public bool Contains(string id)
        {
            return id != null && _byId.ContainsKey(id);
        }
    }
}
