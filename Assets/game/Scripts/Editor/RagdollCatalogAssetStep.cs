namespace Fps.Editor
{
    public sealed class RagdollCatalogAssetStep : IPresentationAssetStep
    {
        // 対応表はほかの手順の生成物を借りないが、PresentationCatalog の手順より前に走らせない決まりなのでそれより後にする。
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;

        public string Name => "RagdollCatalog";

        public int Order => StepOrder;

        public void Build()
        {
            RagdollCatalogBuilder.Build();
        }
    }
}
