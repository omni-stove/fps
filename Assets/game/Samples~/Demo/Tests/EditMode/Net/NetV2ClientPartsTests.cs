using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    public sealed class NetV2ClientPartsTests
    {
        private readonly List<string> _logs = new List<string>();
        private readonly List<string> _decided = new List<string>();
        private MatchClient _client;
        private List<INetV2ClientPart> _parts;

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
            _decided.Clear();
            _client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            if (_parts != null)
            {
                foreach (var part in _parts)
                {
                    part.Dispose();
                }
            }

            _client.Dispose();
            GameSignals.MapVote.Clear();
            GameSignals.MapVoteSender.Clear();
            GameSignals.Chat.Clear();
            GameSignals.ChatSender.Clear();
            GameSignals.Minimap.Clear();
            GameSignals.Spectate.Clear();
            GameSignals.Voice.Clear();
        }

        [Test]
        public void WithoutVoiceTheListHasTheMinimapChatMapVoteAndSpectateParts()
        {
            _parts = NetV2ClientParts.Create(Context(null));

            Assert.That(_parts.Select(p => p.GetType()), Is.EqualTo(new[]
            {
                typeof(NetV2MinimapPart),
                typeof(NetV2ChatPart),
                typeof(NetV2MapVotePart),
                typeof(NetV2SpectatePart),
            }));
        }

        [Test]
        public void WithAVoiceSessionTheVoicePartComesLast()
        {
            var voice = new FakeVoiceSession();

            _parts = NetV2ClientParts.Create(Context(voice));

            Assert.That(_parts.Select(p => p.GetType()), Is.EqualTo(new[]
            {
                typeof(NetV2MinimapPart),
                typeof(NetV2ChatPart),
                typeof(NetV2MapVotePart),
                typeof(NetV2SpectatePart),
                typeof(NetV2VoicePart),
            }));
            Assert.That(((NetV2VoicePart)_parts[4]).Voice, Is.Not.Null);
        }

        [Test]
        public void DisposingTheVoicePartClosesTheSession()
        {
            var voice = new FakeVoiceSession();
            _parts = NetV2ClientParts.Create(Context(voice));

            _parts[4].Dispose();

            Assert.That(voice.Disposed, Is.True);
        }

        [Test]
        public void TheMapVoteInTheListTellsTheDecidedMap()
        {
            _parts = NetV2ClientParts.Create(Context(null));
            var state = new MapVoteStateMessage { RemainingSeconds = 0f };
            state.AddCandidate("NetArena02", 0);
            state.AddCandidate("NetArena03", 2);
            state.ChosenIndex = 1;

            var handled = _parts.Any(part => part.Handle(state));

            Assert.That(handled, Is.True);
            Assert.That(_decided, Is.EqualTo(new[] { "NetArena03" }));
        }

        [Test]
        public void ChatMessagesAreTakenByTheChatPart()
        {
            _parts = NetV2ClientParts.Create(Context(null));

            var taker = _parts.FirstOrDefault(part => part.Handle(new ChatMessage { SenderId = 2, Text = "hi" }));

            Assert.That(taker, Is.InstanceOf<NetV2ChatPart>());
        }

        [Test]
        public void RefusesANullContextAndMissingPieces()
        {
            Assert.Throws<ArgumentNullException>(() => NetV2ClientParts.Create(null));
            Assert.Throws<ArgumentNullException>(() => new NetV2ClientPartsContext(null, _ => true, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new NetV2ClientPartsContext(_client, null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new NetV2ClientPartsContext(_client, _ => true, null));
        }

        private NetV2ClientPartsContext Context(IVoiceSession voice)
        {
            return new NetV2ClientPartsContext(_client, _ => true, _logs.Add)
            {
                MapDecided = _decided.Add,
                LocalPlayer = () => null,
                Voice = voice,
                VoiceSettings = new VoiceSettingsStore("Shooter.Tests.NetV2ClientParts.Voice"),
                PushToTalkHeld = () => false,
            };
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

        private sealed class FakeVoiceSession : IVoiceSession
        {
            public event Action<IReadOnlyList<string>> SpeakersChanged
            {
                add { }
                remove { }
            }

            public event Action<VoiceSessionState> StateChanged
            {
                add { }
                remove { }
            }

            public VoiceSessionState State => default;

            public string LobbyId => null;

            public IReadOnlyList<string> Speakers => Array.Empty<string>();

            public bool WantsSending { get; private set; }

            public bool IsSending => WantsSending;

            public bool Disposed { get; private set; }

            public void Tick()
            {
            }

            public void Join(string lobbyId, Action<string> done)
            {
            }

            public void Leave()
            {
            }

            public void SetSending(bool sending)
            {
                WantsSending = sending;
            }

            public void SetReceiving(string participantId, bool receiving)
            {
            }

            public void SetVolume(float volume)
            {
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
