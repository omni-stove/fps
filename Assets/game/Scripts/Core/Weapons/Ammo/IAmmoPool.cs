using System;

namespace Shooter.Core
{
    public interface IAmmoPool
    {
        event Action Changed;

        string Id { get; }

        int Count { get; }

        int Room { get; }

        int Take(int count);

        int Put(int count);
    }
}
