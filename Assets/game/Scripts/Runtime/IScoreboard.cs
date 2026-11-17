namespace Fps.Runtime
{
    /// <summary>参加者のキル・デス・接続状態をチームごとに一覧にする表示。一覧の中身は <see cref="GameSignals.Scoreboard"/> から受ける。</summary>
    public interface IScoreboard
    {
        bool IsOpen { get; }
    }
}
