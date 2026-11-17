using System;

namespace Shooter.Core
{
    /// <summary>Interact で拾うと何が起きるか (<see cref="PickupHint.Action"/>)。</summary>
    public enum PickupHintAction
    {
        /// <summary>持っていない武器を持ち武器に加える。</summary>
        TakeWeapon = 0,

        /// <summary>持てる武器の数の上限に届いているので、今の武器を落として持ち替える。</summary>
        SwapWeapon = 1,

        /// <summary>持っている武器を拾って、その武器の予備弾にする。</summary>
        WeaponAmmo = 2,

        ReserveAmmo = 3,
        Health = 4,
        Armor = 5,
        Tag = 6,

        /// <summary>アイテムを持ち物に入れる。</summary>
        Item = 7,
    }

    /// <summary>
    /// Interact で拾う物の近くで出す案内の中身 (<see cref="InteractHint.Detail"/>)。何が起きるか・何を (武器かアイテムの Id と表示名のキー)・
    /// いくつ・どの入力 (<see cref="InputActionPath"/>) で拾うかを持つ。文にするのは UI。
    /// </summary>
    public readonly struct PickupHint : IEquatable<PickupHint>
    {
        /// <param name="action">拾うと何が起きるか。</param>
        /// <param name="contentId">武器の Id かアイテムの Id。どちらでもなければ null。</param>
        /// <param name="nameKey">武器かアイテムの表示名を引く表のキー。前後の空白は除き、null は空文字にする。</param>
        /// <param name="count">持ち物に入るアイテムの数。アイテムでなければ 0。</param>
        /// <param name="inputActionPath">拾う入力のアクション (例: <c>Player/Interact</c>)。null は空文字にする。</param>
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

        /// <summary>
        /// outcome (<see cref="PickupJudge.Judge"/> の結果) の物を拾う案内。拾えない outcome なら案内は出さない (false)。
        /// nameKey は武器かアイテムの表示名のキーで、武器とアイテムのときだけ持つ。
        /// </summary>
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
