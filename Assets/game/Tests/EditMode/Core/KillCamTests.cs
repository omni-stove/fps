using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class KillCamTests
    {
        private const float _tolerance = 1e-4f;

        private static readonly float[] _zeroOne = { 0f, 1f };
        private static readonly float[] _zeroTwo = { 0f, 2f };
        private static readonly float[] _zeroOnly = { 0f };
        private static readonly float[] _halfOne = { 0.5f, 1f };
        private static readonly float[] _zeroZero = { 0f, 0f };

        [Test]
        public void BufferCapturesTheLastSecondsAndInterpolatesTheStart()
        {
            var buffer = new KillCamBuffer(1f);
            for (var i = 0; i <= 30; i++)
            {
                buffer.Record(i / 10.0, Pose(i, 90f));
            }

            var clip = buffer.Capture(3.0, 1.05f);

            Assert.That(clip.IsEmpty, Is.False);
            Assert.That(clip.Duration, Is.EqualTo(1.05f).Within(_tolerance));
            Assert.That(clip.Sample(0f).Eye.X, Is.EqualTo(19.5f).Within(_tolerance), "the start falls between two records");
            Assert.That(clip.Sample(clip.Duration).Eye.X, Is.EqualTo(30f).Within(_tolerance), "ends at the time of death");
            Assert.That(clip.Sample(0.55f).Eye.X, Is.EqualTo(25f).Within(_tolerance));
        }

        [Test]
        public void BufferStartsAtTheOldestRecordWhenItHasLessThanTheSeconds()
        {
            var buffer = new KillCamBuffer(4f);
            for (var i = 0; i <= 10; i++)
            {
                buffer.Record(5.0 + (i / 10.0), Pose(i, 0f));
            }

            var clip = buffer.Capture(6.0);

            Assert.That(clip.Duration, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(clip.Sample(0f).Eye.X, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void BufferIgnoresRecordsAfterTheEndTime()
        {
            var buffer = new KillCamBuffer(4f);
            for (var i = 0; i <= 20; i++)
            {
                buffer.Record(i / 10.0, Pose(i, 0f));
            }

            var clip = buffer.Capture(1.0);

            Assert.That(clip.Duration, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(clip.Sample(clip.Duration).Eye.X, Is.EqualTo(10f).Within(_tolerance));
        }

        [Test]
        public void BufferKeepsOnlyItsCapacityAndDropsTheOldest()
        {
            var buffer = new KillCamBuffer(0.5f);
            for (var i = 0; i < 1000; i++)
            {
                buffer.Record(i * KillCamBuffer.SampleInterval * 1.01, Pose(i, 0f));
            }

            Assert.That(buffer.Count, Is.EqualTo(buffer.Capacity));
            var clip = buffer.Capture(999 * KillCamBuffer.SampleInterval * 1.01);
            Assert.That(clip.Duration, Is.EqualTo(0.5f).Within(1e-3f), "the window is still the whole seconds");
        }

        [Test]
        public void BufferSkipsRecordsCloserThanTheSampleIntervalAndOverwritesTheSameTime()
        {
            var buffer = new KillCamBuffer(1f);

            Assert.That(buffer.Record(1.0, Pose(0, 0f)), Is.True);
            Assert.That(buffer.Record(1.0 + (KillCamBuffer.SampleInterval * 0.5), Pose(1, 0f)), Is.False);
            Assert.That(buffer.Record(1.0, Pose(2, 0f)), Is.True);
            Assert.That(buffer.Count, Is.EqualTo(1));

            buffer.Record(2.0, Pose(4, 0f));
            Assert.That(buffer.Capture(2.0).Sample(0f).Eye.X, Is.EqualTo(2f).Within(_tolerance), "the same time overwrites");
        }

        [Test]
        public void BufferRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillCamBuffer(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillCamBuffer(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KillCamBuffer(float.PositiveInfinity));

            var buffer = new KillCamBuffer();
            buffer.Record(2.0, Pose(0, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Record(1.0, Pose(0, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Record(double.NaN, Pose(0, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Capture(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Capture(2.0, -1f));
        }

        [Test]
        public void BufferWithoutEnoughRecordsCapturesNothing()
        {
            var buffer = new KillCamBuffer();
            Assert.That(buffer.Capture(1.0).IsEmpty, Is.True, "no record");

            buffer.Record(1.0, Pose(0, 0f));
            Assert.That(buffer.Capture(1.0).IsEmpty, Is.True, "a single record has no length");

            buffer.Record(2.0, Pose(1, 0f));
            Assert.That(buffer.Capture(0.5).IsEmpty, Is.True, "everything is after the end time");

            buffer.Clear();
            Assert.That(buffer.Count, Is.EqualTo(0));
            Assert.That(buffer.Capture(2.0).IsEmpty, Is.True);
        }

        [Test]
        public void PoseTurnsTheShortWayAndKeepsYawInRange()
        {
            var from = new KillCamPose(Vector3.Zero, 350f);
            var to = new KillCamPose(new Vector3(2f, 0f, 0f), 10f);

            var middle = KillCamPose.Lerp(from, to, 0.5f);

            Assert.That(middle.Eye.X, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(middle.Yaw, Is.EqualTo(0f).Within(_tolerance), "crosses 0 instead of turning through 180");
            Assert.That(new KillCamPose(Vector3.Zero, -90f).Yaw, Is.EqualTo(270f).Within(_tolerance));
            Assert.That(new KillCamPose(Vector3.Zero, 720f).Yaw, Is.EqualTo(0f).Within(_tolerance));
            Assert.Throws<ArgumentException>(() => new KillCamPose(new Vector3(float.NaN, 0f, 0f), 0f));
            Assert.Throws<ArgumentException>(() => new KillCamPose(Vector3.Zero, float.PositiveInfinity));
        }

        [Test]
        public void ClipClampsSamplesAndRejectsBadOffsets()
        {
            var clip = new KillCamClip(_zeroOne, new[] { Pose(0, 0f), Pose(10, 0f) });

            Assert.That(clip.Sample(-1f).Eye.X, Is.EqualTo(0f));
            Assert.That(clip.Sample(float.NaN).Eye.X, Is.EqualTo(0f));
            Assert.That(clip.Sample(5f).Eye.X, Is.EqualTo(10f));
            Assert.That(clip.Sample(0.25f).Eye.X, Is.EqualTo(2.5f).Within(_tolerance));

            Assert.Throws<ArgumentNullException>(() => new KillCamClip(null, new KillCamPose[0]));
            Assert.Throws<ArgumentException>(() => new KillCamClip(_zeroOnly, new KillCamPose[0]));
            Assert.Throws<ArgumentException>(() => new KillCamClip(_halfOne, new[] { Pose(0, 0f), Pose(1, 0f) }));
            Assert.Throws<ArgumentException>(() => new KillCamClip(_zeroZero,new[] { Pose(0, 0f), Pose(1, 0f) }));
            Assert.Throws<InvalidOperationException>(() => KillCamClip.Empty.Sample(0f));
            Assert.That(KillCamClip.Empty.IsEmpty, Is.True);
        }

        [Test]
        public void FromHistoriesCutsTheSecondsBeforeTheEndTick()
        {
            var positions = new HitboxHistory();
            var yaws = new HitboxHistory();
            for (var tick = 100; tick <= 160; tick++)
            {
                positions.Record(tick, new Vector3(tick, 0f, 0f));
                yaws.Record(tick, new Vector3(1f, 0f, 0f));
            }

            var clip = KillCamClip.FromHistories(positions, yaws, 150f, 60f, 0.5f, new Vector3(0f, 1.6f, 0f), 0f);

            Assert.That(clip.Duration, Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(clip.Count, Is.EqualTo(31), "one pose per tick");
            var start = clip.Sample(0f);
            Assert.That(start.Eye.X, Is.EqualTo(120f).Within(_tolerance));
            Assert.That(start.Eye.Y, Is.EqualTo(1.6f).Within(_tolerance), "the eye is above the body");
            Assert.That(start.Yaw, Is.EqualTo(90f).Within(1e-2f), "facing +X");
            Assert.That(clip.Sample(clip.Duration).Eye.X, Is.EqualTo(150f).Within(_tolerance));
        }

        [Test]
        public void FromHistoriesStaysInsideTheRecords()
        {
            var positions = new HitboxHistory();
            for (var tick = 10; tick <= 40; tick++)
            {
                positions.Record(tick, new Vector3(tick, 0f, 0f));
            }

            var clip = KillCamClip.FromHistories(positions, null, 1000f, 60f, 4f, Vector3.Zero, 45f);

            Assert.That(clip.Duration, Is.EqualTo(0.5f).Within(_tolerance), "from the oldest to the newest record");
            Assert.That(clip.Sample(0f).Eye.X, Is.EqualTo(10f).Within(_tolerance));
            Assert.That(clip.Sample(0f).Yaw, Is.EqualTo(45f).Within(_tolerance), "no facing history keeps the fallback yaw");
        }

        [Test]
        public void FromHistoriesWithoutRecordsIsEmpty()
        {
            var positions = new HitboxHistory();
            Assert.That(KillCamClip.FromHistories(positions, null, 10f, 60f, 1f, Vector3.Zero, 0f).IsEmpty, Is.True);

            positions.Record(10, Vector3.Zero);
            Assert.That(KillCamClip.FromHistories(positions, null, 10f, 60f, 1f, Vector3.Zero, 0f).IsEmpty, Is.True, "one tick has no length");

            Assert.Throws<ArgumentNullException>(() => KillCamClip.FromHistories(null, null, 10f, 60f, 1f, Vector3.Zero, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => KillCamClip.FromHistories(positions, null, float.NaN, 60f, 1f, Vector3.Zero, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => KillCamClip.FromHistories(positions, null, 10f, 0f, 1f, Vector3.Zero, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => KillCamClip.FromHistories(positions, null, 10f, 60f, 0f, Vector3.Zero, 0f));
        }

        [Test]
        public void HistoryRangeIsTheOldestAndNewestTick()
        {
            var history = new HitboxHistory(4);
            Assert.That(history.TryGetRange(out _, out _), Is.False);

            for (var tick = 1; tick <= 6; tick++)
            {
                history.Record(tick, Vector3.Zero);
            }

            Assert.That(history.TryGetRange(out var oldest, out var newest), Is.True);
            Assert.That(oldest, Is.EqualTo(3));
            Assert.That(newest, Is.EqualTo(6));
        }

        [Test]
        public void PlaybackFinishesOnceAtTheEndAtTheGivenSpeed()
        {
            var playback = new KillCamPlayback(new KillCamClip(_zeroTwo,new[] { Pose(0, 0f), Pose(2, 0f) }));

            Assert.That(playback.IsFinished, Is.False);
            Assert.That(playback.Advance(1f, 0.5f), Is.False);
            Assert.That(playback.Elapsed, Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(playback.Progress, Is.EqualTo(0.25f).Within(_tolerance));
            Assert.That(playback.Pose.Eye.X, Is.EqualTo(0.5f).Within(_tolerance));

            Assert.That(playback.Advance(3f, 0.5f), Is.True, "reaches the end on this call");
            Assert.That(playback.IsFinished, Is.True);
            Assert.That(playback.Elapsed, Is.EqualTo(2f));
            Assert.That(playback.Advance(1f, 1f), Is.False, "already finished");
        }

        [Test]
        public void PlaybackOfAnEmptyClipIsFinishedFromTheStart()
        {
            var playback = new KillCamPlayback(null);

            Assert.That(playback.IsFinished, Is.True);
            Assert.That(playback.Progress, Is.EqualTo(1f));
            Assert.That(playback.Advance(1f, 1f), Is.False);
        }

        [Test]
        public void PlaybackRejectsInvalidTimeAndSpeed()
        {
            var playback = new KillCamPlayback(new KillCamClip(_zeroOne,new[] { Pose(0, 0f), Pose(1, 0f) }));

            Assert.Throws<ArgumentOutOfRangeException>(() => playback.Advance(-1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => playback.Advance(float.NaN, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => playback.Advance(0.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => playback.Advance(0.1f, float.PositiveInfinity));
            Assert.That(KillCamPlayback.IsValidSpeed(0.5f), Is.True);
            Assert.That(KillCamPlayback.IsValidSpeed(float.NaN), Is.False);
        }

        [Test]
        public void LogLinesCarryTheTag()
        {
            Assert.That(KillCamLog.Replaying("Player", "Enemy-1", 4f, 1f), Is.EqualTo("[KillCam] replaying Enemy-1 for 4.0s at 1.00x on Player"));
            Assert.That(KillCamLog.Finished("Player", "Enemy-1"), Is.EqualTo("[KillCam] finished replaying Enemy-1 on Player; switching to the killer view"));
            Assert.That(KillCamLog.NoRecord("Player", "Enemy-1"), Is.EqualTo("[KillCam] no record of Enemy-1 for Player; switching to the killer view"));
            Assert.That(KillCamLog.NoKiller("Player"), Is.EqualTo("[KillCam] no killer for Player; skipping the replay"));
            Assert.That(KillCamLog.Stopped("Player", "Enemy-1", "Respawned"), Is.EqualTo("[KillCam] stopped replaying Enemy-1 on Player (Respawned)"));
        }

        [Test]
        public void CaptionNamesTheKiller()
        {
            var caption = KillCamText.Caption(LocalizedText.Literal("Enemy-1"));

            Assert.That(caption.Key, Is.EqualTo("hud.killCam.caption"));
        }

        [Test]
        public void RulesJsonReadsKillCam()
        {
            var rules = GameRulesJson.Parse("{\"deathCamera\": \"KillCam\"}", "Rules/Test.json");

            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.KillCam));
        }

        private static KillCamPose Pose(float x, float yaw)
        {
            return new KillCamPose(new Vector3(x, 0f, 0f), yaw);
        }
    }
}
