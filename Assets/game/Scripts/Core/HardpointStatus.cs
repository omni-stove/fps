namespace Fps.Core
{
    /// <summary>ハードポイント (<see cref="Hardpoint"/>) の有効な拠点の様子。</summary>
    public enum HardpointStatus
    {
        /// <summary>生きている参加者が誰もいない。ポイントは入らない。</summary>
        Empty = 0,

        /// <summary>1 つのチームだけがいる。そのチームにポイントが入る。</summary>
        Held = 1,

        /// <summary>両チームがいる。ポイントは止まる。</summary>
        Contested = 2,
    }
}
