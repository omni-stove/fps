using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IMenuNavigator
    {
        event Action ReturningToMenu;

        bool TakeTutorialRequest();

        bool ReturnToMenu(MenuReturnReason reason);
    }
}
