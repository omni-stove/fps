using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientVoiceTests
    {
        private const string _lobby = "match-lobby";
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _ally = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _enemy = "cccccccccccccccccccccccccccccccc";
        private const string _stranger = "dddddddddddddddddddddddddddddddd";
        private const int _selfId = 1;
        private const int _allyId = 2;
        private const int _enemyId = 3;

        private FakeVoiceSession _session;
        private VoiceRoster _roster;
        private ClientVoice _voice;
        private List<VoiceStatus?> _statuses;

        [SetUp]
        public void SetUp()
        {
            _session = new FakeVoiceSession();
            _roster = new VoiceRoster();
            _roster.Set(_self, _selfId);
            _roster.Set(_ally, _allyId);
            _roster.Set(_enemy, _enemyId);
            _voice = new ClientVoice(_session, _roster);
            _statuses = new List<VoiceStatus?>();
            _voice.StatusChanged += _statuses.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _voice.Dispose();
        }

        private static SnapshotMessage Snapshot(params (int Id, Team Team)[] players)
        {
            var snapshot = new SnapshotMessage();
            foreach (var (id, team) in players)
            {
                ref var player = ref snapshot.Players.Add();
                player.PlayerId = id;
                player.Team = team;
            }

            return snapshot;
        }

        private static SnapshotMessage StartingTeams()
        {
            return Snapshot((_selfId, Team.Alpha), (_allyId, Team.Alpha), (_enemyId, Team.Bravo));
        }

        private void EnterAndConnect()
        {
            _voice.Enter(_lobby);
            _session.AnswerJoin(null);
            _session.Connect();
        }

        [Test]
        public void NothingIsShownBeforeEnteringTheRoom()
        {
            Assert.That(_voice.Status, Is.Null);
            Assert.That(_session.JoinedLobby, Is.Null);
            Assert.That(_statuses, Is.Empty);
        }

        [Test]
        public void EnteringJoinsTheLobbyRoomAndShowsTheStatus()
        {
            _voice.Enter(_lobby);

            Assert.That(_session.JoinedLobby, Is.EqualTo(_lobby));
            Assert.That(_voice.IsEntered, Is.True);
            Assert.That(_voice.Status, Is.EqualTo(new VoiceStatus(false, Array.Empty<int>(), true)));
        }

        [Test]
        public void EnteringTheSameLobbyAgainDoesNothing()
        {
            _voice.Enter(_lobby);
            _voice.Enter(_lobby);

            Assert.That(_session.JoinCount, Is.EqualTo(1));
            Assert.That(_session.LeaveCount, Is.EqualTo(0));
        }

        [Test]
        public void AFailedJoinLeavesTheRoomAndClearsTheStatus()
        {
            _voice.Enter(_lobby);

            _session.AnswerJoin("not logged in to EOS");

            Assert.That(_voice.IsEntered, Is.False);
            Assert.That(_voice.Status, Is.Null);
            Assert.That(_statuses[_statuses.Count - 1], Is.Null);
        }

        [Test]
        public void LeavingLeavesTheRoomAndClearsTheStatus()
        {
            EnterAndConnect();

            _voice.Leave();

            Assert.That(_session.LeaveCount, Is.EqualTo(1));
            Assert.That(_session.State, Is.EqualTo(VoiceSessionState.Idle));
            Assert.That(_voice.Status, Is.Null);
        }

        [Test]
        public void SendsOnlyWhileTalking()
        {
            EnterAndConnect();
            Assert.That(_session.WantsSending, Is.False, "not sending before pressing");

            _voice.SetTalking(true);
            Assert.That(_session.WantsSending, Is.True, "sending while pressed");
            Assert.That(_voice.Status.Value.IsLocalSpeaking, Is.True);

            _voice.SetTalking(false);
            Assert.That(_session.WantsSending, Is.False, "not sending after releasing");
            Assert.That(_voice.Status.Value.IsLocalSpeaking, Is.False);
            Assert.That(_session.SendingRequests, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void HoldingTheButtonOutsideTheRoomSendsNothing()
        {
            _voice.SetTalking(true);

            Assert.That(_session.WantsSending, Is.False);
            Assert.That(_session.SendingRequests, Is.Empty);

            _voice.Enter(_lobby);

            Assert.That(_session.WantsSending, Is.True, "starts sending once in the room while still pressed");
        }

        [Test]
        public void LeavingStopsSending()
        {
            EnterAndConnect();
            _voice.SetTalking(true);

            _voice.Leave();

            Assert.That(_session.WantsSending, Is.False);
        }

        [Test]
        public void WithTheMicrophoneOffTalkingSendsNothing()
        {
            EnterAndConnect();
            _voice.ApplySettings(new VoiceSettings(false, 100));

            _voice.SetTalking(true);

            Assert.That(_session.WantsSending, Is.False);
            Assert.That(_voice.Status, Is.EqualTo(new VoiceStatus(false, Array.Empty<int>(), false)));
        }

        [Test]
        public void TurningTheMicrophoneOffWhileTalkingStopsSending()
        {
            EnterAndConnect();
            _voice.SetTalking(true);

            _voice.ApplySettings(new VoiceSettings(false, 100));

            Assert.That(_session.WantsSending, Is.False);
            Assert.That(_voice.Status.Value.IsLocalSpeaking, Is.False);
            Assert.That(_voice.Status.Value.MicrophoneAvailable, Is.False);
        }

        [Test]
        public void TheVolumeComesFromTheSettings()
        {
            Assert.That(_session.Volume, Is.EqualTo(1f), "the default before any settings");

            _voice.ApplySettings(new VoiceSettings(true, 150));

            Assert.That(_session.Volume, Is.EqualTo(1.5f));
        }

        [Test]
        public void EnemiesAreNotReceivedAndAlliesAre()
        {
            EnterAndConnect();

            _voice.Apply(StartingTeams(), _selfId);

            Assert.That(_session.WantsReceiving(_ally), Is.True);
            Assert.That(_session.WantsReceiving(_enemy), Is.False);
            Assert.That(_session.WantsReceiving(_self), Is.True, "never mutes the local player");
        }

        [Test]
        public void ReceivingIsDecidedBeforeEnteringTheRoom()
        {
            _voice.Apply(StartingTeams(), _selfId);

            Assert.That(_session.WantsReceiving(_enemy), Is.False);
            Assert.That(_session.WantsReceiving(_ally), Is.True);
        }

        [Test]
        public void OnlyAlliesAreShownAsSpeaking()
        {
            EnterAndConnect();
            _voice.Apply(StartingTeams(), _selfId);

            _session.Speak(_enemy, true);
            _session.Speak(_ally, true);
            _session.Speak(_self, true);

            Assert.That(_voice.Status.Value.SpeakingIds, Is.EqualTo(new[] { _allyId }));
        }

        [Test]
        public void ATeamChangeFixesReceiving()
        {
            EnterAndConnect();
            _voice.Apply(StartingTeams(), _selfId);
            _session.Speak(_ally, true);
            _session.Speak(_enemy, true);
            Assert.That(_voice.Status.Value.SpeakingIds, Is.EqualTo(new[] { _allyId }));

            _voice.Apply(Snapshot((_selfId, Team.Alpha), (_allyId, Team.Bravo), (_enemyId, Team.Alpha)), _selfId);

            Assert.That(_session.WantsReceiving(_ally), Is.False, "the old ally is now an enemy");
            Assert.That(_session.WantsReceiving(_enemy), Is.True, "the old enemy is now an ally");
            Assert.That(_voice.Status.Value.SpeakingIds, Is.EqualTo(new[] { _enemyId }));
        }

        [Test]
        public void ChangingTheLocalTeamFixesReceiving()
        {
            EnterAndConnect();
            _voice.Apply(StartingTeams(), _selfId);

            _voice.Apply(Snapshot((_selfId, Team.Bravo), (_allyId, Team.Alpha), (_enemyId, Team.Bravo)), _selfId);

            Assert.That(_session.WantsReceiving(_ally), Is.False);
            Assert.That(_session.WantsReceiving(_enemy), Is.True);
        }

        [Test]
        public void ReceivingIsOnlyRequestedWhenItChanges()
        {
            EnterAndConnect();
            _voice.Apply(StartingTeams(), _selfId);
            var requests = _session.ReceivingRequests.Count;

            _voice.Apply(StartingTeams(), _selfId);
            _voice.Apply(StartingTeams(), _selfId);

            Assert.That(_session.ReceivingRequests.Count, Is.EqualTo(requests));
        }

        [Test]
        public void APlayerWhoLeftTheMatchIsNotReceived()
        {
            EnterAndConnect();
            _voice.Apply(StartingTeams(), _selfId);

            _voice.Apply(Snapshot((_selfId, Team.Alpha), (_enemyId, Team.Bravo)), _selfId);

            Assert.That(_session.WantsReceiving(_ally), Is.False);
        }

        [Test]
        public void NobodyElseIsReceivedWhileTheLocalTeamIsUnknown()
        {
            EnterAndConnect();

            _voice.Apply(Snapshot((_allyId, Team.Alpha), (_enemyId, Team.Bravo)), _selfId);

            Assert.That(_session.WantsReceiving(_ally), Is.False);
            Assert.That(_session.WantsReceiving(_enemy), Is.False);
        }

        [Test]
        public void ASpeakerWithoutAPlayerIsMutedUntilTheRosterKnowsThem()
        {
            EnterAndConnect();
            _voice.Apply(Snapshot((_selfId, Team.Alpha), (_allyId, Team.Alpha), (4, Team.Alpha)), _selfId);

            _session.Speak(_stranger, true);

            Assert.That(_session.WantsReceiving(_stranger), Is.False);
            Assert.That(_session.Speakers, Is.Empty);
            Assert.That(_voice.Status.Value.SpeakingIds, Is.Empty);

            _roster.Set(_stranger, 4);

            Assert.That(_session.WantsReceiving(_stranger), Is.True);
            Assert.That(_voice.Status.Value.SpeakingIds, Is.EqualTo(new[] { 4 }));
        }

        [Test]
        public void RemovingSomeoneFromTheRosterMutesThem()
        {
            EnterAndConnect();
            _voice.Apply(StartingTeams(), _selfId);

            _roster.Remove(_ally);

            Assert.That(_session.WantsReceiving(_ally), Is.False);
        }

        [Test]
        public void TheStatusIsOnlyRepublishedWhenItChanges()
        {
            EnterAndConnect();
            var count = _statuses.Count;

            _voice.Tick();
            _voice.Tick();

            Assert.That(_statuses.Count, Is.EqualTo(count));
            Assert.That(_session.TickCount, Is.EqualTo(2));
        }

        [Test]
        public void DisposingDisposesTheSessionAndClearsTheStatus()
        {
            EnterAndConnect();

            _voice.Dispose();

            Assert.That(_session.IsDisposed, Is.True);
            Assert.That(_statuses[_statuses.Count - 1], Is.Null);
            Assert.Throws<ObjectDisposedException>(() => _voice.Enter(_lobby));
        }

        /// <summary>
        /// 偽の <see cref="IVoiceSession"/>。送る・受ける・音量の望む値を覚え、つながっていれば送るものとする。
        /// 話している人は、受けるのを切った人を除いて <see cref="Speakers"/> に出す (<see cref="VoiceSession"/> と同じ)。
        /// </summary>
        private sealed class FakeVoiceSession : IVoiceSession
        {
            private readonly HashSet<string> _muted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly List<string> _speaking = new List<string>();
            private Action<string> _joinDone;
            private IReadOnlyList<string> _speakers = Array.Empty<string>();

            public event Action<IReadOnlyList<string>> SpeakersChanged;

            public event Action<VoiceSessionState> StateChanged;

            public VoiceSessionState State { get; private set; }

            public string LobbyId { get; private set; }

            public IReadOnlyList<string> Speakers => _speakers;

            public bool WantsSending { get; private set; }

            public bool IsSending => State == VoiceSessionState.Connected && WantsSending;

            public string JoinedLobby { get; private set; }

            public int JoinCount { get; private set; }

            public int LeaveCount { get; private set; }

            public int TickCount { get; private set; }

            public float Volume { get; private set; } = VoiceSession.DefaultVolume;

            public bool IsDisposed { get; private set; }

            public List<bool> SendingRequests { get; } = new List<bool>();

            public List<KeyValuePair<string, bool>> ReceivingRequests { get; } = new List<KeyValuePair<string, bool>>();

            public bool WantsReceiving(string participantId) => !_muted.Contains(participantId);

            public void Tick()
            {
                TickCount++;
            }

            public void Join(string lobbyId, Action<string> done)
            {
                if (State != VoiceSessionState.Idle)
                {
                    throw new InvalidOperationException("already in a room");
                }

                JoinCount++;
                JoinedLobby = lobbyId;
                LobbyId = lobbyId;
                _joinDone = done;
                SetState(VoiceSessionState.Joining);
            }

            public void AnswerJoin(string error)
            {
                var done = _joinDone;
                _joinDone = null;
                if (error != null)
                {
                    LobbyId = null;
                    SetState(VoiceSessionState.Idle);
                }
                else
                {
                    SetState(VoiceSessionState.Connecting);
                }

                done(error);
            }

            public void Connect()
            {
                SetState(VoiceSessionState.Connected);
            }

            public void Leave()
            {
                if (State == VoiceSessionState.Idle)
                {
                    return;
                }

                LeaveCount++;
                _joinDone = null;
                LobbyId = null;
                _speaking.Clear();
                SetState(VoiceSessionState.Idle);
                PublishSpeakers();
            }

            public void Speak(string participantId, bool speaking)
            {
                if (speaking)
                {
                    _speaking.Add(participantId);
                }
                else
                {
                    _speaking.Remove(participantId);
                }

                PublishSpeakers();
            }

            public void SetSending(bool sending)
            {
                SendingRequests.Add(sending);
                WantsSending = sending;
            }

            public void SetReceiving(string participantId, bool receiving)
            {
                ReceivingRequests.Add(new KeyValuePair<string, bool>(participantId, receiving));
                if (receiving)
                {
                    _muted.Remove(participantId);
                }
                else
                {
                    _muted.Add(participantId);
                }

                PublishSpeakers();
            }

            public void SetVolume(float volume)
            {
                Volume = volume;
            }

            public void Dispose()
            {
                Leave();
                IsDisposed = true;
            }

            private void PublishSpeakers()
            {
                var speakers = new List<string>();
                foreach (var participant in _speaking)
                {
                    if (!_muted.Contains(participant))
                    {
                        speakers.Add(participant);
                    }
                }

                speakers.Sort(StringComparer.Ordinal);
                if (Same(speakers, _speakers))
                {
                    return;
                }

                _speakers = speakers.ToArray();
                SpeakersChanged?.Invoke(_speakers);
            }

            private static bool Same(List<string> a, IReadOnlyList<string> b)
            {
                if (a.Count != b.Count)
                {
                    return false;
                }

                for (var i = 0; i < a.Count; i++)
                {
                    if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }

            private void SetState(VoiceSessionState state)
            {
                if (State == state)
                {
                    return;
                }

                State = state;
                StateChanged?.Invoke(state);
            }
        }
    }
}
