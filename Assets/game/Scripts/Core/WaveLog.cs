using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// PvE の波 (<see cref="WaveSchedule"/>) の <c>[Wave]</c> 付きログの文言。倍率は小数第 2 位、秒は小数第 1 位まで書く。
    /// </summary>
    public static class WaveLog
    {
        public const string Tag = "[Wave]";

        /// <summary>終わりの無い予定で total= に書く値。</summary>
        public const string Endless = "endless";

        /// <example><c>[Wave] start wave=2/5 enemies=6 strength=1.25 maxAlive=4</c></example>
        public static string Started(int number, int? waveCount, Wave wave)
        {
            return Tag + " start wave=" + Number(number) + "/" + Total(waveCount)
                + " enemies=" + Number(wave.EnemyCount)
                + " strength=" + wave.StrengthMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " maxAlive=" + Number(wave.MaxAlive);
        }

        /// <example><c>[Wave] spawn wave=2 enemy=wave2-3 point=WaveSpawn-1 alive=3 spawned=3/6</c></example>
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

        /// <summary>remaining はこの敵を除いた、波で倒すべき残りの敵の数 (まだ出していない分を含む)。全滅させたら 0。</summary>
        /// <example><c>[Wave] killed wave=2 enemy=wave2-3 remaining=4</c></example>
        public static string Killed(int number, string enemyId, int remaining)
        {
            return Tag + " killed wave=" + Number(number) + " enemy=" + Word(enemyId) + " remaining=" + Number(Math.Max(0, remaining));
        }

        /// <example><c>[Wave] cleared wave=2 rest=5.0</c>、最後の波なら <c>[Wave] cleared wave=5 rest=none</c></example>
        public static string Cleared(int number, float? restSeconds)
        {
            var rest = restSeconds.HasValue ? Seconds(restSeconds.Value) : "none";
            return Tag + " cleared wave=" + Number(number) + " rest=" + rest;
        }

        /// <example><c>[Wave] rest over next=3</c></example>
        public static string RestEnded(int nextNumber)
        {
            return Tag + " rest over next=" + Number(nextNumber);
        }

        /// <example><c>[Wave] complete waves=5</c></example>
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
