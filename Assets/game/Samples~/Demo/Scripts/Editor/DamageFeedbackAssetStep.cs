namespace Shooter.Editor
{
    public sealed class DamageFeedbackAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;

        public string Name => "DamageFeedback";

        public int Order => StepOrder;

        public void Build()
        {
            DamageFeedbackAssetBuilder.Build();
        }
    }
}
