using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HordeTests
    {
        private const int _startingCurrency = 20;
        private const int _killReward = 10;
        private const int _killRewardPerWave = 5;
        private const float _rest = 5f;

        private static readonly int[] _zeroAndOne = { 0, 1 };
        private static readonly int[] _zero = { 0 };

        [Test]
        public void SurvivingEveryWaveWinsForTheDefenders()
        {
            var horde = Playing(2, 2, out var schedule);

            ClearWave(horde);
            horde.Session.Tick(0f);
            Assert.That(horde.Session.State, Is.EqualTo(GameModeState.Playing), "one more wave to go");

            schedule.Tick(_rest);
            ClearWave(horde);
            horde.Session.Tick(0f);

            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Completed));
            Assert.That(horde.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(horde.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(horde.IsVictory, Is.True);
        }

        [Test]
        public void WhenEveryDefenderIsDownTheAttackersWin()
        {
            var horde = Playing(2, 2, out _);

            horde.Session.ReportDeath(0);
            horde.Session.Tick(0f);
            Assert.That(horde.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(horde.Session.IsAwaitingRespawn(0), Is.True, "a teammate is still standing, so player 0 can come back");

            horde.Session.ReportDeath(1);
            Assert.That(horde.Session.IsEliminated(0), Is.True);
            Assert.That(horde.Session.IsEliminated(1), Is.True);
            horde.Session.Tick(0f);

            Assert.That(horde.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(horde.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(horde.IsVictory, Is.False);
        }

        [Test]
        public void ALonePlayerWhoFallsLosesAtOnce()
        {
            var horde = Playing(1, 3, out _);

            horde.Session.ReportDeath(0);
            horde.Session.Tick(0f);

            Assert.That(horde.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
        }

        [Test]
        public void FallenAlliesStayDownUntilTheWaveIsCleared()
        {
            var horde = Playing(2, 2, out var schedule);
            var released = new List<int>();
            horde.Session.RespawnReady += released.Add;

            horde.Session.ReportDeath(0);
            horde.Session.Tick(30f);

            Assert.That(released, Is.Empty, "nobody comes back while the wave is being fought");
            Assert.That(horde.Session.IsDown(0), Is.True);
            Assert.That(horde.Session.GetRespawnRemaining(0), Is.Null, "the wait depends on the enemies, not on time");

            ClearWave(horde);
            Assert.That(schedule.Phase, Is.EqualTo(WavePhase.Resting));
            Assert.That(horde.Session.GetRespawnRemaining(0), Is.EqualTo(0f));
            horde.Session.Tick(0f);

            Assert.That(released, Is.EqualTo(_zero));
            Assert.That(horde.Session.IsDown(0), Is.False);
        }

        [Test]
        public void EveryoneDownBetweenWavesComesBackOnTheNextTick()
        {
            var horde = Playing(2, 2, out _);
            var released = new List<int>();
            horde.Session.RespawnReady += released.Add;
            ClearWave(horde);

            horde.Session.ReportDeath(1);
            horde.Session.ReportDeath(0);
            Assert.That(horde.Session.IsSideEliminated(Side.Alpha), Is.False, "between waves everyone comes back");
            horde.Session.Tick(0f);

            Assert.That(horde.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(released, Is.EqualTo(_zeroAndOne), "released in id order");
        }

        [Test]
        public void ClearingTheLastWaveInTheSameMomentTheLastDefenderFallsStillWins()
        {
            var horde = Playing(1, 1, out var schedule);
            SpawnAll(schedule);

            schedule.ReportKilled();
            schedule.ReportKilled();
            horde.Session.ReportDeath(0);
            horde.Session.Tick(0f);

            Assert.That(horde.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void KillsPayMoreInLaterWavesAndCountAsScore()
        {
            var horde = Playing(1, 2, out var schedule);
            SpawnAll(schedule);

            schedule.ReportKilled();
            Assert.That(horde.ReportEnemyKilled(), Is.EqualTo(_killReward));
            Assert.That(horde.Currency, Is.EqualTo(_startingCurrency + _killReward));

            schedule.ReportKilled();
            horde.ReportEnemyKilled();
            schedule.Tick(_rest);
            Assert.That(schedule.WaveNumber, Is.EqualTo(2));

            SpawnAll(schedule);
            schedule.ReportKilled();
            Assert.That(horde.ReportEnemyKilled(), Is.EqualTo(_killReward + _killRewardPerWave));

            Assert.That(horde.EnemiesKilled, Is.EqualTo(3));
            Assert.That(horde.Session.GetScore(Side.Alpha), Is.EqualTo(3d));
            Assert.That(horde.Currency, Is.EqualTo(_startingCurrency + (_killReward * 2) + _killReward + _killRewardPerWave));
        }

        [Test]
        public void KillsOutsideTheMatchEarnNothing()
        {
            var schedule = Schedule(2);
            var horde = new Horde(schedule, Spec(true));
            horde.Session.AddPlayer(0, Side.Alpha);

            Assert.That(horde.ReportEnemyKilled(), Is.EqualTo(0), "not started");

            horde.Session.Start();
            Assert.That(horde.ReportEnemyKilled(), Is.EqualTo(0), "no wave has started yet");
            Assert.That(horde.Currency, Is.EqualTo(_startingCurrency));
        }

        [Test]
        public void FortificationsCanOnlyBeBoughtBetweenWaves()
        {
            var horde = Playing(1, 2, out _);

            Assert.That(horde.CheckPurchase(5), Is.EqualTo(HordePurchaseVerdict.WaveInProgress));
            Assert.That(horde.TryPurchase(5), Is.EqualTo(HordePurchaseVerdict.WaveInProgress));
            Assert.That(horde.Currency, Is.EqualTo(_startingCurrency));

            ClearWave(horde);
            var currency = horde.Currency;

            Assert.That(horde.TryPurchase(currency + 1), Is.EqualTo(HordePurchaseVerdict.NotEnoughCurrency));
            Assert.That(horde.Currency, Is.EqualTo(currency));
            Assert.That(horde.TryPurchase(currency), Is.EqualTo(HordePurchaseVerdict.Ok));
            Assert.That(horde.Currency, Is.EqualTo(0));
            Assert.That(horde.TryPurchase(0), Is.EqualTo(HordePurchaseVerdict.Ok), "a free purchase needs no currency");
            Assert.Throws<ArgumentOutOfRangeException>(() => horde.CheckPurchase(-1));
        }

        [Test]
        public void PurchasesDuringAWaveCanBeAllowed()
        {
            var schedule = Schedule(1);
            var horde = new Horde(schedule, Spec(false));
            horde.Session.AddPlayer(0, Side.Alpha);
            horde.Session.Start();
            schedule.Start();

            Assert.That(horde.TryPurchase(5), Is.EqualTo(HordePurchaseVerdict.Ok));
            Assert.That(horde.Currency, Is.EqualTo(_startingCurrency - 5));
        }

        [Test]
        public void NothingCanBeBoughtOnceTheMatchIsOver()
        {
            var horde = Playing(1, 1, out _);
            horde.Session.ReportDeath(0);
            horde.Session.Tick(0f);

            Assert.That(horde.CheckPurchase(0), Is.EqualTo(HordePurchaseVerdict.NotPlaying));
        }

        [Test]
        public void TheSessionOnlyTakesDefendersAndNeverRunsOutOfTime()
        {
            var horde = new Horde(Schedule(1), Spec(true));

            Assert.That(horde.Session.Sides, Is.EqualTo(new List<Side> { Side.Alpha }));
            Assert.That(horde.Session.TimeLimitSeconds, Is.EqualTo(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => horde.Session.AddPlayer(1, Side.Bravo));
        }

        [Test]
        public void TheSpecRejectsBadValues()
        {
            Assert.Throws<ArgumentException>(() => new HordeSpec(Side.Alpha, Side.Alpha));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, startingCurrency: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, killReward: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeSpec(Side.Alpha, Side.Bravo, killRewardPerWave: -1));

            var spec = new HordeSpec(Side.Alpha, Side.Bravo, killReward: 10, killRewardPerWave: int.MaxValue);
            Assert.That(spec.KillRewardAt(1), Is.EqualTo(10));
            Assert.That(spec.KillRewardAt(3), Is.EqualTo(int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.KillRewardAt(0));
        }

        [Test]
        public void ASingleTeamPutsEveryoneOnOneSide()
        {
            var layout = new SingleTeam(new Side(3));

            Assert.That(layout.FixedSides, Is.EqualTo(new List<Side> { new Side(3) }));
            Assert.That(layout.Assign(0, _ => 0), Is.EqualTo((Side?)new Side(3)));
            Assert.That(layout.Assign(7, _ => 7), Is.EqualTo((Side?)new Side(3)));
            Assert.That(layout.Contains(new Side(3)), Is.True);
            Assert.That(layout.Contains(Side.Alpha), Is.False);
            Assert.That(layout.Capacity, Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.Assign(-1, _ => 0));
        }

        [Test]
        public void HudLinesAndLogsReadAsDocumented()
        {
            Assert.That(HordeText.Wave(2, 5), Is.EqualTo("WAVE 2/5"));
            Assert.That(HordeText.Wave(2, null), Is.EqualTo("WAVE 2"));
            Assert.That(HordeText.Wave(0, 5), Is.EqualTo("WAVE -/5"));
            Assert.That(HordeText.RemainingEnemies(4), Is.EqualTo("残りの敵 4"));
            Assert.That(HordeText.NextWave(4.2f), Is.EqualTo("次の波まで 5 秒"));
            Assert.That(HordeText.Currency(120), Is.EqualTo("通貨 120"));

            Assert.That(
                HordeText.Prompt("E", HordeFortificationAction.Repair, "Wall-North", 30, HordePurchaseVerdict.Ok),
                Is.EqualTo("[E] Wall-North を直す (30)"));
            Assert.That(
                HordeText.Prompt(null, HordeFortificationAction.Build, "Barricade NE", 40, HordePurchaseVerdict.Ok),
                Is.EqualTo("Barricade_NE を置く (40)"));
            Assert.That(
                HordeText.Prompt("E", HordeFortificationAction.Repair, "Wall-North", 30, HordePurchaseVerdict.WaveInProgress),
                Is.EqualTo("Wall-North を直す (30): 波の間だけ"));
            Assert.That(
                HordeText.Prompt("E", HordeFortificationAction.Build, "Wall-North", 30, HordePurchaseVerdict.NotEnoughCurrency),
                Is.EqualTo("Wall-North を置く (30): 通貨が足りない"));

            Assert.That(HordeText.WaveStartedLog(2, 5, 6), Is.EqualTo("[Mode] wave start number=2/5 enemies=6"));
            Assert.That(HordeText.WaveStartedLog(2, null, 6), Is.EqualTo("[Mode] wave start number=2/endless enemies=6"));
            Assert.That(HordeText.WaveClearedLog(2, 120, 1), Is.EqualTo("[Mode] wave cleared number=2 currency=120 returning=1"));
            Assert.That(HordeText.EarnedLog(2, 12, 120), Is.EqualTo("[Mode] currency +12 wave=2 total=120"));
            Assert.That(
                HordeText.PurchasedLog(HordeFortificationAction.Repair, "Wall-North", 3, 15, 105),
                Is.EqualTo("[Mode] repair structure=Wall-North pieces=3 cost=15 currency=105"));
            Assert.That(
                HordeText.DeniedLog(HordeFortificationAction.Build, "Barricade-NE", HordePurchaseVerdict.NotEnoughCurrency, 40, 10),
                Is.EqualTo("[Mode] purchase-denied action=build structure=Barricade-NE reason=NotEnoughCurrency cost=40 currency=10"));
            Assert.That(HordeText.ResultLog(true, 5, 5), Is.EqualTo("[Mode] horde victory waves=5/5"));
            Assert.That(HordeText.ResultLog(false, 3, 5), Is.EqualTo("[Mode] horde defeat wave=3/5"));
        }

        private static WaveSchedule Schedule(int waveCount)
        {
            var waves = new Wave[waveCount];
            for (var i = 0; i < waves.Length; i++)
            {
                waves[i] = new Wave(2, 1f, 2, _rest);
            }

            return WaveSchedule.FromTable(waves);
        }

        private static HordeSpec Spec(bool onlyBetweenWaves)
        {
            return new HordeSpec(Side.Alpha, Side.Bravo, _startingCurrency, _killReward, _killRewardPerWave, onlyBetweenWaves);
        }

        /// <summary>players 人の守る側で、waveCount 波 (どれも敵 2 体) の 1 波目を始めた状態。</summary>
        private static Horde Playing(int players, int waveCount, out WaveSchedule schedule)
        {
            schedule = Schedule(waveCount);
            var horde = new Horde(schedule, Spec(true));
            for (var id = 0; id < players; id++)
            {
                horde.Session.AddPlayer(id, Side.Alpha);
            }

            horde.Session.Start();
            schedule.Start();
            return horde;
        }

        private static void SpawnAll(WaveSchedule schedule)
        {
            while (schedule.SpawnBudget > 0)
            {
                schedule.ReportSpawned();
            }
        }

        private static void ClearWave(Horde horde)
        {
            var schedule = horde.Schedule;
            while (schedule.Phase == WavePhase.Fighting)
            {
                SpawnAll(schedule);
                schedule.ReportKilled();
                horde.ReportEnemyKilled();
            }
        }
    }
}
