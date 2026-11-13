namespace Fps.Core
{
    /// <summary>
    /// キルコンファームド (<see cref="KillConfirmed"/>) で倒れた人が落とした 1 枚のタグ。<see cref="KillConfirmed.DropTag"/> が作り、
    /// 拾われるか消えるまで <see cref="KillConfirmed.Tags"/> に残る。
    /// </summary>
    public sealed class KillConfirmedTag
    {
        internal KillConfirmedTag(int id, int ownerId, Side ownerSide, float secondsLeft)
        {
            Id = id;
            OwnerId = ownerId;
            OwnerSide = ownerSide;
            Remaining = secondsLeft;
        }

        /// <summary>試合の中で一意な番号。1 から振る。<see cref="KillConfirmed.Reset"/> でまた 1 から。</summary>
        public int Id { get; }

        /// <summary>落とした (倒れた) 人のモードでの ID。</summary>
        public int OwnerId { get; }

        /// <summary>落とした人の陣営。これと違う陣営が拾えば確定、同じ陣営が拾えば阻止。</summary>
        public Side OwnerSide { get; }

        /// <summary>消えるまでの秒数。拾われたか消えた後は 0。</summary>
        public float SecondsLeft => (float)Remaining;

        // float の Tick を float のまま引き続けると誤差が溜まって消える時刻がずれるので、double で数える。
        internal double Remaining { get; set; }
    }
}
