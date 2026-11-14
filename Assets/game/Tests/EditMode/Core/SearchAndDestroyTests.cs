using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SearchAndDestroyTests
    {
        private const int _alphaOne = 0;
        private const int _alphaTwo = 1;
        private const int _bravoOne = 10;
        private const int _bravoTwo = 11;

        private const float _roundSeconds = 60f;
        private const float _plantSeconds = 3f;
        private const float _defuseSeconds = 5f;
        private const float _fuseSeconds = 20f;

        private static readonly int[] _expectedDroppedByAlphaOne = { _alphaOne };
        private static readonly int[] _expectedPickedUpByAlphaTwo = { _alphaTwo };

        private static readonly string[] _expectedSwapOrder =
        {
            "Preparation 1 attacker Side 0", "RoundEnded Side 1 TimeUp", "Swapped attacker Side 1",
            "Preparation 2 attacker Side 1", "RoundEnded Side 0 TimeUp", "Swapped attacker Side 0",
            "Preparation 3 attacker Side 0",
        };

        [Test]
        public void SpecRejectsOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SearchAndDestroySpec(siteCount: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SearchAndDestroySpec(roundSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SearchAndDestroySpec(plantSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SearchAndDestroySpec(defuseSeconds: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SearchAndDestroySpec(fuseSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SearchAndDestroySpec(winsToWin: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SearchAndDestroySpec(preparationSeconds: -0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SearchAndDestroySpec(swapEveryRounds: -1));
            Assert.Throws<ArgumentNullException>(() => new SearchAndDestroy(null));
        }

        [Test]
        public void BombGoesToTheAttackerWithTheLowestIdWhenTheRoundIsPrepared()
        {
            var game = Create(preparationSeconds: 5f);

            game.Start();

            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(game.Attacker, Is.EqualTo(Side.Alpha));
            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(game.CarrierId, Is.EqualTo(_alphaOne));
            Assert.That(game.RoundRemaining, Is.EqualTo(_roundSeconds));
        }

        [Test]
        public void PlantingDuringPreparationDoesNothing()
        {
            var game = Create(preparationSeconds: 5f);
            game.Start();
            game.SetPlanting(_alphaOne, 0);

            game.Tick(4f);

            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(game.PlantProgress, Is.Zero);
            Assert.That(game.RoundRemaining, Is.EqualTo(_roundSeconds));
        }

        [Test]
        public void HoldingOnASitePlantsAfterPlantSecondsAndStopsTheRoundClock()
        {
            var game = Create();
            var planted = new List<string>();
            game.BombPlanted += (player, site) => planted.Add(player + "@" + site);
            game.Start();

            game.SetPlanting(_alphaOne, 1);
            game.Tick(1f);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(game.PlantingSite, Is.EqualTo(1));
            Assert.That(game.PlantProgress, Is.EqualTo(1f));

            game.Tick(2f);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Planted));
            Assert.That(game.PlantedSite, Is.EqualTo(1));
            Assert.That(game.PlanterId, Is.EqualTo(_alphaOne));
            Assert.That(game.CarrierId, Is.Null);
            Assert.That(planted, Is.EqualTo(new List<string> { _alphaOne + "@1" }));
            Assert.That(game.RoundRemaining, Is.EqualTo(_roundSeconds - _plantSeconds));
            Assert.That(game.FuseRemaining, Is.EqualTo(_fuseSeconds));

            game.Tick(5f);
            Assert.That(game.RoundRemaining, Is.EqualTo(_roundSeconds - _plantSeconds));
            Assert.That(game.FuseRemaining, Is.EqualTo(_fuseSeconds - 5f));
        }

        [Test]
        public void ReleasingOrMovingToAnotherSiteStartsThePlantOver()
        {
            var game = Create();
            game.Start();

            game.SetPlanting(_alphaOne, 0);
            game.Tick(2f);
            game.SetPlanting(_alphaOne, null);
            game.Tick(0.5f);
            Assert.That(game.PlantProgress, Is.Zero);
            Assert.That(game.PlantingSite, Is.Null);

            game.SetPlanting(_alphaOne, 0);
            game.Tick(2f);
            game.SetPlanting(_alphaOne, 1);
            game.Tick(2f);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(game.PlantingSite, Is.EqualTo(1));
            Assert.That(game.PlantProgress, Is.EqualTo(2f));
        }

        [Test]
        public void OnlyTheCarrierCanPlant()
        {
            var game = Create();
            game.Start();

            game.SetPlanting(_alphaTwo, 0);
            game.SetPlanting(_bravoOne, 0);
            game.Tick(10f);

            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(game.PlantProgress, Is.Zero);
        }

        [Test]
        public void SetPlantingRejectsUnknownPlayersAndSites()
        {
            var game = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => game.SetPlanting(99, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.SetPlanting(_alphaOne, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.SetPlanting(_alphaOne, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.SetDefusing(99, true));
        }

        [Test]
        public void DetonationWinsTheRoundForTheAttackers()
        {
            var game = Create();
            var detonated = new List<int>();
            var ended = new List<string>();
            game.BombDetonated += detonated.Add;
            game.RoundEnded += (winner, reason) => ended.Add(winner + " " + reason);
            game.Start();
            Plant(game, 0);

            game.Tick(_fuseSeconds - 1f);
            Assert.That(ended, Is.Empty);

            game.Tick(1f);

            Assert.That(detonated, Is.EqualTo(new List<int> { 0 }));
            Assert.That(ended, Is.EqualTo(new List<string> { Side.Alpha + " " + RoundEndReason.Detonated }));
            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.Detonated));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Alpha));
            Assert.That(game.RoundNumber, Is.EqualTo(2));
        }

        [Test]
        public void HoldingNearThePlantedBombDefusesItForTheDefenders()
        {
            var game = Create();
            var defused = new List<int>();
            game.BombDefused += defused.Add;
            game.Start();
            Plant(game, 1);

            game.SetDefusing(_bravoTwo, true);
            game.Tick(4f);
            Assert.That(game.DefuserId, Is.EqualTo(_bravoTwo));
            Assert.That(game.DefuseProgress, Is.EqualTo(4f));
            Assert.That(game.Bomb, Is.EqualTo(BombState.Planted));

            game.Tick(1f);

            Assert.That(defused, Is.EqualTo(new List<int> { _bravoTwo }));
            Assert.That(game.GetRoundWins(Side.Bravo), Is.EqualTo(1));
            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.Defused));
        }

        [Test]
        public void ReleasingTheDefuseStartsItOver()
        {
            var game = Create();
            game.Start();
            Plant(game, 0);

            game.SetDefusing(_bravoOne, true);
            game.Tick(4f);
            game.SetDefusing(_bravoOne, false);
            game.Tick(1f);

            Assert.That(game.DefuseProgress, Is.Zero);
            Assert.That(game.DefuserId, Is.Null);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Planted));
        }

        [Test]
        public void AttackersCannotDefuse()
        {
            var game = Create();
            game.Start();
            Plant(game, 0);

            game.SetDefusing(_alphaTwo, true);
            game.Tick(_defuseSeconds + 1f);

            Assert.That(game.Bomb, Is.EqualTo(BombState.Planted));
            Assert.That(game.DefuseProgress, Is.Zero);
        }

        [Test]
        public void DefuseFinishingAtTheSameTimeAsDetonationCountsAsDefused()
        {
            var game = Create();
            game.Start();
            Plant(game, 0);
            game.Tick(_fuseSeconds - _defuseSeconds);

            game.SetDefusing(_bravoOne, true);
            game.Tick(_defuseSeconds);

            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.Defused));
            Assert.That(game.GetRoundWins(Side.Bravo), Is.EqualTo(1));
        }

        [Test]
        public void ALongTickPlantsAndCountsTheFuseFromThePlantTime()
        {
            var game = Create();
            game.Start();
            game.SetPlanting(_alphaOne, 0);

            game.Tick(_plantSeconds + 7f);

            Assert.That(game.Bomb, Is.EqualTo(BombState.Planted));
            Assert.That(game.RoundRemaining, Is.EqualTo(_roundSeconds - _plantSeconds));
            Assert.That(game.FuseRemaining, Is.EqualTo(_fuseSeconds - 7f));
        }

        [Test]
        public void TheRoundClockRunningOutBeforeThePlantFinishesWinsForTheDefenders()
        {
            var game = Create();
            game.Start();
            game.Tick(_roundSeconds - 1f);
            game.SetPlanting(_alphaOne, 0);

            game.Tick(2f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.TimeUp));
            Assert.That(game.LastRoundWinner, Is.EqualTo(Side.Bravo));
            Assert.That(game.GetRoundWins(Side.Bravo), Is.EqualTo(1));
        }

        [Test]
        public void KillingTheCarrierDropsTheBombAndAnotherAttackerCanPickItUp()
        {
            var game = Create();
            var dropped = new List<int>();
            var pickedUp = new List<int>();
            game.BombDropped += dropped.Add;
            game.BombPickedUp += pickedUp.Add;
            game.Start();
            game.SetPlanting(_alphaOne, 0);
            game.Tick(2f);

            game.ReportKill(_bravoOne, _alphaOne);

            Assert.That(game.Bomb, Is.EqualTo(BombState.Dropped));
            Assert.That(game.CarrierId, Is.Null);
            Assert.That(game.PlantProgress, Is.Zero);
            Assert.That(dropped, Is.EqualTo(_expectedDroppedByAlphaOne));

            Assert.That(game.PickUpBomb(_bravoOne), Is.False);
            Assert.That(game.PickUpBomb(_alphaOne), Is.False);
            Assert.That(game.PickUpBomb(_alphaTwo), Is.True);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(game.CarrierId, Is.EqualTo(_alphaTwo));
            Assert.That(pickedUp, Is.EqualTo(_expectedPickedUpByAlphaTwo));
            Assert.That(game.PickUpBomb(_alphaTwo), Is.False);
        }

        [Test]
        public void ACarrierReportedDownOnTheModeDropsTheBombOnTheNextTick()
        {
            var game = Create();
            game.Start();

            game.Mode.ReportKill(_bravoOne, _alphaOne);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));

            game.Tick(0f);

            Assert.That(game.Bomb, Is.EqualTo(BombState.Dropped));
        }

        [Test]
        public void RemovingTheCarrierDropsTheBombDuringTheRoundAndPassesItOnBefore()
        {
            var game = Create(preparationSeconds: 5f);
            game.Start();

            game.RemovePlayer(_alphaOne);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(game.CarrierId, Is.EqualTo(_alphaTwo));

            game.Tick(5f);
            game.RemovePlayer(_alphaTwo);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Dropped));
        }

        [Test]
        public void EliminatingTheAttackersBeforeThePlantWinsForTheDefenders()
        {
            var game = Create();
            game.Start();

            game.ReportKill(_bravoOne, _alphaOne);
            game.ReportKill(_bravoOne, _alphaTwo);
            game.Tick(0f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.AttackersEliminated));
            Assert.That(game.GetRoundWins(Side.Bravo), Is.EqualTo(1));
        }

        [Test]
        public void EliminatingTheAttackersAfterThePlantStillLetsTheBombGoOff()
        {
            var game = Create();
            game.Start();
            Plant(game, 0);

            game.ReportKill(_bravoOne, _alphaOne);
            game.ReportKill(_bravoOne, _alphaTwo);
            game.Tick(1f);
            Assert.That(game.RoundNumber, Is.EqualTo(1));
            Assert.That(game.Bomb, Is.EqualTo(BombState.Planted));

            game.Tick(_fuseSeconds);

            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.Detonated));
            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
        }

        [Test]
        public void EliminatingTheDefendersWinsForTheAttackersEvenAfterThePlant()
        {
            var game = Create();
            game.Start();
            Plant(game, 0);

            game.ReportKill(_alphaOne, _bravoOne);
            game.ReportKill(_alphaOne, _bravoTwo);
            game.Tick(0f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.DefendersEliminated));
            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
        }

        [Test]
        public void BothSidesEliminatedTogetherBeforeThePlantIsADraw()
        {
            var game = Create();
            game.Start();

            game.ReportKill(_bravoOne, _alphaOne);
            game.ReportKill(_bravoOne, _alphaTwo);
            game.ReportKill(_alphaOne, _bravoOne);
            game.ReportKill(_alphaOne, _bravoTwo);
            game.Tick(0f);

            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.BothEliminated));
            Assert.That(game.LastRoundWinner, Is.Null);
            Assert.That(game.GetRoundWins(Side.Alpha), Is.Zero);
            Assert.That(game.GetRoundWins(Side.Bravo), Is.Zero);
            Assert.That(game.RoundNumber, Is.EqualTo(2));
        }

        [Test]
        public void SidesSwapAndTheBombGoesToTheNewAttackers()
        {
            var game = Create(swapEveryRounds: 1, winsToWin: 5);
            var events = new List<string>();
            game.PreparationStarted += round => events.Add("Preparation " + round + " attacker " + game.Attacker);
            game.RoundEnded += (winner, reason) => events.Add("RoundEnded " + winner + " " + reason);
            game.SidesSwapped += () => events.Add("Swapped attacker " + game.Attacker);
            game.Start();

            game.Tick(_roundSeconds);
            Assert.That(game.Attacker, Is.EqualTo(Side.Bravo));
            Assert.That(game.CarrierId, Is.EqualTo(_bravoOne));

            game.Tick(_roundSeconds);

            Assert.That(events, Is.EqualTo(_expectedSwapOrder));
            Assert.That(game.CarrierId, Is.EqualTo(_alphaOne));
        }

        [Test]
        public void TheMatchEndsWhenASideReachesWinsToWin()
        {
            var game = Create(winsToWin: 2, swapEveryRounds: 0);
            var ended = new List<Side>();
            game.Ended += ended.Add;
            game.Start();

            Plant(game, 0);
            game.Tick(_fuseSeconds);
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Live));

            Plant(game, 1);
            game.Tick(_fuseSeconds);

            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Ended));
            Assert.That(game.Winner, Is.EqualTo(Side.Alpha));
            Assert.That(ended, Is.EqualTo(new List<Side> { Side.Alpha }));
            Assert.That(SearchAndDestroyText.Result(game), Is.EqualTo("ALPHA の勝ち"));

            game.Tick(_fuseSeconds);
            Assert.That(game.RoundNumber, Is.EqualTo(2));
        }

        [Test]
        public void ResetStartsTheMatchOver()
        {
            var game = Create(winsToWin: 3);
            game.Start();
            Plant(game, 0);
            game.Tick(_fuseSeconds);

            game.Reset();

            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(game.RoundNumber, Is.Zero);
            Assert.That(game.GetRoundWins(Side.Alpha), Is.Zero);
            Assert.That(game.LastRoundEnd, Is.Null);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(game.CarrierId, Is.EqualTo(_alphaOne));
        }

        [Test]
        public void ResetInsideThePlantHandlerStopsTheTick()
        {
            var game = Create();
            game.BombPlanted += (player, site) => game.Reset();
            game.Start();
            game.SetPlanting(_alphaOne, 0);

            game.Tick(_plantSeconds + _fuseSeconds);

            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
        }

        [Test]
        public void ClockShowsTheFuseAfterThePlant()
        {
            var game = Create(preparationSeconds: 5f);
            game.Start();
            Assert.That(SearchAndDestroyText.Clock(game), Is.EqualTo("00:05"));

            game.Tick(5f);
            Assert.That(SearchAndDestroyText.Clock(game), Is.EqualTo("01:00"));

            Plant(game, 0);
            game.Tick(0.5f);
            Assert.That(SearchAndDestroyText.Clock(game), Is.EqualTo("00:20"));
            Assert.That(SearchAndDestroyText.Lines(game, _bravoOne), Has.Member(SearchAndDestroyText.Fuse(0, _fuseSeconds - 0.5f)));
        }

        [Test]
        public void TextFormatsTheFuseAndTheRoundResult()
        {
            Assert.That(SearchAndDestroyText.Fuse(1, 12.3f), Is.EqualTo("爆弾 B に設置: 起爆まで 13 秒"));
            Assert.That(SearchAndDestroyText.RoundResult(Side.Bravo, RoundEndReason.Detonated), Is.EqualTo("BRAVO の勝ち (起爆)"));
            Assert.That(SearchAndDestroyText.RoundResult(null, RoundEndReason.BothEliminated), Is.EqualTo("引き分け (同時に全滅)"));
            Assert.That(
                SearchAndDestroyText.RoundEndLog(3, Side.Bravo, RoundEndReason.Defused, 1, 2),
                Is.EqualTo("[SearchAndDestroy] round_end round=3 winner=1 reason=Defused wins=0:1,1:2"));
            Assert.That(SearchAndDestroyText.PlantedLog(0, 0), Is.EqualTo("[SearchAndDestroy] planted player=0 site=A"));
        }

        [Test]
        public void ScoresShowRoundWinsAndRolesWithTheOwnSideFirst()
        {
            var game = Create();
            game.Start();
            Plant(game, 0);
            game.Tick(_fuseSeconds);

            var lines = SearchAndDestroyText.Scores(game, Side.Bravo);

            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0].Text, Is.EqualTo("BRAVO 0 (防衛)"));
            Assert.That(lines[0].IsOwn, Is.True);
            Assert.That(lines[0].IsLeading, Is.False);
            Assert.That(lines[1].Text, Is.EqualTo("ALPHA 1 (攻撃)"));
            Assert.That(lines[1].IsLeading, Is.True);
        }

        private static void Plant(SearchAndDestroy game, int site)
        {
            var carrier = game.CarrierId ?? throw new InvalidOperationException("nobody carries the bomb");
            game.SetPlanting(carrier, site);
            game.Tick(_plantSeconds);
            game.SetPlanting(carrier, null);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Planted));
        }

        private static SearchAndDestroy Create(float preparationSeconds = 0f, int swapEveryRounds = 0, int winsToWin = 3)
        {
            var game = new SearchAndDestroy(new SearchAndDestroySpec(
                2, _roundSeconds, _plantSeconds, _defuseSeconds, _fuseSeconds, winsToWin, preparationSeconds, swapEveryRounds));
            game.AddPlayer(_alphaOne, Side.Alpha);
            game.AddPlayer(_alphaTwo, Side.Alpha);
            game.AddPlayer(_bravoOne, Side.Bravo);
            game.AddPlayer(_bravoTwo, Side.Bravo);
            return game;
        }
    }
}
