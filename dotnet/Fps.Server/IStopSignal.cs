using System;

namespace Fps.Server
{
    /// <summary>終了の求め (SIGTERM など) を受け取る口。テストから求めを起こせるよう、OS のシグナルと分けておく。</summary>
    internal interface IStopSignal
    {
        IDisposable Subscribe(Action onStop);
    }
}
