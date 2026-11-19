using Shooter.Core;

namespace Shooter.Runtime
{
    // 乗る側 (PlayerController) が MovingPlatform と台車 (PayloadCart) を同じ手順で運ばれるための口。
    // 動かし方は床ごとに違うので、乗る側には最後の動きとその番号だけを見せる。
    public interface IRideablePlatform
    {
        // 乗る側はこの数で新しい動きを見分け、同じ動きを 2 度運ばない。
        int StepCount { get; }

        PlatformMotion LastMotion { get; }

        float LastStepSeconds { get; }

        // 乗っている体が頭上に塞がれた。運ぶ側は読み終えた動きを誰にも運ばせないよう StepCount を進める。
        void Obstruct();

        void Board(object rider);

        void Leave(object rider);
    }
}
