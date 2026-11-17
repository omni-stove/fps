namespace Shooter.Core
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

        /// <summary>分隊の指示: 指された場所へ行き、そこで守る (撃たれたら反撃はするが、追って離れない)。</summary>
        Hold,

        /// <summary>分隊の指示: 指された敵を狙う。見えなければその敵の居場所へ向かう。</summary>
        Attack,

        /// <summary>分隊の指示: 指示した人に付いていく。</summary>
        Follow,

        /// <summary>旗取り: 運んでいる敵の旗を自陣の台へ運ぶ。</summary>
        CarryFlag,

        /// <summary>旗取り: 落ちた自陣の旗に触れて台へ戻しに行く。</summary>
        RecoverFlag,

        /// <summary>旗取り: 攻めの役として敵の旗のある所へ行く。</summary>
        TakeFlag,

        /// <summary>旗取り: 守りの役として自陣の台を守る。</summary>
        DefendFlag,
    }
}
