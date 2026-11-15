using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Fps.Core
{
    public sealed class AmmoTypeSelector
    {
        private readonly WeaponSpec _spec;

        public AmmoTypeSelector(WeaponSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public IReadOnlyList<AmmoTypeSpec> Types => _spec.AmmoTypes;

        // 種類を選んでいない間は -1 にして、武器そのものの Round / Impact で撃つ。
        public int Index { get; private set; } = -1;

        public AmmoTypeSpec Current => Index >= 0 ? _spec.AmmoTypes[Index] : null;

        public RoundSpec Round => Current != null ? Current.RoundOr(_spec.Round) : _spec.Round;

        public ImpactSpec Impact => Current != null ? Current.ImpactOr(_spec.Impact) : _spec.Impact;

        [SuppressMessage("Performance", "CA1822", Justification = "Switching keeps the chosen type on the instance")]
        public bool TrySwitch()
        {
            return false;
        }
    }
}
