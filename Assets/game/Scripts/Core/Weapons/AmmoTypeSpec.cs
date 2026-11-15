namespace Fps.Core
{
    public sealed class AmmoTypeSpec
    {
        public AmmoTypeSpec(string id, string nameKey, RoundSpec round = null, ImpactSpec impact = null)
        {
            Id = SpecGuard.LowercaseUuid(id, nameof(id));
            NameKey = SpecGuard.NameKey(nameKey);
            Round = round;
            Impact = impact;
        }

        public string Id { get; }

        public string NameKey { get; }

        public LocalizedText Name => NameKey.Length > 0 ? new LocalizedText(NameKey) : LocalizedText.Literal(Id);

        public RoundSpec Round { get; }

        public ImpactSpec Impact { get; }

        public RoundSpec RoundOr(RoundSpec weaponRound)
        {
            return Round ?? weaponRound;
        }

        public ImpactSpec ImpactOr(ImpactSpec weaponImpact)
        {
            return Impact ?? weaponImpact;
        }
    }
}
