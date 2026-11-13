using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Fps.Core
{
    /// <summary>拾える物の <c>[Pickup]</c> 付きログの文言。名前が空なら <c>-</c> と書く。</summary>
    public static class PickupLog
    {
        public const string Tag = "[Pickup]";

        /// <summary>中身を 1 語ずつ並べた説明。</summary>
        /// <example><c>kind=Weapon weapon=rifle amount=30 who=anyone</c>、<c>kind=Tag owner=2 owner-side=1 who=anyone</c></example>
        public static string Describe(PickupSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var text = new StringBuilder("kind=").Append(spec.Kind);
            if (spec.WeaponId != null)
            {
                text.Append(" weapon=").Append(spec.WeaponId);
            }

            if (spec.Kind == PickupKind.Tag)
            {
                text.Append(" owner=").Append(Number(spec.TagOwnerId))
                    .Append(" owner-side=").Append(spec.TagOwnerSide.HasValue ? Number(spec.TagOwnerSide.Value.Id) : "-");
            }
            else
            {
                text.Append(" amount=").Append(Number(spec.Amount));
            }

            return text.Append(" who=").Append(spec.Eligibility).ToString();
        }

        /// <example><c>[Pickup] spawned pickup=Pickup-Health kind=Health amount=25 who=anyone at=(1.00, 0.00, 2.00)</c></example>
        public static string Spawned(string pickupName, PickupSpec spec, Vector3 position)
        {
            return Tag + " spawned pickup=" + Name(pickupName) + " " + Describe(spec) + " at=" + Position(position);
        }

        /// <example><c>[Pickup] dropped by=enemy-1 pickup=Pickup-ReserveAmmo kind=ReserveAmmo amount=30 who=anyone at=(1.00, 0.00, 2.00)</c></example>
        public static string Dropped(string ownerName, string pickupName, PickupSpec spec, Vector3 position)
        {
            return Tag + " dropped by=" + Name(ownerName) + " pickup=" + Name(pickupName) + " " + Describe(spec) + " at=" + Position(position);
        }

        /// <summary>拾えたときのログ。反映した量だけを書く。</summary>
        /// <example>
        /// <c>[Pickup] collected pickup=Pickup-Health by=Player kind=Health health=+25</c>、
        /// <c>[Pickup] collected pickup=Pickup-Weapon by=Player kind=Weapon weapon=launcher added</c>、
        /// <c>[Pickup] collected pickup=Tag by=Player kind=Tag owner=2 owner-side=1 own-side=false</c>
        /// </example>
        public static string Collected(string pickupName, string collectorName, PickupOutcome outcome)
        {
            if (!outcome.IsCollected)
            {
                throw new ArgumentException("outcome was rejected; use Rejected", nameof(outcome));
            }

            var text = new StringBuilder(Tag)
                .Append(" collected pickup=").Append(Name(pickupName))
                .Append(" by=").Append(Name(collectorName))
                .Append(" kind=").Append(outcome.Kind);
            if (outcome.WeaponId != null)
            {
                text.Append(" weapon=").Append(outcome.WeaponId);
            }

            if (outcome.AddsWeapon)
            {
                text.Append(" added");
            }

            AppendGain(text, "reserve", outcome.ReserveAmmo);
            AppendGain(text, "health", outcome.Health);
            AppendGain(text, "armor", outcome.Armor);
            if (outcome.Kind == PickupKind.Tag)
            {
                text.Append(" owner=").Append(Number(outcome.TagOwnerId))
                    .Append(" owner-side=").Append(outcome.TagOwnerSide.HasValue ? Number(outcome.TagOwnerSide.Value.Id) : "-")
                    .Append(" own-side=").Append(outcome.IsOwnSideTag ? "true" : "false");
            }

            return text.ToString();
        }

        /// <example><c>[Pickup] rejected pickup=Pickup-Health by=Player kind=Health reason=HealthFull</c></example>
        public static string Rejected(string pickupName, string collectorName, PickupOutcome outcome)
        {
            if (outcome.IsCollected)
            {
                throw new ArgumentException("outcome was collected; use Collected", nameof(outcome));
            }

            return Tag + " rejected pickup=" + Name(pickupName) + " by=" + Name(collectorName) + " kind=" + outcome.Kind + " reason=" + outcome.Rejection;
        }

        /// <summary>拾えると判定したのに反映できなかった (武器の定義が無いなど) ときのログ。</summary>
        /// <example><c>[Pickup] failed pickup=Pickup-Weapon by=Player kind=Weapon reason=no WeaponDefinition</c></example>
        public static string Failed(string pickupName, string collectorName, PickupKind kind, string reason)
        {
            return Tag + " failed pickup=" + Name(pickupName) + " by=" + Name(collectorName) + " kind=" + kind + " reason=" + Name(reason);
        }

        /// <example><c>[Pickup] respawned pickup=Pickup-Health</c></example>
        public static string Respawned(string pickupName)
        {
            return Tag + " respawned pickup=" + Name(pickupName);
        }

        /// <summary>置いてから決まった時間が経ち、拾われずに消えたときのログ。</summary>
        /// <example><c>[Pickup] expired pickup=Pickup-ReserveAmmo</c></example>
        public static string Expired(string pickupName)
        {
            return Tag + " expired pickup=" + Name(pickupName);
        }

        private static void AppendGain(StringBuilder text, string label, int amount)
        {
            if (amount > 0)
            {
                text.Append(' ').Append(label).Append("=+").Append(Number(amount));
            }
        }

        private static string Position(Vector3 position)
        {
            return "(" + position.X.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + position.Y.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + position.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        private static string Name(string name)
        {
            return string.IsNullOrEmpty(name) ? "-" : name;
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
