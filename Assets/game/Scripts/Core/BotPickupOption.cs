using System.Numerics;

namespace Fps.Core
{
    /// <summary><see cref="BattleRoyaleBotPlanner.Plan"/> に渡す、bot が拾えると判定した物 1 つ。</summary>
    public readonly struct BotPickupOption
    {
        public BotPickupOption(Vector3 position, bool givesWeapon)
        {
            Position = position;
            GivesWeapon = givesWeapon;
        }

        /// <summary>置いてある足元。</summary>
        public Vector3 Position { get; }

        /// <summary>拾うと持ち武器が増える (武器を持っていない bot が戦えるようになる) なら true。</summary>
        public bool GivesWeapon { get; }
    }
}
