using Fps.Core;

namespace Fps.Runtime
{
    // 見た目を差し替えるときは、これを実装したコンポーネントを根に持つプレハブを AwarenessIndicatorCatalog に入れる。
    public interface IAwarenessIndicator
    {
        // 段階が変わったときだけでなく毎フレーム呼ぶ。ゲージの量で満ちていく見た目にもできるように。
        void Show(AwarenessStage stage, float level);
    }
}
