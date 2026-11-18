namespace Shooter.Runtime
{
    // 画面は LeaderboardSpec のボードの Id で開き、結果は GameSignals.Leaderboard で受け取る。
    public interface ILeaderboardControl
    {
        bool Open(string boardId);

        bool Refresh();

        void Close();
    }
}
