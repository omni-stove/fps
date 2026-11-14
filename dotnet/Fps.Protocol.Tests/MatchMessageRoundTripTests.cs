using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    /// <summary>対戦のメッセージを 1 つずつ書いて、受け取る側の表で読み戻すと同じ値になるか。</summary>
    public sealed class MatchMessageRoundTripTests
    {
        private readonly MessageTable _server = MatchMessages.CreateServerTable();
        private readonly MessageTable _client = MatchMessages.CreateClientTable();

        [Test]
        public void ConnectRequest()
        {
            var sent = new ConnectRequestMessage { ClientVersion = "1.2.3", RejoinKey = "0123abcd", DisplayName = "ぷれいやー" };

            var got = ToServer<ConnectRequestMessage>(sent);

            Assert.That(got.ProtocolVersion, Is.EqualTo(MatchProtocol.Version));
            Assert.That(got.ClientVersion, Is.EqualTo("1.2.3"));
            Assert.That(got.RejoinKey, Is.EqualTo("0123abcd"));
            Assert.That(got.DisplayName, Is.EqualTo("ぷれいやー"));
        }

        [Test]
        public void ConnectAccepted()
        {
            var got = ToClient<ConnectAcceptedMessage>(
                new ConnectAcceptedMessage { PlayerId = 7, ServerVersion = "1.2.3", TickRate = 60, ServerTick = 1234 });

            Assert.That(got.PlayerId, Is.EqualTo(7));
            Assert.That(got.ServerVersion, Is.EqualTo("1.2.3"));
            Assert.That(got.TickRate, Is.EqualTo(60));
            Assert.That(got.ServerTick, Is.EqualTo(1234));
        }

        [Test]
        public void ConnectRejectedAndServerDisconnectingCarryTheNoticeReason()
        {
            var rejected = ToClient<ConnectRejectedMessage>(
                new ConnectRejectedMessage { Reason = DisconnectNotice.VersionMismatchReason("1.2.3") });
            var stopping = ToClient<ServerDisconnectingMessage>(
                new ServerDisconnectingMessage { Reason = DisconnectNotice.ServerStoppedReason });

            Assert.That(rejected.Reason, Is.EqualTo(DisconnectNotice.VersionMismatchReason("1.2.3")));
            Assert.That(stopping.Reason, Is.EqualTo(DisconnectNotice.ServerStoppedReason));
        }

        [Test]
        public void NetStats()
        {
            Assert.That(ToServer<NetStatsProbeMessage>(new NetStatsProbeMessage { Sequence = 65000 }).Sequence, Is.EqualTo(65000));
            var echo = ToClient<NetStatsEchoMessage>(new NetStatsEchoMessage { Sequence = 3, ServerTick = 99 });
            Assert.That(echo.Sequence, Is.EqualTo(3));
            Assert.That(echo.ServerTick, Is.EqualTo(99));
        }

        [Test]
        public void Input()
        {
            var sent = new InputMessage { AckedSnapshotTick = 40 };
            sent.Add(new InputFrame(50, new Vector2(0.5f, 1f), 1.25f, InputButtons.Sprint | InputButtons.Jump));
            sent.Add(new InputFrame(51, new Vector2(-1f, 0f), -3f, InputButtons.All));

            var got = ToServer<InputMessage>(sent);

            Assert.That(got.AckedSnapshotTick, Is.EqualTo(40));
            Assert.That(got.Count, Is.EqualTo(2));
            Assert.That(got[0].Tick, Is.EqualTo(50));
            Assert.That(got[0].Move, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(got[0].Yaw, Is.EqualTo(1.25f));
            Assert.That(got[0].Buttons, Is.EqualTo(InputButtons.Sprint | InputButtons.Jump));
            Assert.That(got[1].Tick, Is.EqualTo(51));
            Assert.That(got[1].Has(InputButtons.Dodge), Is.True);
        }

        [Test]
        public void FireRequest()
        {
            var got = ToServer<FireRequestMessage>(new FireRequestMessage
            {
                WeaponId = "rifle",
                Origin = new Vector3(1f, 2f, 3f),
                Direction = new Vector3(0f, 0f, 1f),
                ClientTick = 77,
                InterpolationAlpha = 0.25f,
                ShotIndex = 4,
                ShotGeneration = 2,
            });

            Assert.That(got.WeaponId, Is.EqualTo("rifle"));
            Assert.That(got.Origin, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(got.Direction, Is.EqualTo(new Vector3(0f, 0f, 1f)));
            Assert.That(got.ClientTick, Is.EqualTo(77));
            Assert.That(got.InterpolationAlpha, Is.EqualTo(0.25f));
            Assert.That(got.ShotIndex, Is.EqualTo(4));
            Assert.That(got.ShotGeneration, Is.EqualTo(2));
        }

        [Test]
        public void WeaponRequests()
        {
            Assert.That(
                ToServer<WeaponActionRequestMessage>(new WeaponActionRequestMessage { Action = WeaponAction.ActiveReload }).Action,
                Is.EqualTo(WeaponAction.ActiveReload));
            Assert.That(
                ToServer<SwitchWeaponRequestMessage>(new SwitchWeaponRequestMessage { WeaponId = "pistol" }).WeaponId,
                Is.EqualTo("pistol"));

            var loadout = new LoadoutRequestMessage();
            loadout.WeaponIds.Add("rifle");
            loadout.WeaponIds.Add("pistol");
            var got = ToServer<LoadoutRequestMessage>(loadout);
            Assert.That(got.WeaponIds.Count, Is.EqualTo(2));
            Assert.That(got.WeaponIds[0], Is.EqualTo("rifle"));
            Assert.That(got.WeaponIds[1], Is.EqualTo("pistol"));
        }

        [Test]
        public void GrenadePingAndReviveRequests()
        {
            Assert.That(
                ToServer<ThrowGrenadeRequestMessage>(new ThrowGrenadeRequestMessage { Direction = new Vector3(0f, 0.5f, 1f) }).Direction,
                Is.EqualTo(new Vector3(0f, 0.5f, 1f)));
            Assert.That(
                ToServer<PingRequestMessage>(new PingRequestMessage { Direction = new Vector3(1f, 0f, 0f) }).Direction,
                Is.EqualTo(new Vector3(1f, 0f, 0f)));
            Assert.That(ToServer<ReviveRequestMessage>(new ReviveRequestMessage { TargetPlayerId = 12 }).TargetPlayerId, Is.EqualTo(12));
        }

        [Test]
        public void StructureState()
        {
            var sent = new StructureStateMessage { StructureId = 3 };
            sent.SetWords(new ulong[] { 1UL, ulong.MaxValue, 0x8000000000000000UL });

            var got = ToClient<StructureStateMessage>(sent);
            var words = new ulong[got.WordCount];
            got.CopyWordsTo(words);

            Assert.That(got.StructureId, Is.EqualTo(3));
            Assert.That(words, Is.EqualTo(new ulong[] { 1UL, ulong.MaxValue, 0x8000000000000000UL }));
        }

        [Test]
        public void ShotWithACharacterImpact()
        {
            var got = ToClient<ShotMessage>(new ShotMessage
            {
                HasShooter = true,
                ShooterId = 5,
                WeaponId = "rifle",
                Origin = new Vector3(1f, 1f, 1f),
                Direction = new Vector3(0f, 0f, 1f),
                HasImpact = true,
                ImpactPoint = new Vector3(1f, 1f, 9f),
                ImpactNormal = new Vector3(0f, 0f, -1f),
                ImpactMaterialId = "flesh",
                ImpactIsCharacter = true,
                HasImpactZone = true,
                ImpactZone = HitZone.Head,
            });

            Assert.That(got.HasShooter, Is.True);
            Assert.That(got.ShooterId, Is.EqualTo(5));
            Assert.That(got.WeaponId, Is.EqualTo("rifle"));
            Assert.That(got.Origin, Is.EqualTo(new Vector3(1f, 1f, 1f)));
            Assert.That(got.Direction, Is.EqualTo(new Vector3(0f, 0f, 1f)));
            Assert.That(got.HasImpact, Is.True);
            Assert.That(got.ImpactPoint, Is.EqualTo(new Vector3(1f, 1f, 9f)));
            Assert.That(got.ImpactNormal, Is.EqualTo(new Vector3(0f, 0f, -1f)));
            Assert.That(got.ImpactMaterialId, Is.EqualTo("flesh"));
            Assert.That(got.ImpactIsCharacter, Is.True);
            Assert.That(got.HasImpactZone, Is.True);
            Assert.That(got.ImpactZone, Is.EqualTo(HitZone.Head));
        }

        [Test]
        public void ShotWithoutShooterOrImpact()
        {
            var got = ToClient<ShotMessage>(new ShotMessage { WeaponId = "rifle", Direction = new Vector3(0f, 0f, 1f) });

            Assert.That(got.HasShooter, Is.False);
            Assert.That(got.HasImpact, Is.False);
            Assert.That(got.HasImpactZone, Is.False);
        }

        [Test]
        public void ShotRejectsAZoneOnANonCharacterImpact()
        {
            var shot = new ShotMessage { HasImpact = true, ImpactIsCharacter = false, HasImpactZone = true };

            Assert.Throws<System.InvalidOperationException>(() => new MessageWriter().WriteMessage(shot));
        }

        [Test]
        public void HitConfirmAndDamageTaken()
        {
            var hit = ToClient<HitConfirmMessage>(new HitConfirmMessage { Damage = 25, Zone = HitZone.Limb, Killed = true, Downed = true });
            Assert.That(hit.Damage, Is.EqualTo(25));
            Assert.That(hit.Zone, Is.EqualTo(HitZone.Limb));
            Assert.That(hit.Killed, Is.True);
            Assert.That(hit.Downed, Is.True);

            var taken = ToClient<DamageTakenMessage>(
                new DamageTakenMessage { HasSource = true, SourcePosition = new Vector3(4f, 5f, 6f), Amount = 30 });
            Assert.That(taken.HasSource, Is.True);
            Assert.That(taken.SourcePosition, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(taken.Amount, Is.EqualTo(30));

            var unknown = ToClient<DamageTakenMessage>(new DamageTakenMessage { Amount = 10 });
            Assert.That(unknown.HasSource, Is.False);
            Assert.That(unknown.SourcePosition, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void KilledAndExplosion()
        {
            var killed = ToClient<KilledMessage>(
                new KilledMessage { KillerId = 1, VictimId = 2, WeaponId = "rifle", Headshot = true, Friendly = true });
            Assert.That(killed.KillerId, Is.EqualTo(1));
            Assert.That(killed.VictimId, Is.EqualTo(2));
            Assert.That(killed.WeaponId, Is.EqualTo("rifle"));
            Assert.That(killed.Headshot, Is.True);
            Assert.That(killed.Friendly, Is.True);

            var explosion = ToClient<ExplosionMessage>(
                new ExplosionMessage { Center = new Vector3(1f, 0f, 2f), HasThrower = true, ThrowerId = 4, WeaponId = "frag" });
            Assert.That(explosion.Center, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(explosion.HasThrower, Is.True);
            Assert.That(explosion.ThrowerId, Is.EqualTo(4));
            Assert.That(explosion.WeaponId, Is.EqualTo("frag"));
        }

        [Test]
        public void Pings()
        {
            var placed = ToClient<PingPlacedMessage>(new PingPlacedMessage
            {
                PingId = 9,
                Kind = PingKind.Enemy,
                Position = new Vector3(1f, 2f, 3f),
                Tracking = true,
                RemainingSeconds = 4.5f,
            });
            Assert.That(placed.PingId, Is.EqualTo(9));
            Assert.That(placed.Kind, Is.EqualTo(PingKind.Enemy));
            Assert.That(placed.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(placed.Tracking, Is.True);
            Assert.That(placed.RemainingSeconds, Is.EqualTo(4.5f));

            var moved = ToClient<PingMovedMessage>(new PingMovedMessage { PingId = 9, Position = new Vector3(2f, 2f, 2f) });
            Assert.That(moved.PingId, Is.EqualTo(9));
            Assert.That(moved.Position, Is.EqualTo(new Vector3(2f, 2f, 2f)));

            var lost = ToClient<PingLostMessage>(new PingLostMessage { PingId = 9, Position = new Vector3(3f, 2f, 2f) });
            Assert.That(lost.PingId, Is.EqualTo(9));
            Assert.That(lost.Position, Is.EqualTo(new Vector3(3f, 2f, 2f)));

            var removed = ToClient<PingRemovedMessage>(new PingRemovedMessage { PingId = 9, Reason = PingRemoval.TargetGone });
            Assert.That(removed.PingId, Is.EqualTo(9));
            Assert.That(removed.Reason, Is.EqualTo(PingRemoval.TargetGone));
        }

        [Test]
        public void OwnerNotices()
        {
            var teleported = ToClient<TeleportedMessage>(
                new TeleportedMessage { PlayerId = 3, Tick = 500, Position = new Vector3(5f, 0f, 5f), Yaw = 1.5f });
            Assert.That(teleported.PlayerId, Is.EqualTo(3));
            Assert.That(teleported.Tick, Is.EqualTo(500));
            Assert.That(teleported.Position, Is.EqualTo(new Vector3(5f, 0f, 5f)));
            Assert.That(teleported.Yaw, Is.EqualTo(1.5f));

            var corrected = ToClient<ShotIndexCorrectedMessage>(
                new ShotIndexCorrectedMessage { Generation = 2, ClientIndex = 5, ServerIndex = 4 });
            Assert.That(corrected.Generation, Is.EqualTo(2));
            Assert.That(corrected.ClientIndex, Is.EqualTo(5));
            Assert.That(corrected.ServerIndex, Is.EqualTo(4));

            var idle = ToClient<IdleWarningMessage>(new IdleWarningMessage { Active = true, RemainingSeconds = 10f });
            Assert.That(idle.Active, Is.True);
            Assert.That(idle.RemainingSeconds, Is.EqualTo(10f));

            var answer = ToClient<LoadoutAnswerMessage>(new LoadoutAnswerMessage { Accepted = false, Reason = "unknown weapon" });
            Assert.That(answer.Accepted, Is.False);
            Assert.That(answer.Reason, Is.EqualTo("unknown weapon"));
        }

        private T ToServer<T>(IMessage message)
            where T : class, IMessage
        {
            return RoundTrip<T>(message, _server);
        }

        private T ToClient<T>(IMessage message)
            where T : class, IMessage
        {
            return RoundTrip<T>(message, _client);
        }

        private static T RoundTrip<T>(IMessage message, MessageTable table)
            where T : class, IMessage
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            var read = table.Read(writer.AsSegment());
            Assert.That(read, Is.InstanceOf<T>());
            Assert.That(read, Is.Not.SameAs(message));
            return (T)read;
        }
    }
}
