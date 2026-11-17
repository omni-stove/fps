using Shooter.Core;

namespace Shooter.Runtime
{
    // Runtime は通信の手順を参照できない (参照される側) ので、手順の側 (Shooter.NetV2) から GameUi.OnlineStarter に登録させる。
    public interface IOnlineMatchStarter
    {
        bool Start(MenuDestination destination, out MenuReturnReason refusal);
    }
}
