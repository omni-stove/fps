using System;

namespace Shooter.Server
{
    // テストから終了の求めを起こせるよう、OS のシグナル (SIGTERM など) と分けておく。
    internal interface IStopSignal
    {
        IDisposable Subscribe(Action onStop);
    }
}
