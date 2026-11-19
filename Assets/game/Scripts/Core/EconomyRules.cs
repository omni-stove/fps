using System;

namespace Shooter.Core
{
    // 額 (EconomySpec) はネット対戦の設定 (NetMatchFile) と共有するので、オフラインだけで読む品の一覧と入れ替え時の扱いはこちらに束ねる。
    public sealed class EconomyRules
    {
        public const bool DefaultResetOnSwap = true;

        public static EconomyRules Default { get; } = new EconomyRules(EconomySpec.Default, BuyCatalog.Empty);

        public EconomyRules(EconomySpec spec, BuyCatalog catalog, bool resetOnSwap = DefaultResetOnSwap)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            ResetOnSwap = resetOnSwap;
        }

        public EconomySpec Spec { get; }

        public BuyCatalog Catalog { get; }

        public bool ResetOnSwap { get; }
    }
}
