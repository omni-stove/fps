using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IJoinByCode
    {
        // Task でなく呼び戻しにするのは、キットの非同期の口 (ILobbyBackend など) がどれも呼び戻しで、メインスレッドで返す約束をそろえるため。
        void Join(string code, Action<JoinCodeOutcome> done);
    }
}
