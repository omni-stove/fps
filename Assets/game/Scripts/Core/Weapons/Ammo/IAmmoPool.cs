using System;

namespace Shooter.Core
{
    // 武器が予備弾を自分では数えず、持ち物などの外から出し入れするときの受け皿
    public interface IAmmoPool
    {
        event Action Changed;

        // どの弾の受け皿か。同じ Id の受け皿は同じ弾を数える (別の武器が同じ弾薬を使う)
        string Id { get; }

        int Count { get; }

        int Room { get; }

        int Take(int count);

        int Put(int count);
    }
}
