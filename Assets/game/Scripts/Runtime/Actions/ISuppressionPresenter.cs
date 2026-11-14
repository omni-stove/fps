using Fps.Core;

namespace Fps.Runtime
{
    // 出し入れの合図を分けず毎フレーム度合いを渡すのは、演出ごとに上がり方・抜け方 (フェードなど) を自由に決められるようにするため。
    // 外すときと Off にしたときは 0 を渡すので、0 で何も出さなくなればよい。
    public interface ISuppressionPresenter
    {
        void Present(float level, SuppressionSpec spec);
    }
}
