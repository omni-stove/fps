namespace Shooter.Core
{
    /// <summary>スコアボードの名前が誰を指すか。<see cref="ScoreboardName"/> が持ち、UI がこれで言い方を決める。</summary>
    public enum ScoreboardNameKind
    {
        /// <summary>言葉の入らない名前 (敵の名前など)。<see cref="ScoreboardName.Text"/> をそのまま出す。</summary>
        Literal,

        /// <summary>この画面のプレイヤー自身。</summary>
        LocalPlayer,

        /// <summary>オンラインの参加者。<see cref="ScoreboardName.Number"/> はプレイヤー Id。</summary>
        Player,

        /// <summary>オフラインの標的。<see cref="ScoreboardName.Number"/> は 1 から数える通し番号。</summary>
        Target,

        /// <summary>Bot。<see cref="ScoreboardName.Number"/> は 1 から数える通し番号 (<see cref="BotRoster.BotNumber"/>)。</summary>
        Bot,
    }
}
