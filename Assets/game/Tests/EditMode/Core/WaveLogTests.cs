using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WaveLogTests
    {
        [Test]
        public void StartedWritesTheNumberCountAndStrength()
        {
            Assert.That(
                WaveLog.Started(2, 5, new Wave(6, 1.25f, 4, 5f)),
                Is.EqualTo("[Wave] start wave=2/5 enemies=6 strength=1.25 maxAlive=4"));
            Assert.That(
                WaveLog.Started(7, null, new Wave(1, 1f, 1, 0f)),
                Is.EqualTo("[Wave] start wave=7/endless enemies=1 strength=1.00 maxAlive=1"));
        }

        [Test]
        public void SpawnedWritesTheProgressOfTheWave()
        {
            var schedule = WaveSchedule.FromTable(new[] { new Wave(6, 1f, 4, 0f) });
            schedule.Start();
            schedule.ReportSpawned();
            schedule.ReportSpawned();

            Assert.That(
                WaveLog.Spawned(schedule, "wave1-2", "WaveSpawn West"),
                Is.EqualTo("[Wave] spawn wave=1 enemy=wave1-2 point=WaveSpawn_West alive=2 spawned=2/6"));
        }

        [Test]
        public void KilledWritesTheRemainingEnemiesAndNeverBelowZero()
        {
            Assert.That(WaveLog.Killed(2, "wave2-3", 4), Is.EqualTo("[Wave] killed wave=2 enemy=wave2-3 remaining=4"));
            Assert.That(WaveLog.Killed(2, null, -1), Is.EqualTo("[Wave] killed wave=2 enemy=- remaining=0"));
        }

        [Test]
        public void ClearedWritesTheRestOrNone()
        {
            Assert.That(WaveLog.Cleared(2, 5f), Is.EqualTo("[Wave] cleared wave=2 rest=5.0"));
            Assert.That(WaveLog.Cleared(5, null), Is.EqualTo("[Wave] cleared wave=5 rest=none"));
        }

        [Test]
        public void RestEndedAndCompletedNameTheWaves()
        {
            Assert.That(WaveLog.RestEnded(3), Is.EqualTo("[Wave] rest over next=3"));
            Assert.That(WaveLog.Completed(5), Is.EqualTo("[Wave] complete waves=5"));
        }
    }
}
