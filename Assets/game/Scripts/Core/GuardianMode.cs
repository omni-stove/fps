using System;

namespace Shooter.Core
{
    public static class GuardianMode
    {
        public const int DefaultPointsPerKill = 1;

        public static ModeSession CreateSession(
            float timeLimitSeconds,
            float respawnSeconds,
            Func<int, int> pick,
            out GuardianRespawn leaders,
            int pointsPerKill = DefaultPointsPerKill)
        {
            if (pick == null)
            {
                throw new ArgumentNullException(nameof(pick));
            }

            // HUD はチームの点を整数で出すので、KillScore が取れる小数の点は断る。
            if (pointsPerKill < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerKill), pointsPerKill, "pointsPerKill must be at least 1");
            }

            var respawn = new GuardianRespawn(new IndividualRespawn(respawnSeconds));
            var session = new ModeSession(
                new TwoTeams(),
                timeLimitSeconds,
                new IScoreRule[] { new KillScore(pointsPerKill) },
                respawn,
                new IWinCondition[] { new LastSideStandingWin(), new LeaderStandingWin(respawn), new TimeLimitWin() });

            // Start は参加者を入れた後に呼ぶ約束なので、Playing になった時点で各チームの顔ぶれが揃っている。
            session.StateChanged += state =>
            {
                if (state == GameModeState.Playing)
                {
                    respawn.ChooseLeaders(session, pick);
                }
            };

            leaders = respawn;
            return session;
        }

        public static ModeSession CreateSession(
            float timeLimitSeconds,
            float respawnSeconds,
            Random random,
            out GuardianRespawn leaders,
            int pointsPerKill = DefaultPointsPerKill)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return CreateSession(timeLimitSeconds, respawnSeconds, count => random.Next(count), out leaders, pointsPerKill);
        }
    }
}
