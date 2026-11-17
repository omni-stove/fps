using System;

namespace Fps.Core
{
    /// <summary>
    /// HUD に出す拠点 1 つの状態 (占領しているチーム・-100 (Bravo) から +100 (Alpha) の進み・押し合い)。
    /// 通信の手順 (Netcode の NetControlPointState・Fps.Protocol の ControlPointSnapshot) に依らずに HUD が描けるよう、ここで受け取る。
    /// </summary>
    public readonly struct ControlPointStatus : IEquatable<ControlPointStatus>
    {
        public const int FullPercent = 100;

        /// <param name="owner">占領しているチーム。中立なら null。</param>
        /// <param name="controlPercent">占領の進み。範囲の外は端に寄せる。</param>
        /// <param name="contested">両チームがいて進みが止まっていれば true。</param>
        public ControlPointStatus(Team? owner, int controlPercent, bool contested)
        {
            Owner = owner;
            ControlPercent = Math.Max(-FullPercent, Math.Min(FullPercent, controlPercent));
            Contested = contested;
        }

        public Team? Owner { get; }

        public int ControlPercent { get; }

        public bool Contested { get; }

        /// <summary>占領の進み。-1 (Bravo) から +1 (Alpha)。</summary>
        public float Control => ControlPercent / (float)FullPercent;

        /// <summary>進みが寄っている側のチーム。0 なら null。</summary>
        public Team? LeaningTeam => ControlPercent > 0 ? Team.Alpha : ControlPercent < 0 ? Team.Bravo : (Team?)null;

        /// <summary>寄っている側への進みの大きさ。0 (中立) から 1 (占領し終えた)。</summary>
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
