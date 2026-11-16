using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour, IInventoryControl
    {
        public const string LogTag = "[Inventory]";

        public const float DropDistance = 1f;

        private Inventory _inventory;
        private Damageable _damageable;

        public event Action Changed;

        public Inventory Inventory
        {
            get
            {
                if (_inventory == null)
                {
                    Initialize(KitTuning.Items);
                }

                return _inventory;
            }
        }

        public float CarriedWeight => Inventory.Rule.CountWeightInCarryLoad ? Inventory.TotalWeight : 0f;

        public void Initialize(ItemCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (_inventory != null)
            {
                _inventory.Changed -= OnInventoryChanged;
            }

            _inventory = new Inventory(catalog);
            _inventory.Changed += OnInventoryChanged;
            _inventory.Fill();
            Debug.Log($"{LogTag} ready owner={name} items={catalog.Items.Count} stacks={_inventory.Stacks.Count} grid={catalog.Rule.GridWidth}x{catalog.Rule.GridHeight}", this);
            Changed?.Invoke();
        }

        public void Move(int handle, int x, int y, bool rotated)
        {
            var result = Inventory.TryMove(handle, x, y, rotated);
            Debug.Log($"{LogTag} move owner={name} handle={handle} to={x},{y}{(rotated ? " rotated" : "")} result={result}", this);
        }

        public void Use(int handle)
        {
            var inventory = Inventory;
            ItemSpec item = null;
            if (inventory.TryGetStack(handle, out var stack))
            {
                inventory.Catalog.TryFind(stack.ItemId, out item);
            }

            var damageable = Damageable;
            var armor = GetComponent<IArmorHolder>();
            var outcome = damageable == null
                ? ItemUseOutcome.Rejected(ItemUseRejection.UserDown)
                : ItemUseJudge.Judge(item, Describe(damageable, armor));
            if (!outcome.IsUsed)
            {
                Debug.Log($"{LogTag} use owner={name} handle={handle} rejected={outcome.Rejection}", this);
                return;
            }

            inventory.TryTake(handle, 1, out _);
            if (outcome.Health > 0)
            {
                damageable.Health.Heal(outcome.Health);
            }

            if (outcome.Armor > 0 && armor != null)
            {
                armor.AddArmor(outcome.Armor);
            }

            Debug.Log($"{LogTag} use owner={name} item={item.Id} health=+{outcome.Health} armor=+{outcome.Armor}", this);
        }

        public void Drop(int handle, int count)
        {
            if (count <= 0 || !Inventory.TryTake(handle, count, out var dropped))
            {
                Debug.Log($"{LogTag} drop owner={name} handle={handle} count={count} rejected", this);
                return;
            }

            var forward = transform.forward;
            forward.y = 0f;
            var position = transform.position + (forward.sqrMagnitude > 1e-6f ? forward.normalized * DropDistance : Vector3.zero);
            var pickup = Pickup.Create(PickupContents.ForItem(dropped.ItemId, dropped.Count), position, PickupCollectMode.Look);
            Debug.Log($"{LogTag} drop owner={name} item={dropped.ItemId} count={dropped.Count} pickup={pickup.name}", this);
        }

        private Damageable Damageable => _damageable != null ? _damageable : (_damageable = GetComponent<Damageable>());

        private static PickupCollector Describe(Damageable damageable, IArmorHolder armor)
        {
            var health = damageable.Health;
            var collector = new PickupCollector(null, damageable.IsDowned ? 0 : health.Current, health.Max);

            // 上限が 0 以下の実装は防具を持てないものとして扱う (拾う判定と同じ)
            return armor != null && armor.MaxArmor > 0 ? collector.WithArmor(Mathf.Clamp(armor.Armor, 0, armor.MaxArmor), armor.MaxArmor) : collector;
        }

        private void OnEnable()
        {
            if (Damageable != null)
            {
                Damageable.HealthReset += OnHealthReset;
            }
        }

        private void OnDisable()
        {
            if (_damageable != null)
            {
                _damageable.HealthReset -= OnHealthReset;
            }
        }

        private void OnHealthReset(Health health)
        {
            if (_inventory != null)
            {
                _inventory.Fill();
            }
        }

        private void OnInventoryChanged()
        {
            Changed?.Invoke();
        }
    }
}
