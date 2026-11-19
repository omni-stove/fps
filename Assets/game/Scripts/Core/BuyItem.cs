using System;

namespace Shooter.Core
{
    // 防具の Id は何も引かず、量だけを持ち主に渡すので、一覧の中で区別できればよく、UUID を求めない。
    public sealed class BuyItem
    {
        public BuyItem(string id, BuyItemKind kind, int price, int slot = 0, int amount = 0, string nameKey = "")
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            Kind = SpecGuard.Defined(kind, nameof(kind));
            Price = SpecGuard.NonNegative(price, nameof(price));
            Slot = SpecGuard.NonNegative(slot, nameof(slot));
            if (kind != BuyItemKind.Weapon && slot != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot is only for weapons; leave it 0 for " + kind);
            }

            if (kind == BuyItemKind.Armor)
            {
                Amount = SpecGuard.AtLeast(amount, 1, nameof(amount));
            }
            else if (amount != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount is only for armor; leave it 0 for " + kind);
            }

            Id = id.Trim();
            NameKey = SpecGuard.NameKey(nameKey);
        }

        public string Id { get; }

        public BuyItemKind Kind { get; }

        public int Price { get; }

        public int Slot { get; }

        public int Amount { get; }

        public string NameKey { get; }
    }
}
