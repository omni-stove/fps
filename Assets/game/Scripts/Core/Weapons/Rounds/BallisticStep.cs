using System.Numerics;

namespace Shooter.Core
{
    public readonly struct BallisticStep
    {
        public BallisticStep(BallisticState state, Vector3 from, Vector3 to, bool hasHit, WorldHit hit)
        {
            State = state;
            From = from;
            To = to;
            HasHit = hasHit;
            Hit = hit;
        }

        public BallisticState State { get; }

        public Vector3 From { get; }

        public Vector3 To { get; }

        // 跳ね返っただけで飛び続けた回も true にする。着弾の音や火花はその回にも出すため。
        public bool HasHit { get; }

        public WorldHit Hit { get; }
    }
}
