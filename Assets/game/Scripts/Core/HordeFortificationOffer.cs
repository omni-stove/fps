namespace Shooter.Core
{
    public readonly struct HordeFortificationOffer
    {
        public HordeFortificationOffer(HordeFortificationAction action, string structureName, int cost, HordePurchaseVerdict verdict, string keyLabel)
        {
            Action = action;
            StructureName = structureName ?? string.Empty;
            Cost = cost;
            Verdict = verdict;
            KeyLabel = keyLabel ?? string.Empty;
        }

        public HordeFortificationAction Action { get; }

        public string StructureName { get; }

        public int Cost { get; }

        public HordePurchaseVerdict Verdict { get; }

        public string KeyLabel { get; }
    }
}
