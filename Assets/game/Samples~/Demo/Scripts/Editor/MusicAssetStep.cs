namespace Shooter.Editor
{
    public sealed class MusicAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = DamageFeedbackAssetStep.StepOrder + 10;

        public string Name => "Music";

        public int Order => StepOrder;

        public void Build()
        {
            MusicAssetBuilder.Build();
        }
    }
}
