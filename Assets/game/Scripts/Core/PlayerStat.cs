namespace Shooter.Core
{
    // 実績とリーダーボードが数える値。名前は JSON に書くので、並びを変えても読み方は変わらない。
    public enum PlayerStat
    {
        Kills,
        Deaths,
        Assists,
        Headshots,
        MeleeKills,
        MatchesPlayed,
        MatchesWon,
    }
}
