using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // Unity 抜きのサーバーは PickupSpawnPoint と AmmoCrate を読めないので、Unity の Editor がシーンから書き出したものを読む。
    // 地点ファイル (NetMapPoints) に混ぜないのは、拾い物を置かないマップの地点ファイルを、拾い物を知らなかったころと同じ中身に保つため
    public sealed class NetMapPickups
    {
        public const string Extension = ".pickups.json";

        public const string PickupsItem = "pickups";
        public const string AmmoCratesItem = "ammoCrates";
        public const string IdItem = "id";
        public const string KindItem = "kind";
        public const string AmountItem = "amount";
        public const string WeaponItem = "weapon";
        public const string ItemItem = "item";
        public const string SideRuleItem = "sideRule";
        public const string SideItem = "side";
        public const string PositionItem = "position";
        public const string RadiusItem = "radius";
        public const string RespawnSecondsItem = "respawnSeconds";
        public const string TouchItem = "touch";
        public const string CooldownSecondsItem = "cooldownSeconds";

        private const string _kind = "map pickups JSON";

        private static readonly string[] _topItems = { PickupsItem, AmmoCratesItem };

        private static readonly string[] _pickupItems =
        {
            IdItem, KindItem, AmountItem, WeaponItem, ItemItem, SideRuleItem, SideItem, PositionItem, RadiusItem, RespawnSecondsItem, TouchItem,
        };

        private static readonly string[] _crateItems = { IdItem, PositionItem, RadiusItem, CooldownSecondsItem };

        public NetMapPickups(IReadOnlyList<NetMapPickup> pickups, IReadOnlyList<NetMapAmmoCrate> ammoCrates)
        {
            Pickups = CopyUnique(pickups, p => p.Id, nameof(pickups));
            AmmoCrates = CopyUnique(ammoCrates, c => c.Id, nameof(ammoCrates));
        }

        public static NetMapPickups None { get; } = new NetMapPickups(null, null);

        // 並びはシーンの階層順。サーバーはこの順で物の Id を振る
        public IReadOnlyList<NetMapPickup> Pickups { get; }

        public IReadOnlyList<NetMapAmmoCrate> AmmoCrates { get; }

        public static NetMapPickups Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pickups = new List<NetMapPickup>();
            var pickupNodes = top.Array(PickupsItem);
            for (var i = 0; i < pickupNodes.Count; i++)
            {
                pickups.Add(ReadPickup(top.Element(pickupNodes[i], PickupsItem, i, _pickupItems), seen));
            }

            seen.Clear();
            var crates = new List<NetMapAmmoCrate>();
            var crateNodes = top.Array(AmmoCratesItem);
            for (var i = 0; i < crateNodes.Count; i++)
            {
                var fields = top.Element(crateNodes[i], AmmoCratesItem, i, _crateItems);
                var id = ReadId(fields, seen);
                var position = fields.ReadVector3(PositionItem);
                var radius = fields.ToFloat(fields.ItemPath(RadiusItem), fields.Require(RadiusItem));
                var cooldown = fields.ToFloat(fields.ItemPath(CooldownSecondsItem), fields.Require(CooldownSecondsItem));
                try
                {
                    crates.Add(new NetMapAmmoCrate(id, position, radius, cooldown));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(fields.ItemPath(ItemOf(e.ParamName)), e.Message, e);
                }
            }

            return new NetMapPickups(pickups, crates);
        }

        public static string Write(NetMapPickups pickups)
        {
            if (pickups == null)
            {
                throw new ArgumentNullException(nameof(pickups));
            }

            var writer = new JsonWriter();
            writer.BeginArray(PickupsItem);
            foreach (var pickup in pickups.Pickups)
            {
                var spec = pickup.Spec;
                writer.BeginElement();
                writer.String(IdItem, pickup.Id);
                writer.Enum(KindItem, spec.Kind);
                writer.Int(AmountItem, spec.Amount);
                if (spec.WeaponId != null)
                {
                    writer.String(WeaponItem, spec.WeaponId);
                }

                if (spec.ItemId != null)
                {
                    writer.String(ItemItem, spec.ItemId);
                }

                // 誰でも拾える物は書かず、絞らなかったころと同じ中身に保つ
                if (spec.Eligibility.Rule != PickupSideRule.Anyone)
                {
                    writer.Enum(SideRuleItem, spec.Eligibility.Rule);
                    writer.Int(SideItem, spec.Eligibility.Side.Id);
                }

                writer.Vector3(PositionItem, pickup.Position);
                writer.Float(RadiusItem, pickup.Radius);
                writer.Float(RespawnSecondsItem, pickup.RespawnSeconds);
                writer.Bool(TouchItem, pickup.Touch);
                writer.EndObject();
            }

            writer.EndArray();
            writer.BeginArray(AmmoCratesItem);
            foreach (var crate in pickups.AmmoCrates)
            {
                writer.BeginElement();
                writer.String(IdItem, crate.Id);
                writer.Vector3(PositionItem, crate.Position);
                writer.Float(RadiusItem, crate.Radius);
                writer.Float(CooldownSecondsItem, crate.CooldownSeconds);
                writer.EndObject();
            }

            writer.EndArray();
            return writer.Finish();
        }

        private static NetMapPickup ReadPickup(ServerDataJson.Fields fields, HashSet<string> seen)
        {
            var id = ReadId(fields, seen);
            var kind = fields.ToEnum<PickupKind>(fields.ItemPath(KindItem), fields.Require(KindItem));
            var amount = fields.ToInt(fields.ItemPath(AmountItem), fields.Require(AmountItem));
            var weapon = fields.ReadString(WeaponItem, null);
            var item = fields.ReadString(ItemItem, null);
            var rule = fields.ReadEnum(SideRuleItem, PickupSideRule.Anyone);
            var sideNode = fields.Find(SideItem);
            if (rule != PickupSideRule.Anyone && sideNode == null)
            {
                throw fields.Rejected(fields.ItemPath(SideItem), "is required when sideRule is " + rule);
            }

            var side = sideNode == null ? 0 : fields.ToInt(fields.ItemPath(SideItem), sideNode);
            if (side < 0)
            {
                throw fields.Invalid(fields.ItemPath(SideItem), sideNode, "must not be negative");
            }

            var position = fields.ReadVector3(PositionItem);
            var radius = fields.ToFloat(fields.ItemPath(RadiusItem), fields.Require(RadiusItem));
            var respawn = fields.ToFloat(fields.ItemPath(RespawnSecondsItem), fields.Require(RespawnSecondsItem));
            var touch = fields.ReadBool(TouchItem, true);

            PickupSpec spec;
            try
            {
                spec = SpecOf(kind, amount, weapon, item, PickupEligibility.Of(rule, new Side(side)));
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(fields.ItemPath(SpecItemOf(e.ParamName)), e.Message, e);
            }

            try
            {
                return new NetMapPickup(id, spec, position, radius, respawn, touch);
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(fields.ItemPath(ItemOf(e.ParamName)), e.Message, e);
            }
        }

        // 種類が使わない欄 (回復の weapon など) が書いてあれば、書き間違いとして断る
        private static PickupSpec SpecOf(PickupKind kind, int amount, string weapon, string item, PickupEligibility eligibility)
        {
            if (weapon != null && kind != PickupKind.Weapon && kind != PickupKind.ReserveAmmo)
            {
                throw new ArgumentException("only a Weapon or ReserveAmmo pickup names a weapon", "weaponId");
            }

            if (item != null && kind != PickupKind.Item)
            {
                throw new ArgumentException("only an Item pickup names an item", "itemId");
            }

            switch (kind)
            {
                case PickupKind.Weapon:
                    return PickupSpec.Weapon(weapon, amount, eligibility);
                case PickupKind.ReserveAmmo:
                    return PickupSpec.ReserveAmmo(amount, weapon, eligibility);
                case PickupKind.Health:
                    return PickupSpec.Health(amount, eligibility);
                case PickupKind.Armor:
                    return PickupSpec.Armor(amount, eligibility);
                case PickupKind.Item:
                    return PickupSpec.Item(item, amount, eligibility);
                default:
                    throw new ArgumentException("a pickup spawn point cannot hold a " + kind, "kind");
            }
        }

        private static string SpecItemOf(string paramName)
        {
            switch (paramName)
            {
                case "weaponId":
                    return WeaponItem;
                case "itemId":
                    return ItemItem;
                case "kind":
                    return KindItem;
                default:
                    return AmountItem;
            }
        }

        private static string ItemOf(string paramName)
        {
            switch (paramName)
            {
                case "id":
                    return IdItem;
                case "spec":
                    return KindItem;
                case "position":
                    return PositionItem;
                case "respawnSeconds":
                    return RespawnSecondsItem;
                case "cooldownSeconds":
                    return CooldownSecondsItem;
                default:
                    return RadiusItem;
            }
        }

        // 重なりを並びの中で断るのは、どちらの要素を直せばよいかを項目の場所で示すため
        private static string ReadId(ServerDataJson.Fields fields, HashSet<string> seen)
        {
            var node = fields.Require(IdItem);
            var id = fields.ToText(fields.ItemPath(IdItem), node);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw fields.Invalid(fields.ItemPath(IdItem), node, "must not be empty");
            }

            if (!seen.Add(id))
            {
                throw fields.Invalid(fields.ItemPath(IdItem), node, "\"" + id + "\" is used twice");
            }

            return id;
        }

        private static System.Collections.ObjectModel.ReadOnlyCollection<T> CopyUnique<T>(IReadOnlyList<T> items, Func<T, string> idOf, string name)
            where T : class
        {
            var copy = new List<T>(items?.Count ?? 0);
            if (items == null)
            {
                return copy.AsReadOnly();
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException(name + " must not contain null", name);
                }

                if (!seen.Add(idOf(item)))
                {
                    throw new ArgumentException(name + " uses the id \"" + idOf(item) + "\" twice", name);
                }

                copy.Add(item);
            }

            return copy.AsReadOnly();
        }
    }
}
