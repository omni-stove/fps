using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WaveScheduleTests
    {
        // 1 波目は 2 体を同時に 1 体ずつ、休憩 3 秒。2 波目は 3 体を同時に 2 体まで、強さ 1.5 倍。
        private static WaveSchedule TwoWaves() => WaveSchedule.FromTable(new[]
        {
            new Wave(2, 1f, 1, 3f),
            new Wave(3, 1.5f, 2, 0f),
        });

        private static readonly string[] FirstWaveStartEvents = { "start 1 enemies=2" };

        private static readonly string[] RestThenSecondWaveEvents =
        {
            "start 1 enemies=2", "cleared 1 phase=Resting", "rest over 2", "start 2 enemies=3",
        };

        private static List<string> Record(WaveSchedule schedule)
        {
            var events = new List<string>();
            schedule.WaveStarted += (number, wave) => events.Add("start " + number + " enemies=" + wave.EnemyCount);
            schedule.WaveCleared += number => events.Add("cleared " + number + " phase=" + schedule.Phase);
            schedule.RestEnded += number => events.Add("rest over " + number);
            schedule.Completed += () => events.Add("completed");
            return events;
        }

        private static void SpawnAndKillAll(WaveSchedule schedule)
        {
            var number = schedule.WaveNumber;
            while (schedule.Phase == WavePhase.Fighting && schedule.WaveNumber == number)
            {
                while (schedule.SpawnBudget > 0)
                {
                    schedule.ReportSpawned();
                }

                schedule.ReportKilled();
            }
        }

        [Test]
        public void StartBeginsTheFirstWaveWithoutRest()
        {
            var schedule = TwoWaves();
            var events = Record(schedule);
            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Waiting));
            Assert.That(schedule.WaveNumber, Is.EqualTo(0));
            Assert.That(schedule.SpawnBudget, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => _ = schedule.CurrentWave);

            schedule.Start();

            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Fighting));
            Assert.That(schedule.WaveNumber, Is.EqualTo(1));
            Assert.That(schedule.CurrentWave, Is.EqualTo(new Wave(2, 1f, 1, 3f)));
            Assert.That(events, Is.EqualTo(FirstWaveStartEvents));
            Assert.Throws<InvalidOperationException>(schedule.Start);
        }

        [Test]
        public void SpawnBudgetKeepsAtMostMaxAliveOut()
        {
            var schedule = TwoWaves();
            schedule.Start();

            Assert.That(schedule.SpawnBudget, Is.EqualTo(1));
            schedule.ReportSpawned();
            Assert.That(schedule.SpawnBudget, Is.EqualTo(0), "max alive is 1");
            Assert.Throws<InvalidOperationException>(schedule.ReportSpawned);

            schedule.ReportKilled();
            Assert.That(schedule.Alive, Is.EqualTo(0));
            Assert.That(schedule.RemainingEnemies, Is.EqualTo(1));
            Assert.That(schedule.SpawnBudget, Is.EqualTo(1), "a kill frees a slot for the last enemy");

            schedule.ReportSpawned();
            Assert.That(schedule.RemainingToSpawn, Is.EqualTo(0));
            Assert.That(schedule.SpawnBudget, Is.EqualTo(0));
        }

        [Test]
        public void SpawnBudgetNeverExceedsTheEnemiesLeftToSpawn()
        {
            var schedule = WaveSchedule.FromTable(new[] { new Wave(3, 1f, 10, 0f) });
            schedule.Start();

            Assert.That(schedule.SpawnBudget, Is.EqualTo(3));
        }

        [Test]
        public void ClearingAWaveRestsThenStartsTheNextOnTheTickThatEndsTheRest()
        {
            var schedule = TwoWaves();
            var events = Record(schedule);
            schedule.Start();
            SpawnAndKillAll(schedule);

            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Resting));
            Assert.That(schedule.WaveNumber, Is.EqualTo(1), "the cleared wave stays current while resting");
            Assert.That(schedule.RestRemaining, Is.EqualTo(3f));
            Assert.That(schedule.NextWaveNumber, Is.EqualTo(2));
            Assert.That(schedule.SpawnBudget, Is.EqualTo(0));

            schedule.Tick(2f);
            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Resting));
            Assert.That(schedule.RestRemaining, Is.EqualTo(1f).Within(1e-5f));

            schedule.Tick(1f);
            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Fighting));
            Assert.That(schedule.WaveNumber, Is.EqualTo(2));
            Assert.That(schedule.Spawned, Is.EqualTo(0));
            Assert.That(schedule.Killed, Is.EqualTo(0));
            Assert.That(schedule.CurrentWave.StrengthMultiplier, Is.EqualTo(1.5f));
            Assert.That(schedule.SpawnBudget, Is.EqualTo(2));
            Assert.That(
                events,
                Is.EqualTo(RestThenSecondWaveEvents));
        }

        [Test]
        public void ZeroRestStartsTheNextWaveOnTheNextTick()
        {
            var schedule = WaveSchedule.FromTable(new[] { new Wave(1, 1f, 1, 0f), new Wave(1, 1f, 1, 0f) });
            schedule.Start();
            SpawnAndKillAll(schedule);
            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Resting));

            schedule.Tick(0f);

            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Fighting));
            Assert.That(schedule.WaveNumber, Is.EqualTo(2));
        }

        [Test]
        public void ClearingTheLastWaveCompletesOnce()
        {
            var schedule = TwoWaves();
            var events = Record(schedule);
            schedule.Start();
            SpawnAndKillAll(schedule);
            schedule.Tick(3f);
            SpawnAndKillAll(schedule);

            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Completed));
            Assert.That(schedule.WaveNumber, Is.EqualTo(2));
            Assert.That(schedule.NextWaveNumber, Is.Null);
            Assert.That(schedule.RemainingEnemies, Is.EqualTo(0));
            Assert.That(events[events.Count - 2], Is.EqualTo("cleared 2 phase=Completed"));
            Assert.That(events[events.Count - 1], Is.EqualTo("completed"));

            schedule.Tick(10f);
            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Completed));
            Assert.Throws<InvalidOperationException>(schedule.ReportKilled);
        }

        [Test]
        public void KillsAreOnlyAcceptedForSpawnedEnemiesWhileFighting()
        {
            var schedule = TwoWaves();
            // 始める前
            Assert.Throws<InvalidOperationException>(schedule.ReportKilled);

            // まだ誰も出していない
            schedule.Start();
            Assert.Throws<InvalidOperationException>(schedule.ReportKilled);

            // 休憩中
            SpawnAndKillAll(schedule);
            Assert.Throws<InvalidOperationException>(schedule.ReportKilled);
            Assert.Throws<InvalidOperationException>(schedule.ReportSpawned);
        }

        [Test]
        public void FormulaScheduleGrowsAndEndsAfterTheWaveCount()
        {
            var formula = new WaveFormula(3, 2f, 1f, 0.25f, 4, 5f);
            var schedule = WaveSchedule.FromFormula(formula, 3);

            Assert.That(schedule.WaveCount, Is.EqualTo(3));
            Assert.That(schedule.WaveAt(1), Is.EqualTo(new Wave(3, 1f, 4, 5f)));
            Assert.That(schedule.WaveAt(2), Is.EqualTo(new Wave(5, 1.25f, 4, 5f)));
            Assert.That(schedule.WaveAt(3), Is.EqualTo(new Wave(7, 1.5f, 4, 5f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.WaveAt(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.WaveAt(0));

            schedule.Start();
            for (var number = 1; number <= 3; number++)
            {
                Assert.That(schedule.WaveNumber, Is.EqualTo(number));
                SpawnAndKillAll(schedule);
                schedule.Tick(5f);
            }

            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Completed));
        }

        [Test]
        public void FormulaScheduleWithZeroWavesNeverEnds()
        {
            var schedule = WaveSchedule.FromFormula(new WaveFormula(1, 0f, 1f, 0f, 1, 0f), 0);
            var completed = false;
            schedule.Completed += () => completed = true;

            Assert.That(schedule.WaveCount, Is.Null);
            schedule.Start();
            for (var i = 0; i < 20; i++)
            {
                SpawnAndKillAll(schedule);
                schedule.Tick(0f);
            }

            Assert.That(schedule.WaveNumber, Is.EqualTo(21));
            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Fighting));
            Assert.That(completed, Is.False);
            Assert.That(schedule.WaveAt(1000).EnemyCount, Is.EqualTo(1));
        }

        [Test]
        public void TableIsCopiedAndRejectsEmptyOrDefaultWaves()
        {
            var waves = new[] { new Wave(1, 1f, 1, 0f) };
            var schedule = WaveSchedule.FromTable(waves);
            waves[0] = new Wave(9, 2f, 9, 9f);

            Assert.That(schedule.WaveAt(1), Is.EqualTo(new Wave(1, 1f, 1, 0f)));
            Assert.Throws<ArgumentNullException>(() => WaveSchedule.FromTable(null));
            Assert.Throws<ArgumentException>(() => WaveSchedule.FromTable(Array.Empty<Wave>()));
            Assert.Throws<ArgumentException>(() => WaveSchedule.FromTable(new[] { new Wave(1, 1f, 1, 0f), default(Wave) }));
            Assert.Throws<ArgumentNullException>(() => WaveSchedule.FromFormula(null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaveSchedule.FromFormula(new WaveFormula(1, 0f, 1f, 0f, 1, 0f), -1));
        }

        [Test]
        public void TickRejectsNegativeOrNonFiniteTime()
        {
            var schedule = TwoWaves();
            schedule.Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.Tick(float.PositiveInfinity));
        }

        [Test]
        public void HandlersSeeTheStateAfterTheChange()
        {
            var schedule = TwoWaves();
            var budgetAtStart = -1;
            schedule.WaveStarted += (number, wave) => budgetAtStart = schedule.SpawnBudget;
            var restAtClear = -1f;
            schedule.WaveCleared += number => restAtClear = schedule.RestRemaining;

            schedule.Start();
            Assert.That(budgetAtStart, Is.EqualTo(1));

            SpawnAndKillAll(schedule);
            Assert.That(restAtClear, Is.EqualTo(3f));
        }
    }
}
