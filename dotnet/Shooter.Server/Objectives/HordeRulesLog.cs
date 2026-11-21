using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 波の始まり・全滅・結果と通貨の行はオフラインと同じ HordeLog・WaveLog で書き、ここにはサーバーだけの出来事 (Bot の出し入れ・人の戻り) を置く
    internal static class HordeRulesLog
    {
        public const string Tag = "[Horde]";

        public static string Started(HordeSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return Tag + " net waves=" + (settings.WaveCount == 0 ? WaveLog.Endless : Text(settings.WaveCount))
                + " rest=" + Seconds(settings.RestSeconds)
                + " firstEnemies=" + Text(settings.FirstEnemyCount)
                + " enemiesPerWave=" + Text(settings.EnemiesPerWave)
                + " maxAlive=" + Text(settings.MaxAlive)
                + " currency=" + Text(settings.StartingCurrency)
                + " corpse=" + Seconds(settings.CorpseSeconds)
                + " alert=" + Seconds(settings.AlertInterval);
        }

        public static string EnemyJoined(int botId, WaveSchedule schedule)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            return Tag + " enemy player=" + Text(botId) + " wave=" + Text(schedule.WaveNumber)
                + " alive=" + Text(schedule.Alive) + " spawned=" + Text(schedule.Spawned) + "/" + Text(schedule.CurrentWave.EnemyCount);
        }

        // 波の数より多く足された Bot (ServerBots が足した後に波が進んだなど) は数えずに、次の足し引きで抜かせる
        public static string SpareBot(int botId, WavePhase phase)
        {
            return Tag + " spare player=" + Text(botId) + " phase=" + phase + "; removing it";
        }

        public static string EnemyKilled(int botId, int killerId, int waveNumber, int remaining)
        {
            return Tag + " enemy killed player=" + Text(botId) + " by=" + Text(killerId) + " wave=" + Text(waveNumber)
                + " remaining=" + Text(Math.Max(0, remaining));
        }

        // オフラインの WaveSpawner と同じく、倒されずに試合を抜けた敵は倒されたとして数える (波が終わらなくならないよう)
        public static string EnemyVanished(int botId, int waveNumber, int remaining)
        {
            return Tag + " enemy left player=" + Text(botId) + " wave=" + Text(waveNumber)
                + " remaining=" + Text(Math.Max(0, remaining)) + "; counted as killed without a reward";
        }

        public static string Returned(int playerId, int waveNumber)
        {
            return Tag + " returned player=" + Text(playerId) + " after wave=" + Text(waveNumber);
        }

        public static string NoDefenders(int waveNumber)
        {
            return Tag + " no defenders are left in wave=" + Text(waveNumber) + "; the enemies win";
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

        public static string Denied(int playerId, HordePurchaseVerdict verdict)
        {
            return Tag + " fortify-denied player=" + Text(playerId) + " reason=" + verdict;
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Seconds(float value) => value.ToString("F1", CultureInfo.InvariantCulture) + "s";
    }
}
