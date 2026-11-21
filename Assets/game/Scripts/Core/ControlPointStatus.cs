using System;

namespace Shooter.Core
{
    // 通信の手順 (Shooter.Protocol の ControlPointSnapshot) に依らずに HUD が描けるよう、ここで受け取る。
    public readonly struct ControlPointStatus : IEquatable<ControlPointStatus>
    {
        public const int FullPercent = 100;

        public ControlPointStatus(Team? owner, int controlPercent, bool contested)
        {
            Owner = owner;
            ControlPercent = Math.Max(-FullPercent, Math.Min(FullPercent, controlPercent));
            Contested = contested;
        }

        public Team? Owner { get; }

        public int ControlPercent { get; }

        public bool Contested { get; }

        public float Control => ControlPercent / (float)FullPercent;

        public Team? LeaningTeam => ControlPercent > 0 ? Team.Alpha : ControlPercent < 0 ? Team.Bravo : (Team?)null;

        public float Fill => Math.Abs(Control);

        // サーバーが送る値 (MatchRulesModule.ToPercent) と揃え、1% 未満の動きで出し直さないよう 0 に向けて切り捨てる
        public static int PercentOf(float control)
        {
            if (float.IsNaN(control))
            {
                return 0;
            }

            var clamped = Math.Max(-1f, Math.Min(1f, control));
            return (int)Math.Truncate(clamped * FullPercent);
        }

        public bool Equals(ControlPointStatus other)
        {
            return Owner == other.Owner && ControlPercent == other.ControlPercent && Contested == other.Contested;
        }

        public override bool Equals(object obj)
        {
            return obj is ControlPointStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Owner, ControlPercent, Contested);
        }

        public static bool operator ==(ControlPointStatus left, ControlPointStatus right) => left.Equals(right);

        public static bool operator !=(ControlPointStatus left, ControlPointStatus right) => !left.Equals(right);
    }
}
