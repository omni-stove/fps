using System;
using Epic.OnlineServices;

namespace Shooter.NetEos
{
    // 期限切れの入り直しで、Device ID と Epic アカウントのどちらで入ったかを呼ぶ側が気にせずに済むようにする。
    internal interface IEosLogin
    {
        void Begin(Action<ProductUserId, string> done);

        void Cancel();
    }
}
