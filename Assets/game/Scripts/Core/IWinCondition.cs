namespace Shooter.Core
{
    public interface IWinCondition
    {
        double? ScoreLimit { get; }

        // 倒れた瞬間ではなく Tick の頭でまとめて調べるのは、同じ間の相打ちを報告の順番によらず同時のものとして扱うため。
        ModeOutcome CheckStanding(ModeSession session);

        ModeOutcome CheckTimeUp(ModeSession session);
    }
}
