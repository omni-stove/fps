namespace Shooter.Editor
{
    public sealed class PresentationCatalogAssetStep : IPresentationAssetStep
    {
        // 粒の材質とテクスチャ (PresentationAssetBuilder.PresentationFolder) もこの手順で作るので、それを借りるほかの手順はこれより大きくする。
        public const int StepOrder = 0;

        public string Name => "PresentationCatalog";

        public int Order => StepOrder;

        public void Build()
        {
            PresentationAssetBuilder.Build();
        }
    }
}
