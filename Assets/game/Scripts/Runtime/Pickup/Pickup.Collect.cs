using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class Pickup
    {
        private readonly List<Collider> _insideBuffer = new List<Collider>();
        private readonly HashSet<Damageable> _triedThisTick = new HashSet<Damageable>();

        public Damageable LocalPlayerInside()
        {
            foreach (var collider in _inside)
            {
                var collector = CollectorOf(collider);
                if (collector != null && collector.IsLocalPlayer)
                {
                    return collector;
                }
            }

            return null;
        }

        public bool TryDescribe(Damageable player, out PickupHint hint)
        {
            hint = default;
            if (player == null || _collected || _collectMode == PickupCollectMode.Touch || _contents == null || !TryResolveSpec())
            {
                return false;
            }

            var collector = Describe(player);
            var outcome = PickupJudge.Judge(_spec, collector);
            var nameKey = _contents.Weapon != null ? _contents.Weapon.NameKey : null;
            if (_spec.Kind == PickupKind.Item && collector.Inventory != null && collector.Inventory.Catalog.TryFind(_spec.ItemId, out var item))
            {
                nameKey = item.NameKey;
            }

            return PickupHint.TryFrom(outcome, nameKey, InteractActionPath, out hint);
        }

        public bool TryCollect(Damageable collector, out PickupOutcome outcome, bool logRejection = true)
        {
            if (collector == null)
            {
                throw new ArgumentNullException(nameof(collector));
            }

            if (!TryResolveSpec())
            {
                outcome = PickupOutcome.Rejected(_contents != null ? _contents.Kind : PickupKind.Health, PickupRejection.ApplyFailed);
                return false;
            }

            var spec = _spec;
            if (_collected)
            {
                outcome = PickupOutcome.Rejected(spec.Kind, PickupRejection.Gone);
                return false;
            }

            var weapons = WeaponsOf(collector);
            var armor = collector.GetComponent<IArmorHolder>();
            outcome = PickupJudge.Judge(spec, Describe(collector, weapons, armor));
            if (!outcome.IsCollected)
            {
                if (logRejection && outcome.Rejection != PickupRejection.CollectorDown)
                {
                    Debug.Log(PickupLog.Rejected(name, collector.Id, outcome), this);
                }

                return false;
            }

            if (!TryApply(outcome, collector, weapons, armor, out var failure))
            {
                Debug.LogError(PickupLog.Failed(name, collector.Id, spec.Kind, failure), this);
                outcome = PickupOutcome.Rejected(spec.Kind, PickupRejection.ApplyFailed);
                return false;
            }

            // 入り切らなかったアイテムは、残りの数にしてこの場に置いておく
            if (spec.Kind == PickupKind.Item && outcome.Items < spec.Amount)
            {
                Contents = _contents.WithAmount(spec.Amount - outcome.Items);
                Debug.Log(PickupLog.Collected(name, collector.Id, outcome) + " left=" + (spec.Amount - outcome.Items), this);
                return true;
            }

            _collected = true;
            _inside.Clear();
            Debug.Log(PickupLog.Collected(name, collector.Id, outcome), this);
            Collected?.Invoke(new PickupCollection(this, collector, spec, outcome));

            if (_destroyOnCollect)
            {
                DestroySafely(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }

            return true;
        }

        public static PickupCollector Describe(Damageable collector)
        {
            if (collector == null)
            {
                throw new ArgumentNullException(nameof(collector));
            }

            return Describe(collector, WeaponsOf(collector), collector.GetComponent<IArmorHolder>());
        }

        private static PickupCollector Describe(Damageable collector, IWeaponHolder weapons, IArmorHolder armor)
        {
            var side = SideMember.Of(collector.gameObject);
            var health = collector.Health;
            var current = collector.IsDowned ? 0 : health.Current;
            var state = new PickupCollector(side != null ? side.Side : (Side?)null, current, health.Max);

            if (weapons != null)
            {
                var list = weapons.DescribeWeapons(out var currentWeaponId);
                state = state.WithWeapons(list, currentWeaponId, Mathf.Max(0, weapons.MaxWeapons));
            }

            // 上限が 0 以下の実装は防具を持てないものとして扱う (Core の検査で例外にしない)。
            if (armor != null && armor.MaxArmor > 0)
            {
                state = state.WithArmor(Mathf.Clamp(armor.Armor, 0, armor.MaxArmor), armor.MaxArmor);
            }

            var inventory = collector.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                state = state.WithInventory(inventory.Inventory);
            }

            return state;
        }

        internal static IWeaponHolder WeaponsOf(Damageable collector)
        {
            var weapons = collector.GetComponentInChildren<WeaponController>(true);
            if (weapons != null && weapons.IsInitialized)
            {
                return weapons;
            }

            var agent = collector.GetComponent<EnemyAgent>();
            return agent != null && agent.IsInitialized ? agent : null;
        }

        private bool TryApply(PickupOutcome outcome, Damageable collector, IWeaponHolder weapons, IArmorHolder armor, out string failure)
        {
            failure = null;
            switch (outcome.Kind)
            {
                case PickupKind.Weapon when outcome.AddsWeapon:
                    var definition = _contents.Weapon;
                    if (definition == null)
                    {
                        failure = "no WeaponDefinition";
                        return false;
                    }

                    try
                    {
                        if (outcome.DroppedWeaponId != null)
                        {
                            if (!weapons.SwapWeapon(outcome.DroppedWeaponId, definition, _contents.CarriedAmmo, _contents.Attachments, out var dropped))
                            {
                                failure = $"could not swap weapon '{outcome.DroppedWeaponId}' for '{definition.Id}'";
                                return false;
                            }

                            DropWeapon(dropped, collector);
                        }
                        else if (!weapons.AddWeapon(definition, _contents.CarriedAmmo, _contents.Attachments))
                        {
                            failure = $"already holds weapon '{definition.Id}'";
                            return false;
                        }
                    }
                    catch (WeaponDefinitionException e)
                    {
                        failure = e.Message;
                        return false;
                    }

                    return true;
                case PickupKind.Weapon:
                case PickupKind.ReserveAmmo:
                    if (weapons.AddReserveAmmo(outcome.WeaponId, outcome.ReserveAmmo) <= 0)
                    {
                        failure = $"could not add ammo to weapon '{outcome.WeaponId}'";
                        return false;
                    }

                    if (outcome.Kind == PickupKind.Weapon)
                    {
                        HandOverAttachments(collector);
                    }

                    return true;
                case PickupKind.Health:
                    collector.Health.Heal(outcome.Health);
                    return true;
                case PickupKind.Armor:
                    armor.AddArmor(outcome.Armor);
                    return true;
                case PickupKind.Item:
                    var inventory = collector.GetComponent<PlayerInventory>();
                    if (inventory == null || inventory.Inventory.Add(outcome.ItemId, outcome.Items) != outcome.Items)
                    {
                        failure = $"could not put {outcome.Items} of item '{outcome.ItemId}' into the inventory";
                        return false;
                    }

                    return true;
                default:
                    // タグはモードが Collected を受けて扱うので、拾う人には何も反映しない。
                    return true;
            }
        }

        private Pickup DropWeapon(HeldWeapon dropped, Damageable collector)
        {
            var position = transform.position;
            var toward = collector.transform.position - position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 1e-6f)
            {
                position += toward.normalized * Mathf.Min(DroppedWeaponOffset, toward.magnitude);
            }

            var contents = PickupContents.ForDroppedWeapon(dropped.Definition, dropped.Ammo, _contents.Eligibility, dropped.Attachments);
            var pickup = Create(contents, position, PickupCollectMode.Look, radius: _radius, lookAngleDegrees: _lookAngleDegrees);
            Debug.Log(PickupLog.Dropped(collector.Id, pickup.name, pickup.Spec, position.ToNumerics()), pickup);
            return pickup;
        }

        // 同じ武器を持つ人は弾だけ取り込むので、付けた物を持ち物へ入れる。入らない物 (持ち物の無い bot も) は足元に置き、消さない
        private void HandOverAttachments(Damageable collector)
        {
            var inventory = collector.GetComponent<PlayerInventory>();
            foreach (var itemId in _contents.Attachments)
            {
                if (inventory != null && inventory.Inventory.Add(itemId, 1) == 1)
                {
                    Debug.Log($"{LogTag} '{name}' attachment item={itemId} -> inventory of {collector.Id}", this);
                    continue;
                }

                var pickup = Create(PickupContents.ForItem(itemId, 1, _contents.Eligibility), transform.position, PickupCollectMode.Look);
                Debug.Log(PickupLog.Dropped(collector.Id, pickup.name, pickup.Spec, transform.position.ToNumerics()), pickup);
            }
        }

        private void TryCollectInside()
        {
            _triedThisTick.Clear();
            foreach (var collider in SnapshotInside())
            {
                var collector = CollectorOf(collider);
                if (collector == null || !_triedThisTick.Add(collector))
                {
                    continue;
                }

                if (TryCollect(collector, out _, logRejection: false) || _collected)
                {
                    return;
                }
            }
        }

        // 拾った処理の途中 (Collected の購読者など) で集合が変わっても回し続けられるよう写す。破棄されたコライダーはここで外す。
        private List<Collider> SnapshotInside()
        {
            _inside.RemoveWhere(collider => collider == null);
            _insideBuffer.Clear();
            _insideBuffer.AddRange(_inside);
            return _insideBuffer;
        }

        private bool IsInside(Damageable collector)
        {
            foreach (var collider in _inside)
            {
                if (collider != null && CollectorOf(collider) == collector)
                {
                    return true;
                }
            }

            return false;
        }

        private static Damageable CollectorOf(Collider collider)
        {
            return collider != null ? collider.GetComponentInParent<Damageable>() : null;
        }
    }
}
