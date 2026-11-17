using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public interface IPlayerActionInstaller
    {
        int Order { get; }

        // rules で使わないことになっていても呼ぶのは、前に有効にしたものを無効に戻すため。
        void Install(GameObject player, GameRules rules);
    }
}
