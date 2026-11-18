using System;
using System.Collections.Generic;

namespace Shooter.Server.Eos
{
    // SDK の型を外に出さず文字列と StatIngest で受け渡すのは、SDK の無いビルドとテストでも StatsIngestQueue を動かせるようにするため。
    internal interface IStatsIngestBackend : IDisposable
    {
        void Tick();

        // done は (失敗の理由、もう一度送ってよいか) を 1 回だけ受け取る。成功なら理由は null。
        void Ingest(string productUserId, IReadOnlyList<StatIngest> stats, Action<string, bool> done);
    }
}
