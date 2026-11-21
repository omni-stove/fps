using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class NetV2EmotePartTests
    {
        // 承認される前の MatchClient の自分の Id
        private const int _localId = 0;
        private const int _otherId = 4;

        private static readonly IReadOnlyList<EmoteSpec> _emotes = new[]
        {
            new EmoteSpec("wave", "emote.wave"),
            new EmoteSpec("dance", "emote.dance", loops: true),
        };

        private readonly List<string> _logs = new List<string>();
        private readonly List<EmoteRequestMessage> _sent = new List<EmoteRequestMessage>();
        private readonly List<Object> _created = new List<Object>();
        private readonly Dictionary<int, GameObject> _bodies = new Dictionary<int, GameObject>();
        private MatchClient _client;
        private NetV2EmotePart _part;
        private bool _held;
        private bool _sendResult;

        [SetUp]
        public void SetUp()
        {
            GameSignals.Emotes.Clear();
            KeySharing.Reset();
            _logs.Clear();
            _sent.Clear();
            _bodies.Clear();
            _held = false;
            _sendResult = true;
            _client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            _part = new NetV2EmotePart(_client, Send, _emotes, Catalog(), id => _bodies.TryGetValue(id, out var body) ? body : null, () => _held, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            _client.Dispose();
            GameSignals.Emotes.Clear();
            KeySharing.Reset();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void HoldingTheButtonOpensTheWheelAndReleasingClosesIt()
        {
            _part.Apply(Snapshot(Emote.On));
            _held = true;

            _part.Tick(0.1f);

            Assert.That(_part.IsWheelOpen, Is.True);
            Assert.That(GameSignals.Emotes.Value, Is.SameAs(_part.Control));
            Assert.That(GameSignals.Emotes.Value.EmoteIds, Is.EqualTo(new[] { "wave", "dance" }));
            Assert.That(KeySharing.IsClaimed(InputActionPaths.Emote), Is.True);

            _held = false;
            _part.Tick(0.1f);

            Assert.That(_part.IsWheelOpen, Is.False);
            Assert.That(GameSignals.Emotes.HasValue, Is.False);
        }

        [Test]
        public void WithTheRuleOffTheWheelIsNotOpenedAndTheKeyIsNotClaimed()
        {
            _part.Apply(Snapshot(Emote.Off));
            _held = true;

            _part.Tick(0.1f);

            Assert.That(_part.IsWheelOpen, Is.False);
            Assert.That(GameSignals.Emotes.HasValue, Is.False);
            Assert.That(KeySharing.IsClaimed(InputActionPaths.Emote), Is.False);
            Assert.That(_part.Control.EmoteIds, Is.Empty);
        }

        [Test]
        public void TheWheelClosesWhenThePlayerFalls()
        {
            _part.Apply(Snapshot(Emote.On));
            _held = true;
            _part.Tick(0.1f);

            _part.Apply(Snapshot(Emote.On, health: 0));

            Assert.That(_part.IsWheelOpen, Is.False);
            Assert.That(GameSignals.Emotes.HasValue, Is.False);
        }

        [Test]
        public void ChoosingFromTheWheelSendsAPlayRequest()
        {
            _part.Apply(Snapshot(Emote.On));

            Assert.That(_part.Control.Play("wave"), Is.True);

            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_sent[0].Action, Is.EqualTo(EmoteAction.Play));
            Assert.That(_sent[0].EmoteId, Is.EqualTo("wave"));
            Assert.That(_logs, Has.Member(EmoteLog.NetRequested("wave")));
        }

        [TestCase(Emote.Off, 100, "dance", "RuleOff")]
        [TestCase(Emote.On, 100, "backflip", "UnknownId")]
        [TestCase(Emote.On, 0, "dance", "Dead")]
        public void ARequestTheServerWouldRefuseIsNotSent(Emote rule, int health, string emoteId, string reason)
        {
            _part.Apply(Snapshot(rule, health));

            Assert.That(_part.Play(emoteId), Is.False);

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Member(EmoteLog.NetNotSent(emoteId, reason)));
        }

        [Test]
        public void AnIdTooLongToSendIsNotSent()
        {
            var longId = new string('a', MatchProtocol.MaxContentIdBytes + 1);
            _part.ApplyEmotes(new[] { new EmoteSpec(longId) });
            _part.Apply(Snapshot(Emote.On));

            Assert.That(_part.Play(longId), Is.False);

            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Member(EmoteLog.NetNotSent(longId, "IdTooLong")));
        }

        [Test]
        public void ARequestThatCannotBeSentReturnsFalse()
        {
            _part.Apply(Snapshot(Emote.On));
            _sendResult = false;

            Assert.That(_part.Play("wave"), Is.False);
            Assert.That(_logs, Has.Member(EmoteLog.NetNotSent("wave", "NotConnected")));
        }

        [Test]
        public void TheOwnEmoteIsKeptAsAnIdAndStopSendsAStopRequest()
        {
            _part.Apply(Snapshot(Emote.On));
            Assert.That(_part.Stop(), Is.False, "nothing to stop yet");

            Assert.That(_part.Handle(new EmoteStateMessage { PlayerId = _localId, Playing = true, EmoteId = "dance" }), Is.True);
            Assert.That(_part.LocalEmoteId, Is.EqualTo("dance"));

            Assert.That(_part.Stop(), Is.True);
            Assert.That(_sent[0].Action, Is.EqualTo(EmoteAction.Stop));

            _part.Handle(new EmoteStateMessage { PlayerId = _localId, Playing = false, StopReason = EmoteStopReason.Cancelled });
            Assert.That(_part.LocalEmoteId, Is.Null);
            Assert.That(_logs, Has.Member(EmoteLog.NetStopped(_localId, "dance", EmoteStopReason.Cancelled)));
        }

        [Test]
        public void AnotherPlayersEmotePlaysOnTheAnimatorOfTheirBody()
        {
            var animator = Body(_otherId, withAnimator: true);

            _part.Handle(new EmoteStateMessage { PlayerId = _otherId, Playing = true, EmoteId = "dance" });

            Assert.That(_part.TryGetShown(_otherId, out var id, out var animated), Is.True);
            Assert.That(id, Is.EqualTo("dance"));
            Assert.That(animated, Is.EqualTo(animator.isActiveAndEnabled), "an Animator without a controller may be inactive in Edit Mode");
            Assert.That(_logs, Has.Member(EmoteLog.NetPlayed(_otherId, "dance", animated)));
        }

        [Test]
        public void AStopFromTheServerStopsTheOtherPlayersEmote()
        {
            Body(_otherId, withAnimator: true);
            _part.Handle(new EmoteStateMessage { PlayerId = _otherId, Playing = true, EmoteId = "dance" });

            _part.Handle(new EmoteStateMessage { PlayerId = _otherId, Playing = false, StopReason = EmoteStopReason.Moved });

            Assert.That(_part.TryGetShown(_otherId, out _, out _), Is.False);
            Assert.That(_logs, Has.Member(EmoteLog.NetStopped(_otherId, "dance", EmoteStopReason.Moved)));
        }

        [Test]
        public void ANewEmoteReplacesTheShownOne()
        {
            Body(_otherId, withAnimator: true);
            _part.Handle(new EmoteStateMessage { PlayerId = _otherId, Playing = true, EmoteId = "dance" });

            _part.Handle(new EmoteStateMessage { PlayerId = _otherId, Playing = true, EmoteId = "wave" });

            Assert.That(_part.TryGetShown(_otherId, out var id, out _), Is.True);
            Assert.That(id, Is.EqualTo("wave"));
        }

        [Test]
        public void AnEmoteWithoutAClipIsKeptWithoutAnimation()
        {
            Body(_otherId, withAnimator: true);
            _part.ApplyEmotes(new[] { new EmoteSpec("shrug") });

            _part.Handle(new EmoteStateMessage { PlayerId = _otherId, Playing = true, EmoteId = "shrug" });

            Assert.That(_part.TryGetShown(_otherId, out _, out var animated), Is.True);
            Assert.That(animated, Is.False);
            Assert.That(_logs, Has.Member(EmoteLog.NoAnimation("shrug")));
        }

        [Test]
        public void AnEmoteOfAPlayerOutOfViewIsShownWhenTheirBodyAppears()
        {
            _part.Handle(new EmoteStateMessage { PlayerId = _otherId, Playing = true, EmoteId = "dance" });
            Assert.That(_logs, Has.Member(EmoteLog.NetNoBody(_otherId, "dance")));

            Body(_otherId, withAnimator: false);
            _part.Tick(0.1f);
            _part.Tick(0.1f);

            Assert.That(_part.TryGetShown(_otherId, out var id, out _), Is.True);
            Assert.That(id, Is.EqualTo("dance"));
            Assert.That(_logs.FindAll(l => l == EmoteLog.NetNoBody(_otherId, "dance")), Has.Count.EqualTo(1), "the body is looked up again only when it changes");
        }

        [Test]
        public void OtherMessagesAreLeftForTheOtherParts()
        {
            Assert.That(_part.Handle(new ChatMessage()), Is.False);
        }

        [Test]
        public void DisposingClosesTheWheelAndForgetsTheShownEmotes()
        {
            _part.Apply(Snapshot(Emote.On));
            _held = true;
            _part.Tick(0.1f);
            Body(_otherId, withAnimator: true);
            _part.Handle(new EmoteStateMessage { PlayerId = _otherId, Playing = true, EmoteId = "dance" });

            _part.Dispose();

            Assert.That(GameSignals.Emotes.HasValue, Is.False);
            Assert.That(KeySharing.IsClaimed(InputActionPaths.Emote), Is.False);
            Assert.That(_part.TryGetShown(_otherId, out _, out _), Is.False);
            Assert.That(_part.Play("wave"), Is.False);
        }

        [Test]
        public void TheArgumentsAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2EmotePart(null, Send, _emotes));
            Assert.Throws<ArgumentNullException>(() => new NetV2EmotePart(_client, null, _emotes));
            Assert.Throws<ArgumentException>(() => new NetV2EmotePart(_client, Send, new[] { new EmoteSpec("wave"), new EmoteSpec("wave") }));
        }

        private EmoteCatalog Catalog()
        {
            var catalog = ScriptableObject.CreateInstance<EmoteCatalog>();
            _created.Add(catalog);
            foreach (var id in new[] { "wave", "dance" })
            {
                var clip = new AnimationClip { name = id + "Clip" };
                _created.Add(clip);
                catalog.Add(id, clip);
            }

            return catalog;
        }

        private Animator Body(int playerId, bool withAnimator)
        {
            var root = new GameObject("NetV2Remote" + playerId);
            _created.Add(root);
            _bodies[playerId] = root;
            if (!withAnimator)
            {
                return null;
            }

            var child = new GameObject("Body");
            child.transform.SetParent(root.transform, false);
            return child.AddComponent<Animator>();
        }

        private static SnapshotMessage Snapshot(Emote rule, int health = 100)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = GameModeState.Playing;
            snapshot.Match.Rules = MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithEmote(rule)));
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _localId;
            own.Team = Team.Alpha;
            own.Health = health;
            return snapshot;
        }

        private bool Send(IMessage message)
        {
            var request = (EmoteRequestMessage)message;
            _sent.Add(new EmoteRequestMessage { Action = request.Action, EmoteId = request.EmoteId });
            return _sendResult;
        }

        private sealed class SilentTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port)
            {
                throw new InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
            {
            }

            public int GetMaxPayloadSize(TransportChannel channel)
            {
                return MessageWriter.DefaultMaxLength;
            }

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
