using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class StreakDeployReportMessageTests
    {
        private readonly MessageTable _server = MatchMessages.CreateServerTable();
        private readonly MessageTable _client = MatchMessages.CreateClientTable();

        [Test]
        public void TheProtocolVersionIs30()
        {
            Assert.That(MatchProtocol.Version, Is.EqualTo(30));
        }

        [Test]
        public void TheNewMessagesHaveTheirIds()
        {
            Assert.That(new StreakUseRequestMessage().Id, Is.EqualTo(30));
            Assert.That(new DeployRequestMessage().Id, Is.EqualTo(31));
            Assert.That(new ReportRequestMessage().Id, Is.EqualTo(32));
            Assert.That(new StreakActivatedMessage().Id, Is.EqualTo(85));
            Assert.That(new KillCamClipMessage().Id, Is.EqualTo(86));
        }

        [Test]
        public void ALoadoutCarriesTheCosmetic()
        {
            var sent = new LoadoutRequestMessage { CosmeticId = "camo-tiger" };
            sent.WeaponIds.Add("rifle");

            var got = ToServer<LoadoutRequestMessage>(sent);

            Assert.That(LoadoutRequestMessage.CurrentVersion, Is.EqualTo(2));
            Assert.That(got.WeaponIds.Count, Is.EqualTo(1));
            Assert.That(got.WeaponIds[0], Is.EqualTo("rifle"));
            Assert.That(got.CosmeticId, Is.EqualTo("camo-tiger"));
        }

        [Test]
        public void ALoadoutWithoutACosmeticSendsAnEmptyId()
        {
            var sent = new LoadoutRequestMessage();
            sent.WeaponIds.Add("rifle");
            var writer = new MessageWriter();
            writer.WriteMessage(sent);

            var got = (LoadoutRequestMessage)_server.Read(writer.AsSegment());

            // Id・版 (2 + 1)、武器の数 (1)、"rifle" (2 + 5)、見た目の Id (2)
            Assert.That(writer.Length, Is.EqualTo(3 + 1 + 7 + 2));
            Assert.That(got.CosmeticId, Is.Empty);
        }

        [Test]
        public void ACosmeticIdLongerThanAContentIdIsNotWritten()
        {
            var sent = new LoadoutRequestMessage { CosmeticId = new string('a', MatchProtocol.MaxContentIdBytes + 1) };

            Assert.Throws<ArgumentException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void StreakUseRequestCarriesTheChosenTargetAndHeading()
        {
            var got = ToServer<StreakUseRequestMessage>(
                new StreakUseRequestMessage { RewardId = "airstrike", HasTarget = true, Target = new Vector3(12f, 0.5f, -30f), HeadingDegrees = 135f });

            Assert.That(StreakUseRequestMessage.CurrentVersion, Is.EqualTo(2));
            Assert.That(got.RewardId, Is.EqualTo("airstrike"));
            Assert.That(got.HasTarget, Is.True);
            Assert.That(got.Target, Is.EqualTo(new Vector3(12f, 0.5f, -30f)));
            Assert.That(got.HeadingDegrees, Is.EqualTo(135f));
        }

        [Test]
        public void AStreakUseRequestWithoutATargetCarriesOnlyTheReward()
        {
            var sent = new StreakUseRequestMessage { RewardId = "uav", Target = new Vector3(1f, 2f, 3f), HeadingDegrees = 90f };

            var got = ToServer<StreakUseRequestMessage>(sent);

            Assert.That(got.RewardId, Is.EqualTo("uav"));
            Assert.That(got.HasTarget, Is.False);
            Assert.That(got.Target, Is.EqualTo(Vector3.Zero), "a target is not written without HasTarget");
            Assert.That(got.HeadingDegrees, Is.Zero);
        }

        [Test]
        public void AStreakUseRequestWithoutARewardIsNotWritten()
        {
            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(new StreakUseRequestMessage()));
        }

        [Test]
        public void AStreakUseRequestWithoutARewardIsUnreadable()
        {
            var writer = Head(MessageIds.StreakUseRequest, StreakUseRequestMessage.CurrentVersion);
            writer.WriteString("");
            writer.WriteBool(false);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void AStreakUseRequestWithANonFiniteTargetIsUnreadable()
        {
            var writer = Head(MessageIds.StreakUseRequest, StreakUseRequestMessage.CurrentVersion);
            writer.WriteString("airstrike");
            writer.WriteBool(true);
            writer.WriteVector3(new Vector3(float.NaN, 0f, 1f));
            writer.WriteSingle(0f);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void AStreakUseRequestWithANonFiniteHeadingIsUnreadable()
        {
            var writer = Head(MessageIds.StreakUseRequest, StreakUseRequestMessage.CurrentVersion);
            writer.WriteString("airstrike");
            writer.WriteBool(true);
            writer.WriteVector3(new Vector3(1f, 0f, 1f));
            writer.WriteSingle(float.PositiveInfinity);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void DeployRequest()
        {
            var got = ToServer<DeployRequestMessage>(
                new DeployRequestMessage { TypeId = "sentry", Position = new Vector3(4f, 0f, -2f), Yaw = 135f });

            Assert.That(got.TypeId, Is.EqualTo("sentry"));
            Assert.That(got.Position, Is.EqualTo(new Vector3(4f, 0f, -2f)));
            Assert.That(got.Yaw, Is.EqualTo(135f));
        }

        [Test]
        public void ADeployRequestWithoutATypeIsNotWritten()
        {
            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(new DeployRequestMessage()));
        }

        [Test]
        public void ADeployRequestWithoutATypeIsUnreadable()
        {
            var writer = Head(MessageIds.DeployRequest, DeployRequestMessage.CurrentVersion);
            writer.WriteString("");
            writer.WriteVector3(Vector3.Zero);
            writer.WriteSingle(0f);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void ADeployRequestWithANonFiniteYawIsUnreadable()
        {
            var writer = Head(MessageIds.DeployRequest, DeployRequestMessage.CurrentVersion);
            writer.WriteString("sentry");
            writer.WriteVector3(Vector3.Zero);
            writer.WriteSingle(float.PositiveInfinity);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void ReportRequest()
        {
            var got = ToServer<ReportRequestMessage>(
                new ReportRequestMessage { TargetPlayerId = 4, Reason = ReportReason.AbusiveChat, Comment = "暴言を繰り返す" });

            Assert.That(got.TargetPlayerId, Is.EqualTo(4));
            Assert.That(got.Reason, Is.EqualTo(ReportReason.AbusiveChat));
            Assert.That(got.Comment, Is.EqualTo("暴言を繰り返す"));
        }

        [Test]
        public void AReportWithoutACommentReadsAsEmpty()
        {
            var got = ToServer<ReportRequestMessage>(new ReportRequestMessage { TargetPlayerId = 0, Reason = ReportReason.Cheating, Comment = null });

            Assert.That(got.Comment, Is.Empty);
            Assert.That(got.Reason, Is.EqualTo(ReportReason.Cheating));
        }

        [Test]
        public void AReportOnANegativePlayerIsNotWritten()
        {
            Assert.Throws<InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new ReportRequestMessage { TargetPlayerId = -1 }));
        }

        [Test]
        public void AReportCommentOverTheLimitIsNotWritten()
        {
            var sent = new ReportRequestMessage { TargetPlayerId = 1, Comment = new string('a', MatchProtocol.MaxReportCommentBytes + 1) };

            Assert.Throws<ArgumentException>(() => new MessageWriter().WriteMessage(sent));
        }

        [TestCase(-1, 0)]
        [TestCase(1, 5)]
        public void AnUnreadableReportIsRejected(int target, int reason)
        {
            var writer = Head(MessageIds.ReportRequest, ReportRequestMessage.CurrentVersion);
            writer.WriteInt32(target);
            writer.WriteByte((byte)reason);
            writer.WriteString("");

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void AReportCommentOverTheLimitIsUnreadable()
        {
            var writer = Head(MessageIds.ReportRequest, ReportRequestMessage.CurrentVersion);
            writer.WriteInt32(1);
            writer.WriteByte((byte)ReportReason.Other);
            writer.WriteString(new string('a', MatchProtocol.MaxReportCommentBytes + 1));

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void StreakActivatedWithATarget()
        {
            var got = ToClient<StreakActivatedMessage>(new StreakActivatedMessage
            {
                PlayerId = 3,
                Team = Team.Bravo,
                RewardId = "airstrike",
                HasTarget = true,
                Target = new Vector3(10f, 0f, -5f),
                DurationSeconds = 1.75f,
            });

            Assert.That(got.PlayerId, Is.EqualTo(3));
            Assert.That(got.Team, Is.EqualTo(Team.Bravo));
            Assert.That(got.RewardId, Is.EqualTo("airstrike"));
            Assert.That(got.HasTarget, Is.True);
            Assert.That(got.Target, Is.EqualTo(new Vector3(10f, 0f, -5f)));
            Assert.That(got.DurationSeconds, Is.EqualTo(1.75f));
        }

        [Test]
        public void StreakActivatedWithoutATargetLeavesOutThePosition()
        {
            var withTarget = new StreakActivatedMessage { PlayerId = 1, RewardId = "uav", HasTarget = true, Target = Vector3.One, DurationSeconds = 20f };
            var without = new StreakActivatedMessage { PlayerId = 1, RewardId = "uav", HasTarget = false, Target = Vector3.One, DurationSeconds = 20f };

            var got = ToClient<StreakActivatedMessage>(without);

            Assert.That(Size(withTarget) - Size(without), Is.EqualTo(12));
            Assert.That(got.HasTarget, Is.False);
            Assert.That(got.Target, Is.EqualTo(Vector3.Zero));
            Assert.That(got.DurationSeconds, Is.EqualTo(20f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void AStreakDurationThatIsNegativeOrNotFiniteIsNotWritten(float seconds)
        {
            var sent = new StreakActivatedMessage { PlayerId = 1, RewardId = "uav", DurationSeconds = seconds };

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void AStreakActivatedWithoutARewardIsNotWritten()
        {
            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(new StreakActivatedMessage { PlayerId = 1 }));
        }

        [TestCase("uav", -1f)]
        [TestCase("", 1f)]
        public void AnUnreadableStreakActivatedIsRejected(string reward, float seconds)
        {
            var writer = Head(MessageIds.StreakActivated, StreakActivatedMessage.CurrentVersion);
            writer.WriteInt32(1);
            writer.WriteByte((byte)Team.Alpha);
            writer.WriteString(reward);
            writer.WriteBool(false);
            writer.WriteSingle(seconds);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void KillCamClipRoundTripsIntoACoreClip()
        {
            var sent = new KillCamClipMessage { KillerId = 2, VictimId = 5 };
            sent.Add(0f, new KillCamPose(new Vector3(0f, 1.6f, 0f), 350f));
            sent.Add(0.5f, new KillCamPose(new Vector3(1f, 1.6f, 0f), 10f));
            sent.Add(1f, new KillCamPose(new Vector3(2f, 1.6f, 0f), 30f));

            var got = ToClient<KillCamClipMessage>(sent);
            var clip = got.ToClip();

            Assert.That(got.KillerId, Is.EqualTo(2));
            Assert.That(got.VictimId, Is.EqualTo(5));
            Assert.That(got.Count, Is.EqualTo(3));
            Assert.That(got.OffsetAt(1), Is.EqualTo(0.5f));
            Assert.That(got.PoseAt(0), Is.EqualTo(new KillCamPose(new Vector3(0f, 1.6f, 0f), 350f)));
            Assert.That(got.PoseAt(2).Yaw, Is.EqualTo(30f));
            Assert.That(clip.Count, Is.EqualTo(3));
            Assert.That(clip.Duration, Is.EqualTo(1f));
            Assert.That(clip.Sample(0.25f).Yaw, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(clip.Sample(0.75f).Eye, Is.EqualTo(new Vector3(1.5f, 1.6f, 0f)));
        }

        [Test]
        public void AnEmptyKillCamClipReadsAsTheEmptyClip()
        {
            var got = ToClient<KillCamClipMessage>(new KillCamClipMessage { KillerId = 1, VictimId = 2 });

            Assert.That(got.Count, Is.Zero);
            Assert.That(got.ToClip(), Is.SameAs(KillCamClip.Empty));
        }

        [Test]
        public void EachKillCamSampleTakesTwentyBytes()
        {
            var one = new KillCamClipMessage { KillerId = 1, VictimId = 2 };
            one.Add(0f, new KillCamPose(Vector3.Zero, 0f));
            var two = new KillCamClipMessage { KillerId = 1, VictimId = 2 };
            two.Add(0f, new KillCamPose(Vector3.Zero, 0f));
            two.Add(0.1f, new KillCamPose(Vector3.Zero, 0f));

            // 秒 (4)・目の位置 (12)・向き (4)
            Assert.That(Size(two) - Size(one), Is.EqualTo(20));

            // Id・版 (2 + 1)、倒した人・倒された人 (4 + 4)、数 (2)
            Assert.That(Size(one), Is.EqualTo(3 + 8 + 2 + 20));
        }

        [TestCase(0.1f)]
        [TestCase(float.NaN)]
        public void AKillCamClipMustStartAtZero(float first)
        {
            var clip = new KillCamClipMessage();

            Assert.Throws<ArgumentOutOfRangeException>(() => clip.Add(first, new KillCamPose(Vector3.Zero, 0f)));
            Assert.That(clip.Count, Is.Zero);
        }

        [TestCase(0.5f)]
        [TestCase(0.25f)]
        public void KillCamOffsetsMustStrictlyIncrease(float next)
        {
            var clip = new KillCamClipMessage();
            clip.Add(0f, new KillCamPose(Vector3.Zero, 0f));
            clip.Add(0.5f, new KillCamPose(Vector3.Zero, 0f));

            Assert.Throws<ArgumentOutOfRangeException>(() => clip.Add(next, new KillCamPose(Vector3.Zero, 0f)));
            Assert.That(clip.Count, Is.EqualTo(2));
        }

        [Test]
        public void AKillCamClipStopsAtItsLimit()
        {
            var clip = new KillCamClipMessage();
            for (var i = 0; i < KillCamClipMessage.MaxSamples; i++)
            {
                clip.Add(i * 0.01f, new KillCamPose(Vector3.Zero, 0f));
            }

            Assert.Throws<InvalidOperationException>(() => clip.Add(KillCamClipMessage.MaxSamples, new KillCamPose(Vector3.Zero, 0f)));
            Assert.That(ToClient<KillCamClipMessage>(clip).Count, Is.EqualTo(KillCamClipMessage.MaxSamples));
        }

        [Test]
        public void AKillCamClipWithNegativeIdsIsNotWritten()
        {
            Assert.Throws<InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new KillCamClipMessage { KillerId = -1, VictimId = 2 }));
        }

        [TestCase(0.5f, 1f)]
        [TestCase(0f, 0f)]
        [TestCase(0f, -1f)]
        [TestCase(float.NaN, 1f)]
        public void KillCamOffsetsThatDoNotStartAtZeroAndIncreaseAreUnreadable(float first, float second)
        {
            var writer = KillCamHead(2);
            WriteSample(writer, first, Vector3.Zero, 0f);
            WriteSample(writer, second, Vector3.Zero, 0f);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void ANonFiniteKillCamPoseIsUnreadable()
        {
            var writer = KillCamHead(1);
            WriteSample(writer, 0f, new Vector3(0f, float.NaN, 0f), 0f);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void MoreKillCamSamplesThanTheLimitAreUnreadable()
        {
            var writer = KillCamHead(KillCamClipMessage.MaxSamples + 1);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void ReadingAShorterClipDropsTheOldSamples()
        {
            var longClip = new KillCamClipMessage { KillerId = 1, VictimId = 2 };
            longClip.Add(0f, new KillCamPose(Vector3.Zero, 0f));
            longClip.Add(1f, new KillCamPose(Vector3.One, 0f));
            var shortClip = new KillCamClipMessage { KillerId = 3, VictimId = 4 };
            shortClip.Add(0f, new KillCamPose(Vector3.UnitX, 90f));
            var writer = new MessageWriter();
            writer.WriteMessage(longClip);
            _client.Read(writer.AsSegment());
            writer.Reset();
            writer.WriteMessage(shortClip);

            var got = (KillCamClipMessage)_client.Read(writer.AsSegment());

            Assert.That(got.Count, Is.EqualTo(1));
            Assert.That(got.KillerId, Is.EqualTo(3));
            Assert.That(got.PoseAt(0).Eye, Is.EqualTo(Vector3.UnitX));
            Assert.Throws<ArgumentOutOfRangeException>(() => got.OffsetAt(1));
        }

        private static MessageWriter KillCamHead(int count)
        {
            var writer = Head(MessageIds.KillCamClip, KillCamClipMessage.CurrentVersion);
            writer.WriteInt32(1);
            writer.WriteInt32(2);
            writer.WriteUInt16((ushort)count);
            return writer;
        }

        private static void WriteSample(MessageWriter writer, float offset, Vector3 eye, float yaw)
        {
            writer.WriteSingle(offset);
            writer.WriteVector3(eye);
            writer.WriteSingle(yaw);
        }

        private static MessageWriter Head(ushort id, byte version)
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(id);
            writer.WriteByte(version);
            return writer;
        }

        private static int Size(IMessage message)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            return writer.Length;
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
