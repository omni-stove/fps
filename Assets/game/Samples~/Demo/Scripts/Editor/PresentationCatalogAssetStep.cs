namespace Fps.Editor
{
    /// <summary>
    /// 武器の音とエフェクトの仮素材と <see cref="Fps.Runtime.PresentationCatalog"/> を作る手順 (<see cref="PresentationAssetBuilder.Build"/>)。
    /// 粒の材質とテクスチャ (<see cref="PresentationAssetBuilder.PresentationFolder"/>) もここで作るので、それを借りる手順より先に実行する。
    /// </summary>
    public sealed class PresentationCatalogAssetStep : IPresentationAssetStep
    {
        /// <summary>この手順の <see cref="Order"/>。ほかの手順はこれより大きくする。</summary>
        public const int StepOrder = 0;

        public string Name => "PresentationCatalog";

        public int Order => StepOrder;

        public void Build()
        {
            PresentationAssetBuilder.Build();
        }
    }
}
