using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class RespawnPolicyTests
    {
        [Test]
        public void FromRulesPicksThePolicyForTheRespawnMode()
        {
            var individual = RespawnPolicy.FromRules(GameRules.Default, 3f);
            var wave = RespawnPolicy.FromRules(GameRules.Default.WithRespawnMode(RespawnMode.Wave).WithWaveIntervalSeconds(7f), 2f);
            var none = RespawnPolicy.FromRules(GameRules.Default.WithRespawnMode(RespawnMode.None), 3f);

            Assert.That(individual, Is.TypeOf<IndividualRespawn>());
            Assert.That(((IndividualRespawn)individual).RespawnSeconds, Is.EqualTo(3f));
            Assert.That(wave, Is.TypeOf<WaveRespawn>());
            Assert.That(((WaveRespawn)wave).RespawnSeconds, Is.EqualTo(2f));
            Assert.That(((WaveRespawn)wave).WaveIntervalSeconds, Is.EqualTo(7f));
            Assert.That(none, Is.TypeOf<NoRespawn>());
        }

        [Test]
        public void IndividualReleasesInOrderOfWaitingTime()
        {
            var session = PlayingSession();
            var policy = new IndividualRespawn(3f);
            var released = new List<int>();

            Assert.That(policy.BeginWait(session, 2), Is.False);
            policy.Tick(session, 1f, released.Add);
            Assert.That(policy.BeginWait(session, 1), Is.False);
            Assert.That(policy.GetRemaining(2), Is.EqualTo(2f));

            policy.Tick(session, 5f, released.Add);

            Assert.That(released, Is.EqualTo(new[] { 2, 1 }));
            Assert.That(policy.GetRemaining(1), Is.Null);
        }

        [Test]
        public void IndividualWithZeroSecondsReleasesImmediately()
        {
            var session = PlayingSession();
            var policy = new IndividualRespawn(0f);

            Assert.That(policy.BeginWait(session, 1), Is.True);
            Assert.That(policy.GetRemaining(1), Is.Null);
        }

        [Test]
        public void CancelledWaitIsNotReleased()
        {
            var session = PlayingSession();
            var policy = new IndividualRespawn(1f);
            var released = new List<int>();
            policy.BeginWait(session, 1);
            policy.BeginWait(session, 2);

            policy.Cancel(1);
            policy.Tick(session, 2f, released.Add);
            Assert.That(released, Is.EqualTo(new[] { 2 }));

            policy.BeginWait(session, 3);
            policy.CancelAll();
            policy.Tick(session, 2f, released.Add);
            Assert.That(released, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void IndividualStopsReleasingWhenTheMatchLeavesPlaying()
        {
            var session = PlayingSession();
            var policy = new IndividualRespawn(1f);
            var released = new List<int>();
            policy.BeginWait(session, 1);
            policy.BeginWait(session, 2);

            policy.Tick(session, 1f, id =>
            {
                released.Add(id);
                session.Reset();
            });

            Assert.That(released, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void WaveReleasesEveryoneReadyAtTheWave()
        {
            var session = PlayingSession();
            var policy = new WaveRespawn(2f, 5f);
            var released = new List<int>();

            Assert.That(policy.SecondsUntilNextWave, Is.EqualTo(5f));
            policy.BeginWait(session, 1);
            policy.Tick(session, 2f, released.Add);
            policy.BeginWait(session, 2);
            Assert.That(policy.GetRemaining(1), Is.EqualTo(3f));
            Assert.That(policy.GetRemaining(2), Is.EqualTo(3f));

            policy.Tick(session, 3f, released.Add);

            Assert.That(released, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(policy.SecondsUntilNextWave, Is.EqualTo(5f));
        }

        [Test]
        public void WaveWaitsForTheNextWaveWhenTheWaitIsNotOver()
        {
            var session = PlayingSession();
            var policy = new WaveRespawn(3f, 5f);
            var released = new List<int>();
            policy.Tick(session, 4f, released.Add);
            policy.BeginWait(session, 1);

            Assert.That(policy.GetRemaining(1), Is.EqualTo(6f));

            policy.Tick(session, 1f, released.Add);
            Assert.That(released, Is.Empty);

            policy.Tick(session, 5f, released.Add);
            Assert.That(released, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void WaveResetRestartsTheWaveTimes()
        {
            var session = PlayingSession();
            var policy = new WaveRespawn(0f, 10f);
            policy.Tick(session, 8f, _ => { });
            policy.BeginWait(session, 1);

            policy.Reset();

            Assert.That(policy.GetRemaining(1), Is.Null);
            Assert.That(policy.SecondsUntilNextWave, Is.EqualTo(10f));
        }

        [Test]
        public void NoRespawnNeverReleases()
        {
            var session = PlayingSession();
            var policy = new NoRespawn();
            var released = new List<int>();

            Assert.That(policy.BeginWait(session, 1), Is.False);
            policy.Tick(session, 100f, released.Add);

            Assert.That(released, Is.Empty);
            Assert.That(policy.WillReturn(session, 1), Is.False);
            Assert.That(policy.GetRemaining(1), Is.Null);
            Assert.That(policy.SecondsUntilNextWave, Is.Null);
        }

        [Test]
        public void RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IndividualRespawn(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IndividualRespawn(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IndividualRespawn(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveRespawn(-1f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveRespawn(1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveRespawn(1f, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => RespawnPolicy.FromRules(null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => RespawnPolicy.FromRules(GameRules.Default, -1f));
        }

        // 部品だけを試すので、リスポーンを扱わないセッションを State の読み先にだけ使う
        private static ModeSession PlayingSession()
        {
            var session = new ModeSession(new TwoTeams(), 60f, new IScoreRule[0], null, new IWinCondition[0]);
            session.Start();
            return session;
        }
    }
}
