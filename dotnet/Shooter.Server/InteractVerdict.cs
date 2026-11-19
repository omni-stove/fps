using System;

namespace Shooter.Server
{
    // 開ける要求 (InteractRequestMessage) への目的の答え。中身を渡すのは目的の側 (ServerInventories.Give など) なので、ここには受けたか断ったかだけを持つ
    internal readonly struct InteractVerdict : IEquatable<InteractVerdict>
    {
        public static readonly InteractVerdict Accepted = new InteractVerdict(true, InteractRefusal.None);

        private readonly bool _accepted;
        private readonly InteractRefusal _refusal;

        private InteractVerdict(bool accepted, InteractRefusal refusal)
        {
            _accepted = accepted;
            _refusal = refusal;
        }

        public bool IsAccepted => _accepted;

        // default を受けたことにしないよう、理由の無い断りは開けられない物として読む
        public InteractRefusal Refusal => _accepted ? InteractRefusal.None : _refusal == InteractRefusal.None ? InteractRefusal.NotInteractable : _refusal;

        public static InteractVerdict Refused(InteractRefusal refusal)
        {
            if (refusal == InteractRefusal.None || !Enum.IsDefined(refusal))
            {
                throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "refusal must name a reason");
            }

            return new InteractVerdict(false, refusal);
        }

        public bool Equals(InteractVerdict other) => IsAccepted == other.IsAccepted && Refusal == other.Refusal;

        public override bool Equals(object obj) => obj is InteractVerdict other && Equals(other);

        public override int GetHashCode() => IsAccepted ? -1 : (int)Refusal;

        public static bool operator ==(InteractVerdict left, InteractVerdict right) => left.Equals(right);

        public static bool operator !=(InteractVerdict left, InteractVerdict right) => !left.Equals(right);
    }
}
