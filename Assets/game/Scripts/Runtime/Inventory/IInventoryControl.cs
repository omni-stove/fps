using System;
using Fps.Core;

namespace Fps.Runtime
{
    public interface IInventoryControl
    {
        event Action Changed;

        Inventory Inventory { get; }

        void Move(int handle, int x, int y, bool rotated);

        void Use(int handle);

        void Drop(int handle, int count);
    }
}
