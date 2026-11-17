using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Shooter.Core
{
    public static class PickupLog
    {
        public const string Tag = "[Pickup]";

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

            if (spec.ItemId != null)
            {
                text.Append(" item=").Append(spec.ItemId);
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

        public static string Spawned(string pickupName, PickupSpec spec, Vector3 position)
        {
            return Tag + " spawned pickup=" + Name(pickupName) + " " + Describe(spec) + " at=" + Position(position);
        }

        public static string Dropped(string ownerName, string pickupName, PickupSpec spec, Vector3 position)
        {
            return Tag + " dropped by=" + Name(ownerName) + " pickup=" + Name(pickupName) + " " + Describe(spec) + " at=" + Position(position);
        }

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

            if (outcome.DroppedWeaponId != null)
            {
                text.Append(" dropped=").Append(outcome.DroppedWeaponId);
            }

            AppendGain(text, "reserve", outcome.ReserveAmmo);
            AppendGain(text, "health", outcome.Health);
            AppendGain(text, "armor", outcome.Armor);
            if (outcome.ItemId != null)
            {
                text.Append(" item=").Append(outcome.ItemId);
            }

            AppendGain(text, "items", outcome.Items);
            if (outcome.Kind == PickupKind.Tag)
            {
                text.Append(" owner=").Append(Number(outcome.TagOwnerId))
                    .Append(" owner-side=").Append(outcome.TagOwnerSide.HasValue ? Number(outcome.TagOwnerSide.Value.Id) : "-")
                    .Append(" own-side=").Append(outcome.IsOwnSideTag ? "true" : "false");
            }

            return text.ToString();
        }

        public static string Rejected(string pickupName, string collectorName, PickupOutcome outcome)
        {
            if (outcome.IsCollected)
            {
                throw new ArgumentException("outcome was collected; use Collected", nameof(outcome));
            }

            return Tag + " rejected pickup=" + Name(pickupName) + " by=" + Name(collectorName) + " kind=" + outcome.Kind + " reason=" + outcome.Rejection;
        }

        public static string Failed(string pickupName, string collectorName, PickupKind kind, string reason)
        {
            return Tag + " failed pickup=" + Name(pickupName) + " by=" + Name(collectorName) + " kind=" + kind + " reason=" + Name(reason);
        }

        public static string Respawned(string pickupName)
        {
            return Tag + " respawned pickup=" + Name(pickupName);
        }

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
