using System;

namespace Shooter.Core
{
    public sealed class InventoryAmmoPool : IAmmoPool
    {
        private readonly Inventory _inventory;

        public InventoryAmmoPool(Inventory inventory, string itemId)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            if (!inventory.Catalog.TryFind(itemId, out var item))
            {
                throw new ArgumentException("item " + itemId + " is not one of the inventory's items", nameof(itemId));
            }

            if (item.IsWeapon)
            {
                throw new ArgumentException("item " + itemId + " is a weapon, not ammo", nameof(itemId));
            }

            Id = item.Id;
        }

        public event Action Changed
        {
            add => _inventory.Changed += value;
            remove => _inventory.Changed -= value;
        }

        public string Id { get; }

        public int Count => _inventory.CountOf(Id);

        public int Room => _inventory.RoomFor(Id);

        public int Take(int count)
        {
            return _inventory.Remove(Id, count);
        }

        public int Put(int count)
        {
            return _inventory.Add(Id, count);
        }
    }
}
