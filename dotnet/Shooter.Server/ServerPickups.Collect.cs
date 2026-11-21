using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerPickups
    {
        // 拾う要求。拾い物なら拾い、弾薬箱なら持っている武器の予備弾を満たす。触れて拾う物も要求で拾える (オフラインの Touch も入れば拾うので、押して拾えても同じ)
        internal ServerPickupRefusal Collect(int playerId, int itemId)
        {
            RequireStarted();
            var crate = FindCrate(itemId);
            if (crate != null)
            {
                return Refill(playerId, crate);
            }

            var spot = FindSpot(itemId);
            if (spot == null)
            {
                return Refuse(itemId, playerId, ServerPickupRefusal.UnknownItem, PickupRejection.None, true);
            }

            return TryCollect(playerId, spot, true);
        }

        // 断る理由は安いものから確かめ、射線は最後にする
        private ServerPickupRefusal TryCollect(int playerId, Spot spot, bool logRefusal)
        {
            var refusal = !_rules.CanFire(playerId) ? ServerPickupRefusal.CannotAct
                : !spot.IsAvailable ? ServerPickupRefusal.Gone
                : !IsInReach(spot.Position, spot.Radius, playerId) ? ServerPickupRefusal.OutOfReach
                : !IsInSight(spot.Position, playerId) ? ServerPickupRefusal.OutOfSight
                : ServerPickupRefusal.None;
            if (refusal != ServerPickupRefusal.None)
            {
                return Refuse(spot.ItemId, playerId, refusal, PickupRejection.None, logRefusal);
            }

            var spec = WithWeight(spot);
            var outcome = PickupJudge.Judge(spec, Describe(playerId));
            if (!outcome.IsCollected)
            {
                return Refuse(spot.ItemId, playerId, ServerPickupRefusal.Judged, outcome.Rejection, logRefusal);
            }

            if (!TryApply(playerId, spot, outcome, out var itemsPut, out var failure))
            {
                _log.WriteLine(ServerPickupsLog.Failed(spot.ItemId, playerId, failure));
                return ServerPickupRefusal.ApplyFailed;
            }

            _log.WriteLine(ServerPickupsLog.Collected(spot.ItemId, playerId, outcome));

            // 入り切らなかったアイテムは、残りの数にしてこの場に置いておく
            if (spec.Kind == PickupKind.Item && itemsPut < spec.Amount)
            {
                var left = spec.Amount - itemsPut;
                spot.Spec = PickupSpec.Item(spec.ItemId, left, spec.Eligibility);
                _log.WriteLine(ServerPickupsLog.Left(spot.ItemId, left));
                return ServerPickupRefusal.None;
            }

            if (spot.Respawn != null)
            {
                spot.Respawn.Collect();
            }
            else
            {
                _spots.Remove(spot);
            }

            return ServerPickupRefusal.None;
        }

        // オフラインの AmmoCrate.TryRefill と同じく、足せる弾が無ければ次に使えるまでの時間を始めない
        private ServerPickupRefusal Refill(int playerId, Crate crate)
        {
            var refusal = !_rules.CanFire(playerId) ? ServerPickupRefusal.CannotAct
                : _now < crate.ReadyAt ? ServerPickupRefusal.CoolingDown
                : !IsInReach(crate.Position, crate.Radius, playerId) ? ServerPickupRefusal.OutOfReach
                : !IsInSight(crate.Position, playerId) ? ServerPickupRefusal.OutOfSight
                : ServerPickupRefusal.None;
            if (refusal != ServerPickupRefusal.None)
            {
                return Refuse(crate.ItemId, playerId, refusal, PickupRejection.None, true);
            }

            var added = 0;
            foreach (var weapon in _combat.WeaponsOf(playerId).Items)
            {
                var room = weapon.HasInfiniteReserve ? 0 : Math.Max(0, weapon.MaxReserveAmmo - weapon.ReserveAmmo);
                if (room > 0)
                {
                    added += weapon.AddReserveAmmo(room);
                }
            }

            if (added <= 0)
            {
                return Refuse(crate.ItemId, playerId, ServerPickupRefusal.NothingToRefill, PickupRejection.None, true);
            }

            crate.ReadyAt = _now + crate.CooldownSeconds;
            _log.WriteLine(ServerPickupsLog.Refilled(crate.ItemId, playerId, added, crate.CooldownSeconds));
            return ServerPickupRefusal.None;
        }

        // 触れて拾う物は、範囲に入った人から拾わせる。断った理由は入った時だけログに出し、立ち止まっている間に毎 tick 出さない
        // 拾ったときに並びが変わる (落とし物が消える・持ち替えた武器を置く) ので、写した並びで回す
        private void CollectTouched()
        {
            _stillTouching.Clear();
            _scan.Clear();
            _scan.AddRange(_spots);
            foreach (var spot in _scan)
            {
                if (!spot.Touch)
                {
                    continue;
                }

                foreach (var body in _context.Bodies.All)
                {
                    if (!spot.IsAvailable || !_spots.Contains(spot))
                    {
                        break;
                    }

                    if (!body.HasSpawned || !IsInReach(spot.Position, spot.Radius, body.PlayerId))
                    {
                        continue;
                    }

                    var key = TouchKey(spot.ItemId, body.PlayerId);
                    _stillTouching.Add(key);
                    TryCollect(body.PlayerId, spot, !_touching.Contains(key));
                }
            }

            _scan.Clear();
            _touching.Clear();
            _touching.UnionWith(_stillTouching);
        }

        // itemsPut は持ち物に入ったアイテムの数。アイテムでなければ 0
        private bool TryApply(int playerId, Spot spot, PickupOutcome outcome, out int itemsPut, out string failure)
        {
            itemsPut = 0;
            failure = null;
            var weapons = _combat.WeaponsOf(playerId);
            switch (outcome.Kind)
            {
                case PickupKind.Weapon when outcome.AddsWeapon:
                    return TryGiveWeapon(playerId, spot, outcome, weapons, out failure);
                case PickupKind.Weapon:
                case PickupKind.ReserveAmmo:
                    var held = weapons.Find(outcome.WeaponId);
                    if (held == null || held.AddReserveAmmo(outcome.ReserveAmmo) <= 0)
                    {
                        failure = "could not add ammo to weapon " + outcome.WeaponId;
                        return false;
                    }

                    if (outcome.Kind == PickupKind.Weapon && spot.Carried != null)
                    {
                        HandOverAttachments(playerId, spot, spot.Carried.Attachments);
                    }

                    return true;
                case PickupKind.Health:
                    if (_rules.Session.Heal(playerId, outcome.Health) <= 0)
                    {
                        failure = "could not heal";
                        return false;
                    }

                    return true;
                case PickupKind.Item:
                    itemsPut = _inventories?.Give(playerId, outcome.ItemId, outcome.Items) ?? 0;
                    if (itemsPut <= 0)
                    {
                        failure = "could not put item " + outcome.ItemId + " into the inventory";
                        return false;
                    }

                    return true;
                default:
                    // 防具はサーバーの体が持たないので、判定 (NoArmor) で断っている
                    failure = "nothing applies a " + outcome.Kind + " pickup on the server";
                    return false;
            }
        }

        private bool TryGiveWeapon(int playerId, Spot spot, PickupOutcome outcome, ServerPlayerWeapons weapons, out string failure)
        {
            failure = null;
            var loadout = _rules.Loadout;
            var rules = _rules.Rules;
            var spec = spot.Weapon ?? WeaponFor(outcome.WeaponId);
            if (loadout == null || rules == null || spec == null)
            {
                failure = "no weapon " + outcome.WeaponId + " in the loadout";
                return false;
            }

            if (outcome.DroppedWeaponId != null)
            {
                var replaced = weapons.Replace(outcome.DroppedWeaponId, spec, loadout.SwitchSeconds, rules);
                if (replaced == null)
                {
                    failure = "could not swap weapon " + outcome.DroppedWeaponId + " for " + spec.Id;
                    return false;
                }

                DropSwapped(playerId, spot, replaced);
            }
            else if (!weapons.Give(spec, loadout.SwitchSeconds, rules))
            {
                failure = "could not give weapon " + spec.Id;
                return false;
            }

            var carried = spot.Carried;
            if (carried != null)
            {
                if (carried.Attachments.Count > 0)
                {
                    weapons.SetAttachments(spec.Id, carried.Attachments, _items);
                }

                weapons.Find(spec.Id)?.SetAmmo(carried.AmmoInMagazine, carried.ReserveAmmo);
            }

            // 持ち物の武器の写しを手の武器に揃える (ServerLoot と同じ)
            if (_inventories != null)
            {
                foreach (var weaponId in _inventories.HoldWeapons(playerId))
                {
                    _log.WriteLine(ServerPickupsLog.Unheld(playerId, weaponId));
                }
            }

            return true;
        }

        // 同じ武器を持つ人は弾だけ取り込むので、付けた物を持ち物へ入れる。入らない物は足元に置き、消さない (オフラインの Pickup と同じ)
        private void HandOverAttachments(int playerId, Spot spot, IReadOnlyList<string> attachments)
        {
            foreach (var attachment in attachments)
            {
                var intoInventory = _inventories != null && _inventories.Give(playerId, attachment, 1) == 1;
                _log.WriteLine(ServerPickupsLog.Attachment(spot.ItemId, playerId, attachment, intoInventory));
                if (!intoInventory)
                {
                    AddDrop(PickupSpec.Item(attachment, 1, spot.Spec.Eligibility), spot.Position, false, spot.OwnerId ?? playerId, null);
                }
            }
        }

        private PickupCollector Describe(int playerId)
        {
            var session = _rules.Session;
            var team = session.GetTeam(playerId);
            var maxHealth = session.GetMaxHealth(playerId);
            var health = Math.Clamp(session.GetHealth(playerId), 0, maxHealth);
            var collector = new PickupCollector(Side.FromTeam(team), health, maxHealth);

            var weapons = _combat.WeaponsOf(playerId);
            var states = new List<PickupWeaponState>(weapons.Items.Count);
            foreach (var weapon in weapons.Items)
            {
                states.Add(new PickupWeaponState(weapon.Spec.Id, weapon.ReserveAmmo, weapon.MaxReserveAmmo, weapon.HasInfiniteReserve));
            }

            // 持てる数はオフラインの WeaponController と同じくルールの MaxWeapons (0 なら上限なし)。一杯なら構えている武器と持ち替える
            collector = collector.WithWeapons(states, weapons.CurrentSpec?.Id, _rules.Rules?.MaxWeapons ?? 0);
            var inventory = _inventories?.InventoryOf(playerId);
            return inventory != null ? collector.WithInventory(inventory) : collector;
        }

        // 持ち物に入るかは武器と付けた物の重さで見るので、置くときではなく拾うときに今の持ち物の表から足す
        private PickupSpec WithWeight(Spot spot)
        {
            var spec = spot.Spec;
            if (spec.Kind != PickupKind.Weapon)
            {
                return spec;
            }

            var weight = _items.TryFindWeapon(spec.WeaponId, out var weaponItem) ? weaponItem.Weight : 0f;
            if (spot.Carried != null)
            {
                foreach (var attachment in WeaponAttachments.Keep(_items, spec.WeaponId, spot.Carried.Attachments))
                {
                    if (_items.TryFind(attachment, out var item))
                    {
                        weight += item.Weight;
                    }
                }
            }

            return PickupSpec.Weapon(spec.WeaponId, spec.Amount, spec.Eligibility, weight);
        }

        private bool IsInReach(Vector3 position, float radius, int playerId)
        {
            if (!_context.Bodies.TryGet(playerId, out var body) || !body.HasSpawned)
            {
                return false;
            }

            var feet = body.Position.Y + body.Shape.BottomY;
            return HorizontalDistanceSquared(position, body.Position) <= radius * radius
                && MathF.Abs(position.Y - feet) <= MaxVerticalReach;
        }

        // 体から物までの線が最初に当たる物が、物の載る床 (物の真上から下ろした線が最初に当たる物) なら見えているとする。
        // 拾い物の当たりはオフラインでもトリガーで弾を止めないので、サーバーの当たりには無い
        private bool IsInSight(Vector3 position, int playerId)
        {
            if (!_context.Bodies.TryGet(playerId, out var body))
            {
                return false;
            }

            var from = body.Center;
            var line = position + new Vector3(0f, SightLift, 0f) - from;
            var distance = line.Length();
            if (!(distance > 1e-4f) || !_context.World.Raycast(from, line / distance, distance, ServerHitboxes.RaycastLayers, out var hit))
            {
                return true;
            }

            var probe = position + new Vector3(0f, _ownProbeHeight, 0f);
            return _context.World.Raycast(probe, -Vector3.UnitY, _ownProbeHeight + MaxVerticalReach, ServerHitboxes.RaycastLayers, out var own)
                && own.ColliderId == hit.ColliderId;
        }

        private ServerPickupRefusal Refuse(int itemId, int playerId, ServerPickupRefusal refusal, PickupRejection rejection, bool log)
        {
            if (!log || (_nextRefusalLogTimes.TryGetValue(playerId, out var next) && _now < next))
            {
                return refusal;
            }

            _nextRefusalLogTimes[playerId] = _now + _refusalLogInterval;
            _log.WriteLine(ServerPickupsLog.Refused(itemId, playerId, refusal, rejection));
            return refusal;
        }

        private static long TouchKey(int itemId, int playerId)
        {
            return ((long)itemId << 32) | (uint)playerId;
        }
    }
}
