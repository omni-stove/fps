namespace Fps.Core
{
    /// <summary><see cref="BotErrand"/> の用事の種類。</summary>
    public enum BotErrandKind
    {
        /// <summary>用事は無い。いつもの判断 (巡回・追跡) に任せる。</summary>
        None,

        /// <summary>縮むエリアの中へ移る。</summary>
        EnterZone,

        /// <summary>ダウンした分隊の味方のところへ行って起こす。</summary>
        Revive,

        /// <summary>拾える物を取りに行く。</summary>
        Collect,
    }
}
