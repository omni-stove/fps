using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 一覧は GameSignals.ServerBrowser で受け取り、行は ServerListing.Id で選ぶ。
    public interface IServerBrowserControl
    {
        // 開くときも読み直すときもこれを呼ぶ。読んでいる途中や入る途中は false で断る。
        bool Refresh();

        // Task でなく呼び戻しにするのは、IJoinByCode と同じくキットの非同期の口がどれも呼び戻しで、メインスレッドで返す約束をそろえるため。
        void Join(string serverId, Action<ServerJoinOutcome> done);

        void Close();
    }
}
