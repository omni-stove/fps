using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Editor;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // Play Mode に入る通しそのものは -executeMethod Shooter.Editor.BattleRoyaleSmokeRun.Run (または SmokeSuite.RunAll) で確かめる。
    public sealed class BattleRoyaleSmokeRunTests
    {
        [Test]
        public void ClassifiesTheSupplyDropLines()
        {
            Assert.That(
                BattleRoyaleSmokeRun.Classify(SupplyDropLog.Dropped(2, new Vector3(1f, 40f, 3f), 0f)),
                Is.EqualTo(BattleRoyaleSmokeRun.LineKind.CrateDropped));
            Assert.That(
                BattleRoyaleSmokeRun.Classify(SupplyDropLog.Landed(2, new Vector3(1f, 0f, 3f))),
                Is.EqualTo(BattleRoyaleSmokeRun.LineKind.CrateLanded));
            Assert.That(BattleRoyaleSmokeRun.Classify(SupplyDropLog.Stage(0, 1)), Is.EqualTo(BattleRoyaleSmokeRun.LineKind.Other));
            Assert.That(
                BattleRoyaleSmokeRun.Classify(SupplyDropLog.NoGround(2, new Vector3(1f, 40f, 3f))),
                Is.EqualTo(BattleRoyaleSmokeRun.LineKind.Other));
        }

        [TestCase(RedeployVerdict.Waiting, BattleRoyaleSmokeRun.LineKind.RedeployWaiting)]
        [TestCase(RedeployVerdict.NoCharges, BattleRoyaleSmokeRun.LineKind.RedeployRefused)]
        [TestCase(RedeployVerdict.PastPhase, BattleRoyaleSmokeRun.LineKind.RedeployRefused)]
        [TestCase(RedeployVerdict.SquadWiped, BattleRoyaleSmokeRun.LineKind.RedeployRefused)]
        public void ClassifiesEveryRedeployDecision(RedeployVerdict verdict, BattleRoyaleSmokeRun.LineKind expected)
        {
            var seconds = verdict == RedeployVerdict.Waiting ? 5f : (float?)null;

            Assert.That(BattleRoyaleSmokeRun.Classify(BattleRoyaleLog.Redeploy(4, verdict, 0, 1, seconds)), Is.EqualTo(expected));
        }

        [Test]
        public void KeepsClassifyingTheMatchLines()
        {
            Assert.That(
                BattleRoyaleSmokeRun.Classify(BattleRoyaleLog.Errand(3, BotErrandKind.Collect, new Vector3(1f, 0f, 2f))),
                Is.EqualTo(BattleRoyaleSmokeRun.LineKind.CollectErrand));
            Assert.That(
                BattleRoyaleSmokeRun.Classify(BattleRoyaleLog.Errand(3, BotErrandKind.None, Vector3.Zero)),
                Is.EqualTo(BattleRoyaleSmokeRun.LineKind.Other));
            Assert.That(BattleRoyaleSmokeRun.Classify(ModeLog.Kill(1, 2)), Is.EqualTo(BattleRoyaleSmokeRun.LineKind.Kill));
            Assert.That(BattleRoyaleSmokeRun.Classify(BattleRoyaleLog.Eliminated(new Side(2), 5, 3)), Is.EqualTo(BattleRoyaleSmokeRun.LineKind.Eliminated));
            Assert.That(BattleRoyaleSmokeRun.Classify(BattleRoyaleLog.Downed(2, 1)), Is.EqualTo(BattleRoyaleSmokeRun.LineKind.Other));
            Assert.That(BattleRoyaleSmokeRun.Classify(BattleRoyaleLog.CrateOpened(2, 1, 3)), Is.EqualTo(BattleRoyaleSmokeRun.LineKind.Other));
            Assert.That(BattleRoyaleSmokeRun.Classify(null), Is.EqualTo(BattleRoyaleSmokeRun.LineKind.Other));
        }

        [Test]
        public void TheScaledScheduleFitsTheSuiteTimeout()
        {
            var phases = BattleRoyaleSmokeRun.ScalePhases(BattleRoyaleSceneBuilder.ZonePhases, BattleRoyaleSmokeRun.DefaultZoneScale);
            var played = BattleRoyaleSmokeRun.WarmUpSeconds + BattleRoyaleSmokeRun.TotalSeconds(phases) + BattleRoyaleSmokeRun.GraceSeconds;

            Assert.That(
                played + BattleRoyaleSmokeRun.EnterTimeoutSeconds,
                Is.LessThan(SmokeSuite.RunTimeoutSeconds),
                "the suite would stop the run before it reports");
            Assert.Throws<ArgumentNullException>(() => BattleRoyaleSmokeRun.ScalePhases(null, 1f));
        }
    }
}
