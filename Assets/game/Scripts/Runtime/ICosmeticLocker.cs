using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public interface ICosmeticLocker
    {
        event Action Changed;

        CosmeticCatalog Catalog { get; }

        string EquippedId(CosmeticKind kind);

        bool IsUnlocked(string cosmeticId);

        // 外す口を分けないのは、画面が選択肢に「無し」(null) を並べるだけで着ける・外すを同じ操作にできるため。
        bool Equip(CosmeticKind kind, string cosmeticId);
    }
}
