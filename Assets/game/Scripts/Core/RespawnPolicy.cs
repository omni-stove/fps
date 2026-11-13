using System;

namespace Fps.Core
{
    /// <summary><see cref="IRespawnPolicy"/> を <see cref="GameRules"/> から作る。</summary>
    public static class RespawnPolicy
    {
        /// <summary>
        /// rules の <see cref="GameRules.RespawnMode"/> に合わせて、Individual なら <see cref="IndividualRespawn"/>、
        /// Wave なら <see cref="WaveRespawn"/> (間隔は <see cref="GameRules.WaveIntervalSeconds"/>)、None なら <see cref="NoRespawn"/> を作る。
        /// </summary>
        /// <param name="rules">モードのルール。</param>
        /// <param name="respawnSeconds">倒されてから戻れるまでの秒数。0 以上の有限値。None でも検査する。</param>
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
