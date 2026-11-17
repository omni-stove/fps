namespace Shooter.Core
{
    /// <summary>この画面のプレイヤーから見た試合の勝敗。ソロの試合 (<see cref="Match"/>) は <see cref="Draw"/> にならない。</summary>
    public enum MatchOutcome
    {
        Won,
        Lost,
        Draw,
    }
}
