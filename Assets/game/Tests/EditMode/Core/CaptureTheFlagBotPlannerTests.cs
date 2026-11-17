using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CaptureTheFlagBotPlannerTests
    {
        private static readonly Vector3 _ownBase = new Vector3(-30f, 0f, 0f);
        private static readonly Vector3 _ownFlagDropped = new Vector3(-5f, 0f, 8f);
        private static readonly Vector3 _enemyFlag = new Vector3(30f, 1f, 0f);

        [Test]
        public void ACarrierTakesTheFlagHomeEvenWhenItsOwnFlagIsDown()
        {
            var errand = CaptureTheFlagBotPlanner.Plan(true, false, _ownBase, FlagState.Dropped, _ownFlagDropped, _enemyFlag);

            Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.CarryFlag));
            Assert.That(errand.Destination, Is.EqualTo(_ownBase));
            Assert.That(errand.OverridesCombat, Is.True);
            Assert.That(errand.TargetIndex, Is.EqualTo(-1));
            Assert.That(errand.IsOrder, Is.False);
        }

        [Test]
        public void ABotGoesToRecoverItsDroppedFlagWhateverItsRole()
        {
            foreach (var attacker in new[] { true, false })
            {
                var errand = CaptureTheFlagBotPlanner.Plan(false, attacker, _ownBase, FlagState.Dropped, _ownFlagDropped, _enemyFlag);

                Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.RecoverFlag));
                Assert.That(errand.Destination, Is.EqualTo(_ownFlagDropped));
                Assert.That(errand.OverridesCombat, Is.True);
            }
        }

        [Test]
        public void AnAttackerHeadsForTheEnemyFlag()
        {
            foreach (var ownFlag in new[] { FlagState.AtBase, FlagState.Carried })
            {
                var errand = CaptureTheFlagBotPlanner.Plan(false, true, _ownBase, ownFlag, _ownBase, _enemyFlag);

                Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.TakeFlag));
                Assert.That(errand.Destination, Is.EqualTo(_enemyFlag));
                Assert.That(errand.OverridesCombat, Is.False);
            }
        }

        [Test]
        public void ADefenderHoldsItsOwnBase()
        {
            foreach (var ownFlag in new[] { FlagState.AtBase, FlagState.Carried })
            {
                var errand = CaptureTheFlagBotPlanner.Plan(false, false, _ownBase, ownFlag, _enemyFlag, _enemyFlag);

                Assert.That(errand.Kind, Is.EqualTo(BotErrandKind.DefendFlag));
                Assert.That(errand.Destination, Is.EqualTo(_ownBase));
                Assert.That(errand.OverridesCombat, Is.False);
            }
        }

        [Test]
        public void HalfOfTheTeamAttacksRoundingUp()
        {
            Assert.That(CaptureTheFlagBotPlanner.DefaultAttackShare, Is.EqualTo(0.5f));

            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(0, 1), Is.True, "a lone bot attacks");
            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(0, 4), Is.True);
            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(1, 4), Is.True);
            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(2, 4), Is.False);
            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(3, 4), Is.False);
            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(1, 3), Is.True);
            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(2, 3), Is.False);
        }

        [Test]
        public void TheAttackShareSetsHowManyAttack()
        {
            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(0, 3, 0f), Is.False, "nobody attacks");
            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(2, 3, 1f), Is.True, "everybody attacks");
            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(2, 10, 0.3f), Is.True);
            Assert.That(CaptureTheFlagBotPlanner.IsAttacker(3, 10, 0.3f), Is.False, "0.3 of 10 is exactly 3 attackers");
        }

        [Test]
        public void TheRoleRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CaptureTheFlagBotPlanner.IsAttacker(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CaptureTheFlagBotPlanner.IsAttacker(-1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => CaptureTheFlagBotPlanner.IsAttacker(2, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => CaptureTheFlagBotPlanner.IsAttacker(0, 2, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CaptureTheFlagBotPlanner.IsAttacker(0, 2, 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CaptureTheFlagBotPlanner.IsAttacker(0, 2, float.NaN));
        }
    }
}
