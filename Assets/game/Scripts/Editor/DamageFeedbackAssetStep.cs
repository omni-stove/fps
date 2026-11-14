namespace Fps.Editor
{
    /// <summary>
    /// 被弾の画面効果と低体力の演出の仮素材と <see cref="Fps.Runtime.DamageFeedbackCatalog"/> を作る手順 (<see cref="DamageFeedbackAssetBuilder.Build"/>)。
    /// </summary>
    public sealed class DamageFeedbackAssetStep : IPresentationAssetStep
    {
        /// <summary>この手順の <see cref="Order"/>。</summary>
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;

        public string Name => "DamageFeedback";

        public int Order => StepOrder;

        public void Build()
        {
            DamageFeedbackAssetBuilder.Build();
        }
    }
}
