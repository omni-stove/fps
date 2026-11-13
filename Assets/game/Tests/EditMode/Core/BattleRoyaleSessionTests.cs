using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BattleRoyaleSessionTests
    {
        // 分隊 s の n 人目 (0 始まり) は 10 * s + n。
        private const int _zeroA = 0;
        private const int _zeroB = 1;
        private const int _oneA = 10;
        private const int _oneB = 11;
        private const int _twoA = 20;
        private const int _twoB = 21;
        private const int _threeA = 30;
        private const int _threeB = 31;

        private static readonly Side _squadZero = new Side(0);
        private static readonly Side _squadOne = new Side(1);
        private static readonly Side _squadTwo = new Side(2);
        private static readonly Side _squadThree = new Side(3);

        private static readonly string[] _expectedWipeEvents = { "downed 11 by 0", "finished 11 credit 0" };
        private static readonly string[] _expectedBleedEvents = { "downed 11 by 0", "bledout 11 credit 0" };
        private static readonly string[] _expectedReviveEvents = { "downed 11 by 0", "revived 11 by 10" };
        private static readonly string[] _expectedEliminations = { "eliminated 1 place 4", "eliminated 2 place 3", "eliminated 3 place 2" };

        [Test]
        public void ALethalHitDownsWhileASquadmateStands()
        {
            var br = Create();
            br.Start();

            Assert.That(br.ReportLethal(_oneA, _zeroA), Is.EqualTo(BattleRoyaleHit.Downed));

            Assert.That(br.IsDowned(_oneA), Is.True);
            Assert.That(br.Mode.IsDown(_oneA), Is.False, "a downed player is still in the match");
            Assert.That(br.CountRemaining(_squadOne), Is.EqualTo(2));
            Assert.That(br.CountDowned(_squadOne), Is.EqualTo(1));
            Assert.That(br.CountStanding(_squadOne), Is.EqualTo(1));
            Assert.That(br.ReportLethal(_oneA, _zeroA), Is.EqualTo(BattleRoyaleHit.Ignored), "a downed body cannot be downed again");
        }

        [Test]
        public void TheLastStandingMemberFallsAndTakesTheDownedSquadWithThem()
        {
            var br = Create();
            var log = Record(br);
            br.Start();
            br.ReportLethal(_oneB, _zeroA);

            Assert.That(br.ReportLethal(_oneA, _zeroB), Is.EqualTo(BattleRoyaleHit.Killed));

            Assert.That(br.IsDowned(_oneB), Is.False);
            Assert.That(br.Mode.IsDown(_oneA), Is.True);
            Assert.That(br.Mode.IsDown(_oneB), Is.True);
            Assert.That(br.Mode.GetKills(_zeroB), Is.EqualTo(1));
            Assert.That(br.Mode.GetKills(_zeroA), Is.EqualTo(1), "the squadmate who downed the other one gets that kill");
            Assert.That(br.Mode.IsSideEliminated(_squadOne), Is.True);
            Assert.That(log, Is.EqualTo(_expectedWipeEvents));
        }

        [Test]
        public void ADownedSquadIsWipedWhenTheLastStandingMemberLeaves()
        {
            var br = Create();
            var log = Record(br);
            br.Start();
            br.ReportLethal(_oneB, _zeroA);

            br.RemovePlayer(_oneA);
            br.Tick(0f);

            Assert.That(br.Mode.IsDown(_oneB), Is.True);
            Assert.That(log, Is.EqualTo(_expectedWipeEvents));
            Assert.That(br.GetPlacement(_squadOne), Is.EqualTo(4));
        }

        [Test]
        public void BleedingOutCreditsTheDowner()
        {
            var br = Create();
            var log = Record(br);
            br.Start();
            br.ReportLethal(_oneB, _zeroA);

            br.Tick(ActionRules.DefaultDownedBleedoutSeconds);

            Assert.That(br.IsDowned(_oneB), Is.False);
            Assert.That(br.Mode.IsDown(_oneB), Is.True);
            Assert.That(br.Mode.GetKills(_zeroA), Is.EqualTo(1));
            Assert.That(br.CountRemaining(_squadOne), Is.EqualTo(1));
            Assert.That(br.GetPlacement(_squadOne), Is.Null, "one member is still standing");
            Assert.That(log, Is.EqualTo(_expectedBleedEvents));
        }

        [Test]
        public void OnlyASquadmateCanRevive()
        {
            var br = Create();
            var log = Record(br);
            br.Start();
            br.ReportLethal(_oneB, _zeroA);

            Assert.That(br.Revive(_zeroA, _oneB, 1f), Is.EqualTo(ReviveStep.Rejected), "an enemy cannot revive");
            Assert.That(br.Revive(_oneA, _oneB, 2f), Is.EqualTo(ReviveStep.Progressed));
            Assert.That(br.GetReviveProgress(_oneB), Is.EqualTo(2f / ActionRules.DefaultReviveSeconds).Within(1e-5f));
            br.Tick(0.1f);
            Assert.That(br.Revive(_oneA, _oneB, 1f), Is.EqualTo(ReviveStep.Completed));

            Assert.That(br.IsDowned(_oneB), Is.False);
            Assert.That(br.IsStanding(_oneB), Is.True);
            Assert.That(br.RevivedHealth(100), Is.EqualTo(30));
            Assert.That(log, Is.EqualTo(_expectedReviveEvents));
        }

        [Test]
        public void SquadsArePlacedInTheOrderTheyAreEliminatedAndTheLastOneWins()
        {
            var br = Create();
            var eliminations = new List<string>();
            br.SquadEliminated += (squad, place) => eliminations.Add("eliminated " + squad.Id + " place " + place);
            Side? winner = null;
            var ended = 0;
            br.Ended += side =>
            {
                ended++;
                winner = side;
            };
            br.Start();

            Wipe(br, _oneA, _oneB);
            br.Tick(0f);
            Assert.That(br.SquadsRemaining, Is.EqualTo(3));
            Wipe(br, _twoA, _twoB);
            br.Tick(0f);
            Wipe(br, _threeA, _threeB);
            br.Tick(0f);

            Assert.That(eliminations, Is.EqualTo(_expectedEliminations));
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(winner, Is.EqualTo((Side?)_squadZero));
            Assert.That(br.Mode.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(br.GetPlacement(_squadZero), Is.EqualTo(1));
            Assert.That(br.SquadsRemaining, Is.Zero);

            var placements = br.GetPlacements();
            Assert.That(placements.Count, Is.EqualTo(4));
            Assert.That(placements[0].Squad, Is.EqualTo(_squadZero));
            Assert.That(placements[3].Squad, Is.EqualTo(_squadOne));
            Assert.That(placements[3].Place, Is.EqualTo(4));
        }

        [Test]
        public void SquadsEliminatedOnTheSameTickShareAPlacement()
        {
            var br = Create();
            br.Start();

            Wipe(br, _oneA, _oneB);
            Wipe(br, _twoA, _twoB);
            br.Tick(0f);

            Assert.That(br.GetPlacement(_squadOne), Is.EqualTo(3));
            Assert.That(br.GetPlacement(_squadTwo), Is.EqualTo(3));
            Assert.That(br.SquadsRemaining, Is.EqualTo(2));
            Assert.That(br.Mode.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void EveryoneFallingAtOnceIsADrawSharingFirstPlace()
        {
            var br = new BattleRoyaleSession(2, 1, float.PositiveInfinity, Revive());
            br.Mode.AddPlayer(_zeroA, _squadZero);
            br.Mode.AddPlayer(_oneA, _squadOne);
            br.Start();

            br.ReportLethal(_zeroA, null);
            br.ReportLethal(_oneA, null);
            br.Tick(0f);

            Assert.That(br.Mode.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(br.Mode.Winner, Is.Null);
            Assert.That(br.GetPlacement(_squadZero), Is.EqualTo(1));
            Assert.That(br.GetPlacement(_squadOne), Is.EqualTo(1));
        }

        [Test]
        public void EmptySquadsAreNotRanked()
        {
            var br = new BattleRoyaleSession(4, 2, float.PositiveInfinity, Revive());
            br.Mode.AddPlayer(_zeroA, _squadZero);
            br.Mode.AddPlayer(_oneA, _squadOne);
            br.Start();

            Assert.That(br.EntrantCount, Is.EqualTo(2));
            br.ReportLethal(_oneA, _zeroA);
            br.Tick(0f);

            Assert.That(br.GetPlacement(_squadOne), Is.EqualTo(2));
            Assert.That(br.GetPlacement(_squadZero), Is.EqualTo(1));
            Assert.That(br.GetPlacement(_squadTwo), Is.Null);
            Assert.That(br.Mode.Winner, Is.EqualTo((Side?)_squadZero));
        }

        [Test]
        public void WithoutDownsALethalHitKills()
        {
            var br = new BattleRoyaleSession(4, 2, float.PositiveInfinity, ActionRules.Default);
            AddEveryone(br);
            br.Start();

            Assert.That(br.UsesDowned, Is.False);
            Assert.That(br.ReportLethal(_oneA, _zeroA), Is.EqualTo(BattleRoyaleHit.Killed));
            Assert.That(br.Mode.IsDown(_oneA), Is.True);
        }

        [Test]
        public void AtTimeUpTheSquadWithTheMostStandingWins()
        {
            // 出血 (15 秒) より短い制限時間にして、時間切れの時点でダウンが残るようにする。
            var br = new BattleRoyaleSession(4, 2, 10f, Revive());
            AddEveryone(br);
            br.Start();
            br.ReportLethal(_oneB, _zeroA);
            Wipe(br, _twoA, _twoB);
            br.ReportLethal(_threeA, _zeroA);
            br.Tick(1f);
            br.Tick(1f);
            br.ReportLethal(_threeB, _zeroA);

            br.Tick(10f);

            Assert.That(br.Mode.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(br.Mode.Winner, Is.EqualTo((Side?)_squadZero), "two standing beat one standing and one downed");
            Assert.That(br.GetPlacement(_squadZero), Is.EqualTo(1));
            Assert.That(br.GetPlacement(_squadOne), Is.EqualTo(2));
            Assert.That(br.GetPlacement(_squadThree), Is.EqualTo(3), "wiped on the time-up tick, before the time ran out");
            Assert.That(br.GetPlacement(_squadTwo), Is.EqualTo(4));
        }

        [Test]
        public void AnEvenTimeUpIsADrawThatSharesThePlacement()
        {
            var br = new BattleRoyaleSession(2, 2, 10f, Revive());
            br.Mode.AddPlayer(_zeroA, _squadZero);
            br.Mode.AddPlayer(_oneA, _squadOne);
            br.Start();

            br.Tick(10f);

            Assert.That(br.Mode.Winner, Is.Null);
            Assert.That(br.GetPlacement(_squadZero), Is.EqualTo(1));
            Assert.That(br.GetPlacement(_squadOne), Is.EqualTo(1));
        }

        [Test]
        public void ResetClearsDownsAndPlacements()
        {
            var br = Create();
            br.Start();
            br.ReportLethal(_oneB, _zeroA);
            Wipe(br, _twoA, _twoB);
            br.Tick(0f);

            br.Reset();

            Assert.That(br.GetDownedIds(), Is.Empty);
            Assert.That(br.GetPlacements(), Is.Empty);
            Assert.That(br.EntrantCount, Is.Zero);
            Assert.That(br.Mode.State, Is.EqualTo(GameModeState.Waiting));
        }

        [Test]
        public void NothingCountsBeforeTheStart()
        {
            var br = Create();

            Assert.That(br.ReportLethal(_oneA, _zeroA), Is.EqualTo(BattleRoyaleHit.Ignored));
            Assert.That(br.Revive(_oneB, _oneA, 1f), Is.EqualTo(ReviveStep.Rejected));
        }

        [Test]
        public void InvalidArgumentsThrow()
        {
            var br = Create();

            Assert.Throws<ArgumentNullException>(() => new BattleRoyaleSession(4, 2, 60f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleRoyaleSession(1, 2, 60f, Revive()));
            Assert.Throws<ArgumentOutOfRangeException>(() => br.ReportLethal(99, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => br.ReportLethal(_oneA, 99));
            Assert.Throws<ArgumentOutOfRangeException>(() => br.Revive(_oneA, _oneB, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => br.Tick(-1f));
        }

        private static BattleRoyaleSession Create()
        {
            var br = new BattleRoyaleSession(4, 2, float.PositiveInfinity, Revive());
            AddEveryone(br);
            return br;
        }

        private static ActionRules Revive() => ActionRules.Default.WithDowned(Downed.Revive);

        private static void AddEveryone(BattleRoyaleSession br)
        {
            br.Mode.AddPlayer(_zeroA, _squadZero);
            br.Mode.AddPlayer(_zeroB, _squadZero);
            br.Mode.AddPlayer(_oneA, _squadOne);
            br.Mode.AddPlayer(_oneB, _squadOne);
            br.Mode.AddPlayer(_twoA, _squadTwo);
            br.Mode.AddPlayer(_twoB, _squadTwo);
            br.Mode.AddPlayer(_threeA, _squadThree);
            br.Mode.AddPlayer(_threeB, _squadThree);
        }

        /// <summary>分隊の 2 人を分隊 0 の 1 人目が倒す (1 人目をダウンさせ、2 人目を倒して全滅させる)。</summary>
        private static void Wipe(BattleRoyaleSession br, int first, int second)
        {
            br.ReportLethal(first, _zeroA);
            br.ReportLethal(second, _zeroA);
        }

        private static List<string> Record(BattleRoyaleSession br)
        {
            var log = new List<string>();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            br.PlayerDowned += (player, by) => log.Add("downed " + player + " by " + (by.HasValue ? by.Value.ToString(inv) : "-"));
            br.PlayerRevived += (by, player) => log.Add("revived " + player + " by " + by);
            br.PlayerBledOut += (player, credit) => log.Add("bledout " + player + " credit " + (credit.HasValue ? credit.Value.ToString(inv) : "-"));
            br.PlayerFinished += (player, credit) => log.Add("finished " + player + " credit " + (credit.HasValue ? credit.Value.ToString(inv) : "-"));
            return log;
        }
    }
}
