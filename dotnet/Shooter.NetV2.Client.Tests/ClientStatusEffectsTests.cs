using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientStatusEffectsTests
    {
        private static readonly string[] _startedOnly = { "start 2 burn" };
        private static readonly string[] _startedThenEnded = { "start 2 burn", "end 2 burn" };
        private static readonly string[] _endedByServer = { "start 3 burn", "end 3 burn" };
        private static readonly string[] _firstForgotten = { "end 1 burn" };
        private static readonly string[] _allCleared = { "end 1 burn", "end 2 burn" };

        private readonly List<string> _events = new List<string>();
        private ClientStatusEffects _effects;

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            _effects = new ClientStatusEffects();
            _effects.Started += (target, kind) => _events.Add("start " + target + " " + kind);
            _effects.Ended += (target, kind) => _events.Add("end " + target + " " + kind);
        }

        [Test]
        public void AnEffectStartsOnceAndExtendingDoesNotStartItAgain()
        {
            _effects.Receive(Message(2, "burn", 4f));
            _effects.Tick(1f);
            _effects.Receive(Message(2, "burn", 4f));

            Assert.That(_events, Is.EqualTo(_startedOnly));
            Assert.That(_effects.RemainingSeconds(2, "burn"), Is.EqualTo(4f));
        }

        [Test]
        public void ItCountsDownAndEndsOnItsOwn()
        {
            _effects.Receive(Message(2, "burn", 1.5f));

            _effects.Tick(1f);
            Assert.That(_effects.RemainingSeconds(2, "burn"), Is.EqualTo(0.5f).Within(1e-5f));

            _effects.Tick(1f);
            Assert.That(_effects.Count, Is.Zero);
            Assert.That(_events, Is.EqualTo(_startedThenEnded));

            _effects.Receive(Message(2, "burn", 0f));
            Assert.That(_events, Has.Count.EqualTo(2), "the server's late end changes nothing");
        }

        [Test]
        public void TheServersEndStopsItEarly()
        {
            _effects.Receive(Message(3, "burn", 4f));

            _effects.Receive(Message(3, "burn", 0f));

            Assert.That(_effects.RemainingSeconds(3, "burn"), Is.Zero);
            Assert.That(_events, Is.EqualTo(_endedByServer));
        }

        [Test]
        public void TheHudReadsTheLongestEffectOnThePlayer()
        {
            _effects.Receive(Message(1, "burn", 2f));
            _effects.Receive(Message(1, "blind", 3f));
            _effects.Receive(Message(2, "burn", 9f));

            Assert.That(_effects.TryGetLongest(1, out var kind, out var remaining), Is.True);
            Assert.That(kind, Is.EqualTo("blind"));
            Assert.That(remaining, Is.EqualTo(3f));
            Assert.That(_effects.TryGetLongest(5, out _, out _), Is.False);
        }

        [Test]
        public void ForgettingAPlayerEndsOnlyTheirEffects()
        {
            _effects.Receive(Message(1, "burn", 2f));
            _effects.Receive(Message(2, "burn", 2f));
            _events.Clear();

            _effects.Forget(1);

            Assert.That(_events, Is.EqualTo(_firstForgotten));
            Assert.That(_effects.RemainingSeconds(2, "burn"), Is.EqualTo(2f));

            _effects.Clear();
            Assert.That(_events, Is.EqualTo(_allCleared));
        }

        [Test]
        public void AMessageWithoutAKindIsIgnored()
        {
            _effects.Receive(Message(1, "", 2f));

            Assert.That(_effects.Count, Is.Zero);
            Assert.That(_events, Is.Empty);
        }

        private static StatusEffectMessage Message(int target, string kind, float remaining)
        {
            return new StatusEffectMessage { TargetId = target, KindId = kind, RemainingSeconds = remaining };
        }
    }
}
