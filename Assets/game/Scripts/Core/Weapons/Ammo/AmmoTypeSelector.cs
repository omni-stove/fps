using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class AmmoTypeSelector
    {
        private readonly WeaponSpec _spec;

        public AmmoTypeSelector(WeaponSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Index = spec.AmmoTypes.Count > 0 ? 0 : -1;
        }

        public IReadOnlyList<AmmoTypeSpec> Types => _spec.AmmoTypes;

        // 種類を持たない武器は -1 にして、武器そのものの Round / Impact で撃つ。種類を持つ武器は種類を選ばずには撃たないので、0 番から始める。
        public int Index { get; private set; }

        public AmmoTypeSpec Current => Index >= 0 ? _spec.AmmoTypes[Index] : null;

        public RoundSpec Round => Current != null ? Current.RoundOr(_spec.Round) : _spec.Round;

        public ImpactSpec Impact => Current != null ? Current.ImpactOr(_spec.Impact) : _spec.Impact;

        // 武器を作り直すときに、込めてある弾の種類を保つためだけに使う。弾倉には触れないので、切り替え (TrySwitch) とは別にしてある。
        public bool TrySelect(int index)
        {
            if (index < 0 || index >= _spec.AmmoTypes.Count)
            {
                return false;
            }

            Index = index;
            return true;
        }

        public bool TrySwitch()
        {
            var count = _spec.AmmoTypes.Count;
            if (count < 2)
            {
                return false;
            }

            Index = (Index + 1) % count;
            return true;
        }
    }
}
