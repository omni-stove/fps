using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class WaveLog
    {
        public const string Tag = "[Wave]";

        public const string Endless = "endless";

        public static string Started(int number, int? waveCount, Wave wave)
        {
            return Tag + " start wave=" + Number(number) + "/" + Total(waveCount)
                + " enemies=" + Number(wave.EnemyCount)
                + " strength=" + wave.StrengthMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " maxAlive=" + Number(wave.MaxAlive);
        }

        public static string Spawned(WaveSchedule schedule, string enemyId, string point)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            return Tag + " spawn wave=" + Number(schedule.WaveNumber)
                + " enemy=" + Word(enemyId)
                + " point=" + Word(point)
                + " alive=" + Number(schedule.Alive)
                + " spawned=" + Number(schedule.Spawned) + "/" + Number(schedule.CurrentWave.EnemyCount);
        }

        public static string Killed(int number, string enemyId, int remaining)
        {
            return Tag + " killed wave=" + Number(number) + " enemy=" + Word(enemyId) + " remaining=" + Number(Math.Max(0, remaining));
        }

        public static string Cleared(int number, float? restSeconds)
        {
            var rest = restSeconds.HasValue ? Seconds(restSeconds.Value) : "none";
            return Tag + " cleared wave=" + Number(number) + " rest=" + rest;
        }

        public static string RestEnded(int nextNumber)
        {
            return Tag + " rest over next=" + Number(nextNumber);
        }

        public static string Completed(int waveCount)
        {
            return Tag + " complete waves=" + Number(waveCount);
        }

        private static string Total(int? waveCount) => waveCount.HasValue ? Number(waveCount.Value) : Endless;

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Seconds(float value) => value.ToString("F1", CultureInfo.InvariantCulture);

        // 空白を含む値はログを key=value で読む側を壊すので詰める。
        private static string Word(string value) => string.IsNullOrEmpty(value) ? "-" : value.Replace(' ', '_');
    }
}
