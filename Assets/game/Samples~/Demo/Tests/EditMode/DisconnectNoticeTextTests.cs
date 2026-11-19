using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class DisconnectNoticeTextTests
    {
        private GameLanguage _applied;

        [SetUp]
        public void SetUp()
        {
            _applied = Localizer.Language;
            Localizer.Apply(GameLanguage.Japanese);
        }

        [TearDown]
        public void TearDown()
        {
            Localizer.Apply(_applied);
        }

        private static LocalizedText For(string reason, bool wasConnected, string localVersion = "0.1.0")
        {
            return DisconnectNoticeText.For(DisconnectNotice.Classify(reason, wasConnected, localVersion));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TheServerStoppedReasonSaysTheServerStopped(bool wasConnected)
        {
            var text = For(DisconnectNotice.ServerStoppedReason, wasConnected);

            Assert.That(text, Is.EqualTo(new LocalizedText("net.disconnect.serverStopped")));
            Assert.That(Localizer.Resolve(text), Is.EqualTo("サーバーが止まりました。"));
        }

        [Test]
        public void AVersionMismatchNamesBothVersions()
        {
            var text = For(DisconnectNotice.VersionMismatchReason("0.2.0"), false);

            Assert.That(text, Is.EqualTo(new LocalizedText("net.disconnect.versionMismatchDetail", "0.2.0", "0.1.0")));
            Assert.That(
                Localizer.Resolve(text),
                Is.EqualTo("版が違います (サーバー 0.2.0 / このゲーム 0.1.0)。サーバーと同じ版のゲームでつないでください。"));
        }

        [TestCase("shooter:version-mismatch:")]
        [TestCase("shooter:version-mismatch:<b>red</b>")]
        [TestCase("shooter:version-mismatch:v0.2.0")]
        public void AnUnreadableServerVersionIsLeftOut(string reason)
        {
            var text = For(reason, false);

            Assert.That(text, Is.EqualTo(new LocalizedText("net.disconnect.versionMismatchAdvice", DisconnectNoticeText.VersionMismatchMessage)));
            Assert.That(Localizer.Resolve(text), Is.EqualTo("版が違います。サーバーと同じ版のゲームでつないでください。"));
        }

        [Test]
        public void AnUnreadableLocalVersionIsLeftOut()
        {
            var text = For(DisconnectNotice.VersionMismatchReason("0.2.0"), false, null);

            Assert.That(Localizer.Resolve(text), Is.EqualTo("版が違います。サーバーと同じ版のゲームでつないでください。"));
        }

        [Test]
        public void ARejectionSaysTheServerRefused()
        {
            Assert.That(For(DisconnectNotice.RejectedReason("invalid-payload"), false), Is.EqualTo(DisconnectNoticeText.RejectedMessage));
            Assert.That(For("shooter:rejected:Not Allowed!", false), Is.EqualTo(DisconnectNoticeText.RejectedMessage));
            Assert.That(Localizer.Resolve(DisconnectNoticeText.RejectedMessage), Is.EqualTo("サーバーに接続を断られました。"));
        }

        [Test]
        public void ADrainingServerSaysItIsGettingReadyToStop()
        {
            var text = For(DisconnectNotice.RejectedReason(DisconnectNotice.ServerDrainingDetail), false);

            Assert.That(text, Is.EqualTo(DisconnectNoticeText.ServerDrainingMessage));
            Assert.That(Localizer.Resolve(text), Does.StartWith("サーバーが止まる準備中"));
        }

        [Test]
        public void ABanSaysThePlayerIsBanned()
        {
            var text = For(DisconnectNotice.RejectedReason(DisconnectNotice.BannedDetail), false);

            Assert.That(text, Is.EqualTo(new LocalizedText("net.disconnect.banned")));
            Assert.That(Localizer.Resolve(text), Is.EqualTo("このサーバーから BAN されているため参加できません。"));
            Assert.That(For(DisconnectNotice.RejectedReason(DisconnectNotice.BannedDetail), true), Is.EqualTo(DisconnectNoticeText.BannedMessage), "banned while playing");
        }

        [Test]
        public void AKickSaysTheAdministratorRemovedThePlayer()
        {
            var text = For(DisconnectNotice.RejectedReason(DisconnectNotice.KickedDetail), true);

            Assert.That(text, Is.EqualTo(new LocalizedText("net.disconnect.kicked")));
            Assert.That(Localizer.Resolve(text), Is.EqualTo("サーバーの管理者に切断されました (キック)。"));
        }

        [Test]
        public void TheIdleKickedReasonSaysThePlayerWasIdle()
        {
            var text = For(DisconnectNotice.IdleKickedReason, true);

            Assert.That(text, Is.EqualTo(DisconnectNoticeText.IdleKickedMessage));
            Assert.That(Localizer.Resolve(text), Does.Contain("放置"));
        }

        [Test]
        public void TheVoteKickedReasonSaysPlayersVotedThePlayerOut()
        {
            var text = For(DisconnectNotice.VoteKickedReason, true);

            Assert.That(text, Is.EqualTo(DisconnectNoticeText.VoteKickedMessage));
            Assert.That(text, Is.Not.EqualTo(DisconnectNoticeText.KickedMessage), "not the administrator's kick");
            Assert.That(
                Localizer.Resolve(text),
                Is.EqualTo("プレイヤーの投票で試合から外されました (投票キック)。この試合が終わるまで入り直せません。"));
        }

        [TestCase(null)]
        [TestCase("Disconnected due to host shutting down.")]
        [TestCase("shooter:something-new")]
        public void WithoutAKnownCodeAConnectedClientTimedOut(string reason)
        {
            Assert.That(For(reason, true), Is.EqualTo(DisconnectNoticeText.TimedOutMessage));
            Assert.That(Localizer.Resolve(DisconnectNoticeText.TimedOutMessage), Is.EqualTo("サーバーからの応答が途絶えました (タイムアウト)。"));
        }

        [Test]
        public void WithoutAKnownCodeAClientThatNeverConnectedCouldNotConnect()
        {
            var text = For(null, false);

            Assert.That(text, Is.EqualTo(DisconnectNoticeText.ConnectTimedOutMessage));
            Assert.That(Localizer.Resolve(text), Is.EqualTo("サーバーに接続できませんでした (タイムアウト)。"));
        }

        [Test]
        public void AMenuReturnReasonUsesItsDisconnectOrItsDetail()
        {
            var stopped = DisconnectNotice.Classify(DisconnectNotice.ServerStoppedReason, true, "0.1.0");

            Assert.That(DisconnectNoticeText.For(MenuReturnReason.Disconnected(stopped, 0)), Is.EqualTo(DisconnectNoticeText.ServerStoppedMessage));
            Assert.That(
                DisconnectNoticeText.For(MenuReturnReason.OnlineFailed(7, "[QuickMatch] no lobby was found")),
                Is.EqualTo(LocalizedText.Literal("[QuickMatch] no lobby was found")));
            Assert.That(DisconnectNoticeText.For(MenuReturnReason.OnlineFailed(3, " ")).IsEmpty, Is.True, "a blank detail shows nothing");
            Assert.That(DisconnectNoticeText.For((MenuReturnReason)null).IsEmpty, Is.True);
            Assert.Throws<ArgumentNullException>(() => DisconnectNoticeText.For((DisconnectNotice)null));
        }
    }
}
