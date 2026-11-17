using System;

namespace Shooter.Core
{
    public enum PickupHintAction
    {
        TakeWeapon = 0,

        SwapWeapon = 1,

        WeaponAmmo = 2,

        ReserveAmmo = 3,
        Health = 4,
        Armor = 5,
        Tag = 6,

        Item = 7,
    }

    public readonly struct PickupHint : IEquatable<PickupHint>
    {
        public PickupHint(PickupHintAction action, string contentId, string nameKey, int count, string inputActionPath)
        {
            Action = action;
            ContentId = contentId;
            NameKey = nameKey?.Trim() ?? string.Empty;
            Count = count;
            InputActionPath = inputActionPath ?? string.Empty;
        }

        public PickupHintAction Action { get; }

        public string ContentId { get; }

        public string NameKey { get; }

        public int Count { get; }

        public string InputActionPath { get; }

        public static bool TryFrom(PickupOutcome outcome, string nameKey, string inputActionPath, out PickupHint hint)
        {
            hint = default;
            if (!outcome.IsCollected)
            {
                return false;
            }

            switch (outcome.Kind)
            {
                case PickupKind.Weapon:
                    var action = outcome.DroppedWeaponId != null ? PickupHintAction.SwapWeapon
                        : outcome.AddsWeapon ? PickupHintAction.TakeWeapon
                        : PickupHintAction.WeaponAmmo;
                    hint = new PickupHint(action, outcome.WeaponId, nameKey, 0, inputActionPath);
                    return true;
                case PickupKind.ReserveAmmo:
                    hint = new PickupHint(PickupHintAction.ReserveAmmo, null, null, 0, inputActionPath);
                    return true;
                case PickupKind.Health:
                    hint = new PickupHint(PickupHintAction.Health, null, null, 0, inputActionPath);
                    return true;
                case PickupKind.Armor:
                    hint = new PickupHint(PickupHintAction.Armor, null, null, 0, inputActionPath);
                    return true;
                case PickupKind.Tag:
                    hint = new PickupHint(PickupHintAction.Tag, null, null, 0, inputActionPath);
                    return true;
                case PickupKind.Item:
                    hint = new PickupHint(PickupHintAction.Item, outcome.ItemId, nameKey, outcome.Items, inputActionPath);
                    return true;
                default:
                    return false;
            }
        }

        public bool Equals(PickupHint other)
        {
            return Action == other.Action
                && string.Equals(ContentId, other.ContentId, StringComparison.Ordinal)
                && string.Equals(NameKey, other.NameKey, StringComparison.Ordinal)
                && Count == other.Count
                && string.Equals(InputActionPath, other.InputActionPath, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PickupHint other && Equals(other);
        }

        public static bool operator ==(PickupHint left, PickupHint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PickupHint left, PickupHint right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Action, ContentId, NameKey, Count, InputActionPath);
        }

        public override string ToString()
        {
            return $"pickup action={Action} content={ContentId ?? "none"} name={NameKey} count={Count} input={InputActionPath}";
        }
    }
}
