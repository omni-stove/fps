using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientLockOnWarningTests
    {
        private const int _me = 4;
        private const int _missile = 12;

        [Test]
        public void ALockedWarningPointsAtTheMissileFromWhereIFace()
        {
            var warning = new ClientLockOnWarning();

            Assert.That(warning.Handle(Locked(_missile, new Vector3(10f, 2f, 0f))), Is.True);

            var status = warning.StatusFrom(Vector3.Zero, 0f);
            Assert.That(status.HasValue, Is.True);
            Assert.That(status.Value.Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(status.Value.BearingDegrees, Is.EqualTo(90f).Within(1e-3f), "east of me while I face north");
            Assert.That(warning.StatusFrom(Vector3.Zero, 90f).Value.BearingDegrees, Is.EqualTo(0f).Within(1e-3f), "turning toward it puts it ahead");
            Assert.That(warning.StatusFrom(Vector3.Zero, 180f).Value.BearingDegrees, Is.EqualTo(-90f).Within(1e-3f));
        }

        [Test]
        public void TheMissileInTheSnapshotMovesTheWarning()
        {
            var warning = new ClientLockOnWarning();
            warning.Handle(Locked(_missile, new Vector3(10f, 2f, 0f)));

            warning.Apply(Snapshot(missileAt: new Vector3(0f, 2f, 10f)), _me);

            Assert.That(warning.Source, Is.EqualTo(new Vector3(0f, 2f, 10f)));
            Assert.That(warning.StatusFrom(Vector3.Zero, 0f).Value.BearingDegrees, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void AMissileMissingFromTheSnapshotKeepsTheLastKnownPlace()
        {
            var warning = new ClientLockOnWarning();
            warning.Handle(Locked(_missile, new Vector3(-10f, 2f, 0f)));

            warning.Apply(Snapshot(missileAt: null), _me);

            Assert.That(warning.IsWarning, Is.True);
            Assert.That(warning.StatusFrom(Vector3.Zero, 0f).Value.BearingDegrees, Is.EqualTo(-90f).Within(1e-3f));
        }

        [Test]
        public void TheServerClearingTheWarningClearsIt()
        {
            var warning = new ClientLockOnWarning();
            warning.Handle(Locked(_missile, Vector3.UnitX));

            Assert.That(warning.Handle(new LockOnWarningMessage { Stage = LockOnWarningStage.None }), Is.True);

            Assert.That(warning.IsWarning, Is.False);
            Assert.That(warning.ProjectileId, Is.EqualTo(LockOnWarningMessage.NoProjectile));
            Assert.That(warning.StatusFrom(Vector3.Zero, 0f), Is.Null);
        }

        [Test]
        public void TheEndOfTheMatchOrMyDeathClearsTheWarning()
        {
            var warning = new ClientLockOnWarning();
            warning.Handle(Locked(_missile, Vector3.UnitX));
            var ended = Snapshot(missileAt: null);
            ended.Match.State = GameModeState.Ended;

            warning.Apply(ended, _me);
            Assert.That(warning.IsWarning, Is.False, "the server forgets its warnings on a restart without clearing them");

            warning.Handle(Locked(_missile, Vector3.UnitX));
            var dead = Snapshot(missileAt: null);
            dead.Players[0].Health = 0;

            warning.Apply(dead, _me);
            Assert.That(warning.IsWarning, Is.False);
        }

        [Test]
        public void AMissileStraightAboveOrAnUnknownHeadingIsShownAhead()
        {
            var warning = new ClientLockOnWarning();
            warning.Handle(Locked(_missile, new Vector3(0f, 30f, 0f)));

            Assert.That(warning.StatusFrom(Vector3.Zero, 45f).Value.BearingDegrees, Is.Zero);

            warning.Handle(Locked(_missile, new Vector3(10f, 0f, 0f)));

            Assert.That(warning.StatusFrom(Vector3.Zero, float.NaN).Value.BearingDegrees, Is.Zero);
            Assert.That(warning.StatusFrom(Vector3.Zero, float.NaN).Value.Stage, Is.EqualTo(LockOnWarningStage.Locked));
        }

        [Test]
        public void OtherMessagesAreLeftAlone()
        {
            var warning = new ClientLockOnWarning();

            Assert.That(warning.Handle(new ShotMessage()), Is.False);
            Assert.That(warning.IsWarning, Is.False);
            Assert.That(() => warning.Apply(null, _me), Throws.ArgumentNullException);
        }

        [Test]
        public void TheWarningMessageReadsBackFromTheWire()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(Locked(_missile, new Vector3(1f, 2f, 3f)));

            var read = MatchMessages.CreateClientTable().Read(writer.AsSegment());
            var warning = new ClientLockOnWarning();

            Assert.That(warning.Handle(read), Is.True);
            Assert.That(warning.ProjectileId, Is.EqualTo(_missile));
            Assert.That(warning.Source, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        private static LockOnWarningMessage Locked(int projectile, Vector3 source)
        {
            return new LockOnWarningMessage { Stage = LockOnWarningStage.Locked, ProjectileId = projectile, Source = source };
        }

        private static SnapshotMessage Snapshot(Vector3? missileAt)
        {
            var snapshot = new SnapshotMessage { Tick = 5 };
            snapshot.Match.State = GameModeState.Playing;
            ref var me = ref snapshot.Players.Add();
            me.PlayerId = _me;
            me.Health = 100;
            if (missileAt.HasValue)
            {
                ref var missile = ref snapshot.Projectiles.Add();
                missile.ProjectileId = _missile;
                missile.Position = missileAt.Value;
            }

            return snapshot;
        }
    }
}
