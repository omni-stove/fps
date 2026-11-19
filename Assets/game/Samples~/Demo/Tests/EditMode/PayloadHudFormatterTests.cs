using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class PayloadHudFormatterTests
    {
        private const int AttackerId = 1;
        private const int DefenderId = 2;

        private static readonly string[] _expectedPushing = { "台車 45%", "中継点 1/2", "2 人が押している" };
        private static readonly string[] _expectedAttacker = { "台車 45%", "中継点 1/2", "2 人が押している", "あなたは攻め: 台車のそばにいて終点まで押す" };
        private static readonly string[] _expectedDefender = { "台車 45%", "中継点 1/2", "2 人が押している", "あなたは守り: 台車のそばに立って時間切れまで止める" };
        private static readonly string[] _expectedRespawn = { "リスポーンまで 3 秒", "台車 45%", "中継点 1/2", "2 人が押している", "あなたは攻め: 台車のそばにいて終点まで押す" };
        private static readonly string[] _expectedWithoutCheckpoints = { "台車 0%", "台車は止まっている" };
        private static readonly string[] _expectedFromPayload = { "台車 10%", "中継点 0/1", "1 人が押している", "あなたは攻め: 台車のそばにいて終点まで押す" };
        private static readonly string[] _expectedDefenderScores = { "BRAVO 守り", "ALPHA 攻め" };
        private static readonly string[] _expectedBravoAttackScores = { "BRAVO 攻め", "ALPHA 守り" };

        [Test]
        public void TheProgressIsShownInWholePercentRoundedDown()
        {
            Assert.That(PayloadHudFormatter.ProgressPercent(0f), Is.EqualTo(0));
            Assert.That(PayloadHudFormatter.ProgressPercent(0.29f), Is.EqualTo(29));
            Assert.That(PayloadHudFormatter.ProgressPercent(0.4567f), Is.EqualTo(45));
            Assert.That(PayloadHudFormatter.ProgressPercent(0.999f), Is.EqualTo(99), "the end is not shown before the cart arrives");
            Assert.That(PayloadHudFormatter.ProgressPercent(1f), Is.EqualTo(100));
            Assert.Throws<ArgumentOutOfRangeException>(() => PayloadHudFormatter.ProgressPercent(-0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PayloadHudFormatter.ProgressPercent(1.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PayloadHudFormatter.ProgressPercent(float.NaN));
        }

        [Test]
        public void TheLinesReadInJapanese()
        {
            Assert.That(Localizer.Resolve(PayloadHudFormatter.Progress(0.5f)), Is.EqualTo("台車 50%"));
            Assert.That(Localizer.Resolve(PayloadHudFormatter.Checkpoints(2, 3)), Is.EqualTo("中継点 2/3"));
            Assert.That(Localizer.Resolve(PayloadHudFormatter.Pushing(3)), Is.EqualTo("3 人が押している"));
            Assert.That(Localizer.Resolve(PayloadHudFormatter.Contested), Is.EqualTo("台車を止められている"));
            Assert.That(Localizer.Resolve(PayloadHudFormatter.RollingBack), Is.EqualTo("台車が戻っている"));
            Assert.That(Localizer.Resolve(PayloadHudFormatter.Idle), Is.EqualTo("台車は止まっている"));
            Assert.That(Localizer.Resolve(PayloadHudFormatter.Role(Side.Alpha, Side.Alpha)), Is.EqualTo("ALPHA 攻め"));
            Assert.That(Localizer.Resolve(PayloadHudFormatter.Role(Side.Bravo, Side.Alpha)), Is.EqualTo("BRAVO 守り"));
        }

        [Test]
        public void TheCountsMustBeInRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PayloadHudFormatter.Checkpoints(-1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => PayloadHudFormatter.Checkpoints(3, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => PayloadHudFormatter.Checkpoints(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PayloadHudFormatter.Pushing(0));
            Assert.Throws<ArgumentNullException>(() => PayloadHudFormatter.HudLines(null, null));
            Assert.Throws<ArgumentNullException>(() => PayloadHudFormatter.Motion(null));
        }

        [Test]
        public void StoppedComesBeforeRollingBackAndRollingBackBeforePushing()
        {
            Assert.That(PayloadHudFormatter.Motion(Detail(pushers: 1, contested: true)), Is.EqualTo(PayloadHudFormatter.Contested));
            Assert.That(PayloadHudFormatter.Motion(Detail(pushers: 0, rollingBack: true)), Is.EqualTo(PayloadHudFormatter.RollingBack));
            Assert.That(Localizer.Resolve(PayloadHudFormatter.Motion(Detail(pushers: 2))), Is.EqualTo("2 人が押している"));
            Assert.That(PayloadHudFormatter.Motion(Detail(pushers: 0)), Is.EqualTo(PayloadHudFormatter.Idle));
            var short2 = new PayloadStatus(Side.Alpha, 0.4f, 1, 2, 1, false, false, 2);
            Assert.That(PayloadHudFormatter.Motion(short2), Is.EqualTo(PayloadHudFormatter.Idle), "fewer than minPushers do not move the cart");
        }

        [Test]
        public void TheLinesShowProgressCheckpointsTheCartAndTheOwnRole()
        {
            Assert.That(PayloadHudFormatter.HudLines(Detail(pushers: 2), null).Select(Localizer.Resolve), Is.EqualTo(_expectedPushing));
            Assert.That(PayloadHudFormatter.HudLines(Detail(pushers: 2), Side.Alpha).Select(Localizer.Resolve), Is.EqualTo(_expectedAttacker));
            Assert.That(PayloadHudFormatter.HudLines(Detail(pushers: 2), Side.Bravo).Select(Localizer.Resolve), Is.EqualTo(_expectedDefender));
        }

        [Test]
        public void ARouteWithoutCheckpointsHasNoCheckpointLine()
        {
            var detail = new PayloadStatus(Side.Alpha, 0f, 0, 0, 0, false, false);

            Assert.That(PayloadHudFormatter.HudLines(detail, null).Select(Localizer.Resolve), Is.EqualTo(_expectedWithoutCheckpoints));
        }

        [Test]
        public void TheFormatterIsRegisteredAndPutsTheRespawnCountdownFirst()
        {
            var status = Status(Detail(pushers: 2), Side.Alpha, respawn: 2.5f);

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<PayloadHudFormatter>());
            Assert.That(Lines(status), Is.EqualTo(_expectedRespawn));
        }

        [Test]
        public void TheScoresNameTheRolesInsteadOfTheAlwaysZeroScores()
        {
            var formatter = new PayloadHudFormatter();

            var defending = formatter.Scores(Status(Detail(pushers: 0), Side.Bravo));
            Assert.That(defending.Select(line => Localizer.Resolve(line.Text)), Is.EqualTo(_expectedDefenderScores));
            Assert.That(defending[0].IsOwn, Is.True);
            Assert.That(defending.Any(line => line.IsLeading), Is.False);

            var bravoAttacks = new PayloadStatus(Side.Bravo, 0f, 0, 2, 0, false, false);
            Assert.That(
                formatter.Scores(Status(bravoAttacks, Side.Bravo)).Select(line => Localizer.Resolve(line.Text)),
                Is.EqualTo(_expectedBravoAttackScores));
        }

        [Test]
        public void TheClockAndTheResultComeFromTheModeStatus()
        {
            var formatter = new PayloadHudFormatter();
            var detail = Detail(pushers: 0);

            Assert.That(Localizer.Resolve(formatter.Time(new ModeStatus(61f, null, Side.Alpha, null, false, null, detail))), Is.EqualTo("01:01"));
            Assert.That(Localizer.Resolve(formatter.Result(new ModeStatus(0f, null, Side.Alpha, null, true, Side.Alpha, detail))), Is.EqualTo("ALPHA の勝ち"));
            Assert.That(Localizer.Resolve(formatter.Result(new ModeStatus(0f, null, Side.Alpha, null, true, Side.Bravo, detail))), Is.EqualTo("BRAVO の勝ち"));
            Assert.That(formatter.Result(new ModeStatus(10f, null, Side.Alpha, null, false, null, detail)), Is.EqualTo(default(LocalizedText)));
        }

        [Test]
        public void TheLinesReadAPushedPayload()
        {
            var points = new[]
            {
                System.Numerics.Vector3.Zero,
                new System.Numerics.Vector3(0f, 0f, 10f),
                new System.Numerics.Vector3(0f, 0f, 20f),
            };
            var payload = new Payload(new PayloadSpec(2f, 1, null, 10f, 0.5f, 60f, 300f), new PayloadRoute(points, new[] { 1 }), Side.Alpha, 5f);
            payload.Session.AddPlayer(AttackerId, Side.Alpha);
            payload.Session.AddPlayer(DefenderId, Side.Bravo);
            payload.Session.Start();
            payload.SetNearCart(AttackerId, true);
            payload.Session.Tick(1f);

            var session = payload.Session;
            var sideScores = session.Sides.Select(side => new SideScore(side, session.GetScore(side))).ToArray();
            var status = new ModeStatus(session.RemainingSeconds, sideScores, Side.Alpha, null, false, null, PayloadStatus.Of(payload));
            var formatter = ModeHudFormatters.For(status);

            Assert.That(Lines(status), Is.EqualTo(_expectedFromPayload));
            Assert.That(Localizer.Resolve(formatter.Time(status)), Is.EqualTo("04:59"));
        }

        private static PayloadStatus Detail(int pushers, bool contested = false, bool rollingBack = false)
        {
            return new PayloadStatus(Side.Alpha, 0.4567f, 1, 2, pushers, contested, rollingBack);
        }

        private static ModeStatus Status(PayloadStatus detail, Side? ownSide, float? respawn = null)
        {
            var sideScores = new[] { new SideScore(Side.Alpha, 0d), new SideScore(Side.Bravo, 0d) };
            return new ModeStatus(120f, sideScores, ownSide, respawn, false, null, detail);
        }

        private static IEnumerable<string> Lines(ModeStatus status)
        {
            var lines = new List<LocalizedText>();
            ModeHudFormatters.For(status).AppendLines(status, lines);
            return lines.Select(Localizer.Resolve);
        }
    }
}
