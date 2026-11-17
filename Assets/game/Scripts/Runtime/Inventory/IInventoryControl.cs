using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IInventoryControl
    {
        event Action Changed;

        Inventory Inventory { get; }

        void Move(int handle, int x, int y, bool rotated);

        void Use(int handle);

        void Drop(int handle, int count);

        void Attach(int handle);

        void Detach(int weaponHandle, string itemId);
    }
}
