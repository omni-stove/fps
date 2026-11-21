using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    // 乗り物を狙うロックオン・フレアの要求・ロックオンの警告
    public sealed class LockOnMessageTests
    {
        [Test]
        public void AFireRequestCanLockOntoAVehicle()
        {
            var got = ToServer<FireRequestMessage>(new FireRequestMessage
            {
                WeaponId = "launcher",
                Direction = Vector3.UnitZ,
                HasLockTarget = true,
                LockTargetId = 2,
                LockTargetKind = LockTargetKind.Vehicle,
            });

            Assert.That(FireRequestMessage.CurrentVersion, Is.EqualTo(3));
            Assert.That(got.HasLockTarget, Is.True);
            Assert.That(got.LockTargetId, Is.EqualTo(2));
            Assert.That(got.LockTargetKind, Is.EqualTo(LockTargetKind.Vehicle));
        }

        [Test]
        public void AFireRequestWithoutALockReadsAsAPlayerKind()
        {
            var got = ToServer<FireRequestMessage>(new FireRequestMessage
            {
                WeaponId = "launcher",
                Direction = Vector3.UnitZ,
                LockTargetKind = LockTargetKind.Vehicle,
            });

            Assert.That(got.HasLockTarget, Is.False);
            Assert.That(got.LockTargetKind, Is.EqualTo(LockTargetKind.Player));
        }

        // 前の版 (2) は種類の 1 バイトを持たないので、最後の 1 バイトを落として版を 2 に書き換えた要求を読ませる
        [Test]
        public void AVersionTwoFireRequestIsReadWithAPlayerLock()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new FireRequestMessage
            {
                WeaponId = "launcher",
                Direction = Vector3.UnitZ,
                HasLockTarget = true,
                LockTargetId = 7,
                LockTargetKind = LockTargetKind.Vehicle,
                Trigger = TriggerEdge.Pressed,
            });
            var segment = writer.AsSegment();
            var bytes = new byte[segment.Count - 1];
            Array.Copy(segment.Array, segment.Offset, bytes, 0, bytes.Length);
            bytes[2] = 2;

            var got = (FireRequestMessage)MatchMessages.CreateServerTable().Read(new ArraySegment<byte>(bytes));

            Assert.That(got.HasLockTarget, Is.True);
            Assert.That(got.LockTargetId, Is.EqualTo(7));
            Assert.That(got.LockTargetKind, Is.EqualTo(LockTargetKind.Player));
            Assert.That(got.Trigger, Is.EqualTo(TriggerEdge.Pressed));
        }

        [Test]
        public void AnUnknownLockTargetKindIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new FireRequestMessage { WeaponId = "launcher", Direction = Vector3.UnitZ, HasLockTarget = true, LockTargetId = 1 });
            var segment = writer.AsSegment();
            var bytes = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, bytes, 0, bytes.Length);
            bytes[bytes.Length - 1] = 9;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateServerTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void TheCountermeasureRequestReadsBack()
        {
            var got = ToServer<VehicleRequestMessage>(new VehicleRequestMessage { Action = VehicleAction.Countermeasure });

            Assert.That((int)VehicleAction.Countermeasure, Is.EqualTo(3));
            Assert.That(got.Action, Is.EqualTo(VehicleAction.Countermeasure));
        }

        [Test]
        public void ALockedWarningReadsBack()
        {
            var got = ToClient<LockOnWarningMessage>(new LockOnWarningMessage
            {
                Stage = LockOnWarningStage.Locked,
                ProjectileId = 4,
                Source = new Vector3(1f, 2f, 3f),
            });

            Assert.That(got.Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(got.ProjectileId, Is.EqualTo(4));
            Assert.That(got.Source, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void AClearedWarningCarriesNoProjectileOrSource()
        {
            var got = ToClient<LockOnWarningMessage>(new LockOnWarningMessage
            {
                Stage = LockOnWarningStage.None,
                ProjectileId = 4,
                Source = new Vector3(1f, 2f, 3f),
            });

            Assert.That(got.Stage, Is.EqualTo(LockOnWarningStage.None));
            Assert.That(got.ProjectileId, Is.EqualTo(LockOnWarningMessage.NoProjectile));
            Assert.That(got.Source, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void TheWarningHasTheLastIdAndIsOnlyReadByTheClient()
        {
            Assert.That(MessageIds.LockOnWarning, Is.EqualTo(90));
            Assert.That(new LockOnWarningMessage().Id, Is.EqualTo(MessageIds.LockOnWarning));
            Assert.That(MatchMessages.CreateClientTable().TryGet(MessageIds.LockOnWarning, out var warning), Is.True);
            Assert.That(warning, Is.InstanceOf<LockOnWarningMessage>());
            Assert.That(MatchMessages.CreateServerTable().TryGet(MessageIds.LockOnWarning, out _), Is.False);
        }

        [Test]
        public void AWarningWithAnUnknownStageIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new LockOnWarningMessage { Stage = LockOnWarningStage.Locked, ProjectileId = 1 });
            var segment = writer.AsSegment();
            var bytes = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, bytes, 0, bytes.Length);
            bytes[3] = 7;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        private static T ToServer<T>(IMessage message)
            where T : class, IMessage
        {
            return RoundTrip<T>(message, MatchMessages.CreateServerTable());
        }

        private static T ToClient<T>(IMessage message)
            where T : class, IMessage
        {
            return RoundTrip<T>(message, MatchMessages.CreateClientTable());
        }

        private static T RoundTrip<T>(IMessage message, MessageTable table)
            where T : class, IMessage
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            var read = table.Read(writer.AsSegment());
            Assert.That(read, Is.InstanceOf<T>());
            return (T)read;
        }
    }
}
