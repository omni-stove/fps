using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PickupLogTests
    {
        private const string Launcher = "00000000-0000-4000-8000-000000000003";

        [Test]
        public void DescribesTheContents()
        {
            Assert.That(
                PickupLog.Describe(PickupSpec.Weapon(Launcher, 2)),
                Is.EqualTo("kind=Weapon weapon=" + Launcher + " amount=2 who=anyone"));
            Assert.That(
                PickupLog.Describe(PickupSpec.Health(25, PickupEligibility.Only(Side.Bravo))),
                Is.EqualTo("kind=Health amount=25 who=only:1"));
            Assert.That(
                PickupLog.Describe(PickupSpec.Tag(2, Side.Bravo, PickupEligibility.Except(Side.Alpha))),
                Is.EqualTo("kind=Tag owner=2 owner-side=1 who=except:0"));
            Assert.That(PickupLog.Describe(PickupSpec.Tag(PickupSpec.NoOwner, null)), Is.EqualTo("kind=Tag owner=-1 owner-side=- who=anyone"));
        }

        [Test]
        public void SpawnedAndDroppedCarryThePosition()
        {
            Assert.That(
                PickupLog.Spawned("Pickup-Health", PickupSpec.Health(25), new Vector3(1f, 0f, -2.5f)),
                Is.EqualTo("[Pickup] spawned pickup=Pickup-Health kind=Health amount=25 who=anyone at=(1.00, 0.00, -2.50)"));
            Assert.That(
                PickupLog.Dropped("enemy-1", "Pickup-ReserveAmmo", PickupSpec.ReserveAmmo(30), Vector3.Zero),
                Is.EqualTo("[Pickup] dropped by=enemy-1 pickup=Pickup-ReserveAmmo kind=ReserveAmmo amount=30 who=anyone at=(0.00, 0.00, 0.00)"));
        }

        [Test]
        public void CollectedWritesOnlyWhatChanged()
        {
            var healed = PickupJudge.Judge(PickupSpec.Health(25), new PickupCollector(null, 90, 100));
            Assert.That(
                PickupLog.Collected("Pickup-Health", "Player", healed),
                Is.EqualTo("[Pickup] collected pickup=Pickup-Health by=Player kind=Health health=+10"));

            var armed = new PickupCollector(Side.Alpha, 100, 100).WithWeapons(Array.Empty<PickupWeaponState>(), null);
            Assert.That(
                PickupLog.Collected("Pickup-Weapon", "Player", PickupJudge.Judge(PickupSpec.Weapon(Launcher, 2), armed)),
                Is.EqualTo("[Pickup] collected pickup=Pickup-Weapon by=Player kind=Weapon weapon=" + Launcher + " added"));

            var withLauncher = armed.WithWeapons(new[] { new PickupWeaponState(Launcher, 1, 4, false) }, Launcher);
            Assert.That(
                PickupLog.Collected(null, null, PickupJudge.Judge(PickupSpec.ReserveAmmo(10), withLauncher)),
                Is.EqualTo("[Pickup] collected pickup=- by=- kind=ReserveAmmo weapon=" + Launcher + " reserve=+3"));

            Assert.That(
                PickupLog.Collected("Tag", "bot-2", PickupJudge.Judge(PickupSpec.Tag(4, Side.Alpha), armed)),
                Is.EqualTo("[Pickup] collected pickup=Tag by=bot-2 kind=Tag owner=4 owner-side=0 own-side=true"));
        }

        [Test]
        public void RejectedWritesTheReason()
        {
            var full = PickupJudge.Judge(PickupSpec.Health(25), new PickupCollector(null, 100, 100));

            Assert.That(
                PickupLog.Rejected("Pickup-Health", "Player", full),
                Is.EqualTo("[Pickup] rejected pickup=Pickup-Health by=Player kind=Health reason=HealthFull"));
            Assert.Throws<ArgumentException>(() => PickupLog.Collected("p", "c", full));
            Assert.Throws<ArgumentException>(
                () => PickupLog.Rejected("p", "c", PickupJudge.Judge(PickupSpec.Health(25), new PickupCollector(null, 10, 100))));
        }

        [Test]
        public void OtherLines()
        {
            Assert.That(PickupLog.Respawned("Pickup-Health"), Is.EqualTo("[Pickup] respawned pickup=Pickup-Health"));
            Assert.That(PickupLog.Expired("Pickup-Tag"), Is.EqualTo("[Pickup] expired pickup=Pickup-Tag"));
            Assert.That(
                PickupLog.Failed("Pickup-Weapon", "Player", PickupKind.Weapon, "no WeaponDefinition"),
                Is.EqualTo("[Pickup] failed pickup=Pickup-Weapon by=Player kind=Weapon reason=no WeaponDefinition"));
        }
    }
}
