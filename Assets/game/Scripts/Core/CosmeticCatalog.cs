using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public enum CosmeticKind
    {
        Skin,
        Camo,
    }

    public sealed class CosmeticCatalog
    {
        // 見た目の差し替えはゲームが絵を用意して初めて意味を持つので、キットは空の一覧を既定にし、何も差し替えない。
        public static IReadOnlyList<CosmeticItem> DefaultItems { get; } = Array.Empty<CosmeticItem>();

        public static CosmeticCatalog Default { get; } = new CosmeticCatalog();

        public CosmeticCatalog(IReadOnlyList<CosmeticItem> items = null)
        {
            Items = items == null ? DefaultItems : SpecLists.UniqueById(items, i => i.Id, nameof(items));
        }

        public IReadOnlyList<CosmeticItem> Items { get; }

        public CosmeticItem Find(string id)
        {
            return SpecLists.FindById(Items, i => i.Id, id);
        }

        public IReadOnlyList<CosmeticItem> OfKind(CosmeticKind kind)
        {
            var found = new List<CosmeticItem>();
            foreach (var item in Items)
            {
                if (item.Kind == kind)
                {
                    found.Add(item);
                }
            }

            return found;
        }
    }

    public sealed class CosmeticItem
    {
        public const int DefaultUnlockLevel = 0;

        public CosmeticItem(string id, CosmeticKind kind, string nameKey, string visualId, int unlockLevel = DefaultUnlockLevel)
        {
            Id = SpecLists.Id(id, nameof(id));
            Kind = SpecGuard.Defined(kind, nameof(kind));
            DisplayNameKey = SpecGuard.NameKey(nameKey);

            // 絵の無い見た目は装備しても何も変わらないので、読み込みで止める。
            VisualId = SpecLists.Id(visualId, nameof(visualId));
            UnlockLevel = SpecGuard.NonNegative(unlockLevel, nameof(unlockLevel));
        }

        public string Id { get; }

        public CosmeticKind Kind { get; }

        public string DisplayNameKey { get; }

        public string VisualId { get; }

        public int UnlockLevel { get; }

        public bool IsUnlockedAt(int level)
        {
            return level >= UnlockLevel;
        }
    }
}
