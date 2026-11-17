using System;

namespace Shooter.Core
{
    public static class RespawnPolicy
    {
        public static IRespawnPolicy FromRules(GameRules rules, float respawnSeconds)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must not be negative and must be finite");
            }

            switch (rules.RespawnMode)
            {
                case RespawnMode.Individual:
                    return new IndividualRespawn(respawnSeconds);
                case RespawnMode.Wave:
                    return new WaveRespawn(respawnSeconds, rules.WaveIntervalSeconds);
                case RespawnMode.None:
                    return new NoRespawn();
                default:
                    throw new ArgumentOutOfRangeException(nameof(rules), rules.RespawnMode, "unknown RespawnMode");
            }
        }
    }
}
