using System;
using System.IO;
using NUnit.Framework;

namespace Shooter.Server.Eos.Tests
{
    public sealed class StatsIngestQueueTests
    {
        private const string _alice = "0123456789abcdef0123456789abcdef";
        private const string _bob = "fedcba9876543210fedcba9876543210";

        private StringWriter _log;
        private FakeStatsIngestBackend _backend;
        private StatsIngestQueue _queue;
        private float _now;
        private int _slept;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _backend = new FakeStatsIngestBackend();
            _now = 0f;
            _slept = 0;
            _queue = new StatsIngestQueue(
                _backend,
                _log,
                () => _now,
                milliseconds =>
                {
                    _slept += milliseconds;
                    _now += milliseconds / 1000f;
                });
        }

        [TearDown]
        public void TearDown()
        {
            _queue.Dispose();
            _backend.Dispose();
            _log.Dispose();
        }

        [Test]
        public void SendsOneUpdateAtATimeInOrder()
        {
            _queue.Enqueue("m1", _alice, new[] { new StatIngest("Kills", 3) });
            _queue.Enqueue("m1", _bob, new[] { new StatIngest("Kills", 1), new StatIngest("MatchesWon", 1) });

            Assert.That(_backend.Calls, Is.Empty, "nothing is sent before the tick");
            _queue.Tick();
            _queue.Tick();

            Assert.That(_backend.Calls, Has.Count.EqualTo(1));
            Assert.That(_backend.Calls[0].UserId, Is.EqualTo(_alice));
            Assert.That(_backend.Calls[0].Stats, Is.EqualTo(new[] { new StatIngest("Kills", 3) }));

            _backend.CompleteNext();
            _queue.Tick();

            Assert.That(_backend.Calls, Has.Count.EqualTo(2));
            Assert.That(_backend.Calls[1].UserId, Is.EqualTo(_bob));
            Assert.That(_backend.Calls[1].Stats, Is.EqualTo(new[] { new StatIngest("Kills", 1), new StatIngest("MatchesWon", 1) }));
            _backend.CompleteNext();

            Assert.That(_queue.Pending, Is.Zero);
            Assert.That(_queue.Sent, Is.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[Stats] sent match=m1 user=" + _alice + " Kills=3"));
            Assert.That(_log.ToString(), Does.Contain("[Stats] sent match=m1 user=" + _bob + " Kills=1 MatchesWon=1"));
        }

        [Test]
        public void KeepsItsOwnCopyOfTheStats()
        {
            var stats = new[] { new StatIngest("Kills", 3) };
            _queue.Enqueue("m1", _alice, stats);
            stats[0] = new StatIngest("Kills", 99);

            _queue.Tick();

            Assert.That(_backend.Calls[0].Stats, Is.EqualTo(new[] { new StatIngest("Kills", 3) }));
        }

        [Test]
        public void IgnoresAnEmptyUpdateAndRejectsAMissingUser()
        {
            _queue.Enqueue("m1", _alice, Array.Empty<StatIngest>());

            Assert.That(_queue.Pending, Is.Zero);
            Assert.Throws<ArgumentException>(() => _queue.Enqueue("m1", null, new[] { new StatIngest("Kills", 1) }));
            Assert.Throws<ArgumentException>(() => _queue.Enqueue("m1", string.Empty, new[] { new StatIngest("Kills", 1) }));
        }

        [Test]
        public void RetriesATransientFailureAfterADelayWhileSendingOthers()
        {
            _queue.Enqueue("m1", _alice, new[] { new StatIngest("Kills", 3) });
            _queue.Enqueue("m1", _bob, new[] { new StatIngest("Kills", 1) });
            _queue.Tick();
            _backend.CompleteNext("ingesting EOS stats failed: TooManyRequests", canRetry: true);

            Assert.That(_log.ToString(), Does.Contain(
                "[Stats] sending match=m1 user=" + _alice + " failed (attempt 1): ingesting EOS stats failed: TooManyRequests; retrying in 2s"));

            _queue.Tick();
            Assert.That(_backend.Calls, Has.Count.EqualTo(2));
            Assert.That(_backend.Calls[1].UserId, Is.EqualTo(_bob), "the next player is not held up by the retry");
            _backend.CompleteNext();

            _now = 1.9f;
            _queue.Tick();
            Assert.That(_backend.Calls, Has.Count.EqualTo(2), "waits for the retry delay");

            _now = 2f;
            _queue.Tick();
            Assert.That(_backend.Calls, Has.Count.EqualTo(3));
            Assert.That(_backend.Calls[2].UserId, Is.EqualTo(_alice));
            _backend.CompleteNext();

            Assert.That(_queue.Sent, Is.EqualTo(2));
            Assert.That(_queue.Dropped, Is.Zero);
            Assert.That(_queue.Pending, Is.Zero);
        }

        [Test]
        public void DoublesTheDelayAndDropsAfterTheLastAttempt()
        {
            _queue.Enqueue("m1", _alice, new[] { new StatIngest("Kills", 3) });

            _queue.Tick();
            _backend.CompleteNext("timed out", canRetry: true);
            _now = 2f;
            _queue.Tick();
            _backend.CompleteNext("timed out", canRetry: true);

            Assert.That(_log.ToString(), Does.Contain("failed (attempt 2): timed out; retrying in 4s"));
            _now = 5.9f;
            _queue.Tick();
            Assert.That(_backend.Calls, Has.Count.EqualTo(2));

            _now = 6f;
            _queue.Tick();
            _backend.CompleteNext("timed out", canRetry: true);

            Assert.That(_backend.Calls, Has.Count.EqualTo(StatsIngestQueue.MaxAttempts));
            Assert.That(_queue.Pending, Is.Zero);
            Assert.That(_queue.Dropped, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Stats] dropped the stats of match=m1 user=" + _alice + " after 3 attempt(s): timed out"));
        }

        [Test]
        public void DropsAFailureThatCannotBeRetriedAtOnce()
        {
            _queue.Enqueue("m1", _alice, new[] { new StatIngest("Kills", 3) });
            _queue.Tick();
            _backend.CompleteNext("ingesting EOS stats failed: InvalidUser", canRetry: false);

            Assert.That(_queue.Dropped, Is.EqualTo(1));
            Assert.That(_queue.Pending, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("after 1 attempt(s): ingesting EOS stats failed: InvalidUser"));
        }

        [Test]
        public void HandlesAnAnswerGivenDuringTheCall()
        {
            _backend.SucceedAtOnce();
            _queue.Enqueue("m1", _alice, new[] { new StatIngest("Kills", 3) });
            _queue.Enqueue("m1", _bob, new[] { new StatIngest("Kills", 1) });

            _queue.Tick();
            _queue.Tick();

            Assert.That(_queue.Sent, Is.EqualTo(2));
            Assert.That(_queue.Pending, Is.Zero);
        }

        [Test]
        public void FlushesWhatIsQueuedBeforeQuitting()
        {
            _backend.OnTick = () =>
            {
                if (_backend.Waiting > 0)
                {
                    _backend.CompleteNext();
                }
            };
            _queue.Enqueue("m1", _alice, new[] { new StatIngest("Kills", 3) });
            _queue.Enqueue("m1", _bob, new[] { new StatIngest("Kills", 1) });

            _queue.FlushAndWait();

            Assert.That(_queue.Sent, Is.EqualTo(2));
            Assert.That(_queue.Pending, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Stats] sending 2 queued stat update(s) before quitting (up to 5s)"));
            Assert.That(_log.ToString(), Does.Not.Contain("were not sent"));
        }

        [Test]
        public void GivesUpFlushingAfterTheWait()
        {
            _queue.Enqueue("m1", _alice, new[] { new StatIngest("Kills", 3) });

            _queue.FlushAndWait();

            Assert.That(_now, Is.GreaterThanOrEqualTo(StatsIngestQueue.FlushWaitSeconds));
            Assert.That(_slept, Is.GreaterThan(0));
            Assert.That(_queue.Pending, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Stats] 1 stat update(s) were not sent before quitting"));
        }

        [Test]
        public void DoesNotWaitWhenNothingIsQueued()
        {
            _queue.FlushAndWait();

            Assert.That(_slept, Is.Zero);
            Assert.That(_log.ToString(), Is.Empty);
        }

        [Test]
        public void IgnoresAnAnswerAfterDisposeAndDisposesTheBackend()
        {
            _queue.Enqueue("m1", _alice, new[] { new StatIngest("Kills", 3) });
            _queue.Tick();

            _queue.Dispose();
            _backend.CompleteNext();

            Assert.That(_backend.Disposed, Is.True);
            Assert.That(_queue.Sent, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => _queue.Enqueue("m1", _alice, new[] { new StatIngest("Kills", 1) }));
        }

        [Test]
        public void ClampsAmountsToTheEosRange()
        {
            Assert.That(StatIngest.Clamp(9000000000L), Is.EqualTo(int.MaxValue));
            Assert.That(StatIngest.Clamp(-9000000000L), Is.EqualTo(int.MinValue));
            Assert.That(StatIngest.Clamp(12), Is.EqualTo(12));
            Assert.Throws<ArgumentException>(() => new StatIngest(" ", 1));
        }
    }
}
