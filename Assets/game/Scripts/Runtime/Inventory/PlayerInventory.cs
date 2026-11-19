using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour, IInventoryControl
    {
        public const string LogTag = "[Inventory]";

        public const float DropDistance = 1f;

        private Inventory _inventory;
        private Damageable _damageable;
        private WeaponController _weapons;

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

        // 武器の重さは移動のルール (CarryLoadSpec) が武器として数えるので、持ち物の重さからは除く
        public float CarriedWeight => Inventory.Rule.CountWeightInCarryLoad ? Inventory.TotalWeight - Inventory.WeaponWeight : 0f;

        public WeaponController Weapons => _weapons;

        public Func<string, bool> IsAvailable { get; set; }

        public void Bind(WeaponController weapons)
        {
            if (_weapons == weapons)
            {
                return;
            }

            if (_weapons != null)
            {
                _weapons.WeaponChanged -= OnWeaponChanged;
                _weapons.LoadoutReplaced -= HoldWeapons;
                _weapons.AttachmentsReleased -= TakeBackAttachments;
                _weapons.AttachmentsRestored -= SpendAttachments;
                _weapons.UseAmmoPools(null);
            }

            _weapons = weapons;
            if (_weapons == null)
            {
                return;
            }

            _weapons.WeaponChanged += OnWeaponChanged;
            _weapons.LoadoutReplaced += HoldWeapons;
            _weapons.AttachmentsReleased += TakeBackAttachments;
            _weapons.AttachmentsRestored += SpendAttachments;
            _weapons.AttachmentCatalog = Inventory.Catalog;
            ConnectWeapons();
        }

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
            if (_weapons != null)
            {
                _weapons.AttachmentCatalog = catalog;
                ConnectWeapons();
            }

            Debug.Log($"{LogTag} ready owner={name} items={catalog.Items.Count} stacks={_inventory.Stacks.Count} grid={catalog.Rule.GridWidth}x{catalog.Rule.GridHeight}", this);
            Changed?.Invoke();
        }

        // 付けた物は最初の持ち物にまた入るので、武器からは戻さずに外す。出直しの経路ごとに外すと漏れる経路で付けた物が増えるため、ここでまとめて外す
        public void Refill()
        {
            // Fill が武器の重さを使って最初の持ち物を置くので、付けた物を先に外して武器の重さを揃える
            if (_weapons != null)
            {
                _weapons.ClearAttachments();
            }

            Inventory.Fill();
            HoldWeapons();
            Restock();
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

            if (item != null && item.IsWeapon)
            {
                var equipped = _weapons != null && _weapons.IsInitialized && Damageable != null && !Damageable.IsDowned && _weapons.Select(item.WeaponId);
                Debug.Log($"{LogTag} equip owner={name} weapon={item.WeaponId}{(equipped ? string.Empty : " rejected")}", this);
                return;
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
            var weaponId = Inventory.WeaponIdOf(handle);
            if (weaponId != null)
            {
                DropWeapon(weaponId);
                return;
            }

            if (count <= 0 || !Inventory.TryTake(handle, count, out var dropped))
            {
                Debug.Log($"{LogTag} drop owner={name} handle={handle} count={count} rejected", this);
                return;
            }

            var pickup = Pickup.Create(PickupContents.ForItem(dropped.ItemId, dropped.Count), DropPosition(), PickupCollectMode.Look);
            Debug.Log($"{LogTag} drop owner={name} item={dropped.ItemId} count={dropped.Count} pickup={pickup.name}", this);
        }

        public void Attach(int handle)
        {
            var weaponId = CanChangeWeapons ? _weapons.Weapon?.Spec.Id : null;
            var result = InventoryAttachments.Attach(
                Inventory,
                handle,
                weaponId,
                _weapons != null && weaponId != null ? _weapons.AttachmentsOf(weaponId) : WeaponAttachments.None,
                next => _weapons.SetAttachments(weaponId, next),
                out var itemId,
                out var replaced,
                IsAvailable ?? Unlocks.IsUnlocked);
            Debug.Log(
                $"{LogTag} attach owner={name} handle={handle} item={itemId ?? "-"} weapon={weaponId ?? "-"} replaced={replaced ?? "-"}"
                + (result == AttachmentRejection.None ? string.Empty : $" rejected={result}"),
                this);
        }

        public void Detach(int weaponHandle, string itemId)
        {
            var weaponId = CanChangeWeapons ? Inventory.WeaponIdOf(weaponHandle) : null;
            var result = InventoryAttachments.Detach(
                Inventory,
                weaponId,
                weaponId != null ? _weapons.AttachmentsOf(weaponId) : WeaponAttachments.None,
                itemId,
                next => _weapons.SetAttachments(weaponId, next));
            Debug.Log(
                $"{LogTag} detach owner={name} weapon={weaponId ?? "-"} item={itemId ?? "-"}" + (result == AttachmentRejection.None ? string.Empty : $" rejected={result}"),
                this);
        }

        private bool CanChangeWeapons => _weapons != null && _weapons.IsInitialized && !_weapons.IsUnarmed && Damageable != null && !Damageable.IsDowned;

        private void DropWeapon(string weaponId)
        {
            if (_weapons == null || !_weapons.IsInitialized || !_weapons.DropWeapon(weaponId, out var dropped))
            {
                Debug.Log($"{LogTag} drop owner={name} weapon={weaponId} rejected", this);
                return;
            }

            var pickup = Pickup.Create(
                PickupContents.ForDroppedWeapon(dropped.Definition, dropped.Ammo, attachments: dropped.Attachments), DropPosition(), PickupCollectMode.Look);
            Debug.Log(
                $"{LogTag} drop owner={name} weapon={weaponId} ammo={dropped.Ammo.AmmoInMagazine} attachments={WeaponAttachments.Describe(dropped.Attachments)} pickup={pickup.name}",
                this);
        }

        private Vector3 DropPosition()
        {
            var forward = transform.forward;
            forward.y = 0f;
            return transform.position + (forward.sqrMagnitude > 1e-6f ? forward.normalized * DropDistance : Vector3.zero);
        }

        private void ConnectWeapons()
        {
            var inventory = Inventory;
            HoldWeapons();
            _weapons.UseAmmoPools(spec => InventoryWeapons.PoolFor(inventory, spec));
            Restock();
        }

        private void HoldWeapons()
        {
            if (_weapons == null || _inventory == null)
            {
                return;
            }

            var loadout = _weapons.Loadout;
            var held = new List<WeaponSpec>();
            if (loadout != null)
            {
                foreach (var weapon in loadout.Items)
                {
                    held.Add(weapon.Spec);
                }
            }

            foreach (var weaponId in _inventory.HoldWeapons(held, _weapons.AttachmentsOf))
            {
                Debug.LogWarning($"{LogTag} weapon owner={name} weapon={weaponId} does not fit; held outside the inventory", this);
            }
        }

        private void Restock()
        {
            var loadout = _weapons != null ? _weapons.Loadout : null;
            if (loadout == null || _inventory == null)
            {
                return;
            }

            var pooled = new List<(string WeaponId, int StartingReserveAmmo)>();
            foreach (var weapon in loadout.Items)
            {
                if (weapon.AmmoPool != null)
                {
                    pooled.Add((weapon.Spec.Id, weapon.StartingReserveAmmo));
                }
            }

            var added = InventoryWeapons.Restock(_inventory, pooled);
            if (added > 0)
            {
                Debug.Log($"{LogTag} restock owner={name} ammo={added}", this);
            }
        }

        private void TakeBackAttachments(IReadOnlyList<string> released)
        {
            if (_inventory == null)
            {
                return;
            }

            var freed = 0f;
            foreach (var itemId in released)
            {
                // 付けた物の重さはまだ武器の重さに数えられているので、戻した分も含めた累積を空きに足して判定する
                freed += _inventory.Catalog.TryFind(itemId, out var item) ? item.Weight : 0f;
                if (_inventory.Add(itemId, 1, freed) < 1)
                {
                    Debug.LogWarning($"{LogTag} attachment owner={name} item={itemId} does not fit; lost", this);
                }
            }
        }

        // セーブから戻した付けた物は持ち物にも最初の分が入っているので、付けた分だけ引く
        private void SpendAttachments(IReadOnlyList<string> restored)
        {
            if (_inventory == null)
            {
                return;
            }

            foreach (var itemId in restored)
            {
                _inventory.Remove(itemId, 1);
            }
        }

        private void OnWeaponChanged(Weapon weapon)
        {
            HoldWeapons();
        }

        private Damageable Damageable => _damageable != null ? _damageable : (_damageable = GetComponent<Damageable>());

        private static PickupCollector Describe(Damageable damageable, IArmorHolder armor)
        {
            var health = damageable.Health;
            var collector = new PickupCollector(null, damageable.IsDowned ? 0 : health.Current, health.Max);

            // 上限が 0 以下の実装は防具を持てないものとして扱う (拾う判定と同じ)
            return armor != null && armor.MaxArmor > 0 ? collector.WithArmor(Mathf.Clamp(armor.Armor, 0, armor.MaxArmor), armor.MaxArmor) : collector;
        }

        // シーンに置いたプレイヤーでも、組み立てから Bind されなかったときは子の武器を使う
        private void Start()
        {
            if (_weapons == null)
            {
                var weapons = GetComponentInChildren<WeaponController>(true);
                if (weapons != null)
                {
                    Bind(weapons);
                }
            }
        }

        private void OnEnable()
        {
            if (Damageable != null)
            {
                Damageable.HealthReset += OnHealthReset;
            }
        }

        private void OnDestroy()
        {
            Bind(null);
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
                Refill();
            }
        }

        private void OnInventoryChanged()
        {
            Changed?.Invoke();
        }
    }
}
