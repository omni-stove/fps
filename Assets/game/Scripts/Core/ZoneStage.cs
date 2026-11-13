namespace Fps.Core
{
    /// <summary>縮むエリア (<see cref="ZoneSchedule"/>) の、ある時刻での動き。</summary>
    public enum ZoneStage
    {
        /// <summary>段階の始めで、今の円のまま縮み始めるのを待っている。</summary>
        Waiting,

        /// <summary>次の円へ縮んでいる途中。</summary>
        Shrinking,

        /// <summary>すべての段階を終え、最後の円のまま止まっている。</summary>
        Closed,
    }
}
