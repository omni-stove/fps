using System;

namespace Shooter.NetV2
{
    // Shooter.NetV2.Client は Unity を参照しないので、測る実装 (UnityEngine.Ping を使う NetV2ServerPinger) はこの口で ServerBrowser に渡す。
    public interface IServerPinger : IDisposable
    {
        // address は IPv4 の文字。往復のミリ秒を、測れなければ null を、Tick の中で返す。
        void Ping(string address, Action<int?> done);

        void Tick();

        // 返していない測定を捨て、その呼び戻しを呼ばない。
        void CancelAll();
    }
}
