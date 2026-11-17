using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.NetV2.Tests
{
    public sealed class VoiceSessionTests
    {
        private const string _lobby = "match-lobby";
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _ally = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _enemy = "cccccccccccccccccccccccccccccccc";

        private FakeVoiceRtc _rtc;
        private List<string> _log;
        private List<string[]> _speakerChanges;
        private List<VoiceSessionState> _states;
        private VoiceSession _session;

        [SetUp]
        public void SetUp()
        {
            _rtc = new FakeVoiceRtc();
            _log = new List<string>();
            _speakerChanges = new List<string[]>();
            _states = new List<VoiceSessionState>();
            _session = new VoiceSession(_rtc, _log.Add);
            _session.SpeakersChanged += speakers => _speakerChanges.Add(ToArray(speakers));
            _session.StateChanged += _states.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _session.Dispose();
        }

        /// <summary>部屋に入ってつなぎ、つないだときの送る (false) と音量 (1) の要求に応える。</summary>
        private void Connect()
        {
            var joinError = "not answered";
            _session.Join(_lobby, error => joinError = error);
            _rtc.AnswerJoin(null);
            Assert.That(joinError, Is.Null);
            _rtc.Connect();
            _rtc.AnswerSending(null);
            _rtc.AnswerVolume(null);
        }

        [Test]
        public void JoiningWaitsForTheRoomAndThenSendsNothingAtTheDefaultVolume()
        {
            var joinError = "not answered";
            _session.Join(_lobby, error => joinError = error);

            Assert.That(_rtc.JoinedLobby, Is.EqualTo(_lobby));
            Assert.That(_session.State, Is.EqualTo(VoiceSessionState.Joining));
            Assert.That(_session.LobbyId, Is.EqualTo(_lobby));
            Assert.That(_log, Is.EqualTo(Names("[Voice] joining lobby=match-lobby")));

            _rtc.AnswerJoin(null);
            Assert.That(joinError, Is.Null);
            Assert.That(_session.State, Is.EqualTo(VoiceSessionState.Connecting));
            Assert.That(_rtc.Sending, Is.Empty, "nothing is requested before the room connects");

            _rtc.Connect();
            Assert.That(_session.State, Is.EqualTo(VoiceSessionState.Connected));
            Assert.That(_rtc.SendingValues(), Is.EqualTo(Flags(false)));
            Assert.That(_rtc.VolumeValues(), Is.EqualTo(Levels(1f)));
            Assert.That(_log, Has.Member("[Voice] connected lobby=match-lobby"));
            Assert.That(_states, Is.EqualTo(States(VoiceSessionState.Joining, VoiceSessionState.Connecting, VoiceSessionState.Connected)));
        }

        [Test]
        public void PushToTalkSendsOnlyWhileHeld()
        {
            Connect();
            Assert.That(_session.IsSending, Is.False);

            _session.SetSending(true);
            Assert.That(_session.WantsSending, Is.True);
            Assert.That(_rtc.SendingValues(), Is.EqualTo(Flags(true)));
            Assert.That(_session.IsSending, Is.False, "not sending until the service accepts it");
            _rtc.AnswerSending(null);
            Assert.That(_session.IsSending, Is.True);

            _session.SetSending(false);
            Assert.That(_session.WantsSending, Is.False);
            Assert.That(_rtc.SendingValues(), Is.EqualTo(Flags(false)));
            _rtc.AnswerSending(null);
            Assert.That(_session.IsSending, Is.False);
            Assert.That(_rtc.Sending, Is.Empty);
        }

        [Test]
        public void TogglingWhileARequestIsPendingSendsTheLatestWishAfterTheReply()
        {
            Connect();

            _session.SetSending(true);
            _session.SetSending(false);
            _session.SetSending(true);
            Assert.That(_rtc.Sending, Has.Count.EqualTo(1), "one request at a time");
            _rtc.AnswerSending(null);
            Assert.That(_rtc.Sending, Is.Empty, "the wish already matches what the service accepted");
            Assert.That(_session.IsSending, Is.True);

            _session.SetSending(false);
            _session.SetSending(true);
            _rtc.AnswerSending(null);
            Assert.That(_session.IsSending, Is.False);
            Assert.That(_rtc.SendingValues(), Is.EqualTo(Flags(true)), "released and pressed again while waiting");
            _rtc.AnswerSending(null);
            Assert.That(_session.IsSending, Is.True);
        }

        [Test]
        public void SendingChosenBeforeJoiningIsAppliedWhenTheRoomConnects()
        {
            _session.SetSending(true);
            _session.SetVolume(0.5f);
            Assert.That(_rtc.Sending, Is.Empty);

            _session.Join(_lobby, _ => { });
            _rtc.AnswerJoin(null);
            _rtc.Connect();

            Assert.That(_rtc.SendingValues(), Is.EqualTo(Flags(true)));
            Assert.That(_rtc.VolumeValues(), Is.EqualTo(Levels(0.5f)));
        }

        [Test]
        public void ARefusedRequestIsLoggedAndTriedAgainOnlyWhenTheWishChanges()
        {
            Connect();

            _session.SetSending(true);
            _rtc.AnswerSending("TooManyRequests");
            Assert.That(_session.IsSending, Is.False);
            Assert.That(_rtc.Sending, Is.Empty, "a refused value is not requested again by itself");
            Assert.That(_log, Has.Member("[Voice] sending=true failed reason=TooManyRequests"));

            _session.SetSending(false);
            Assert.That(_rtc.Sending, Is.Empty, "the service already does not send");

            _session.SetSending(true);
            Assert.That(_rtc.SendingValues(), Is.EqualTo(Flags(true)));
        }

        [Test]
        public void ParticipantsCutBeforeTheyJoinAreCutWhenTheyJoinAndNotReportedAsSpeaking()
        {
            _session.SetReceiving(_enemy, false);
            Connect();

            _rtc.Enter(_ally);
            _rtc.Enter(_enemy);
            Assert.That(_rtc.ReceivingRequests(), Is.EqualTo(Names(_enemy + "=false")), "the service starts receiving everyone");

            _rtc.Speak(_enemy, true);
            Assert.That(_speakerChanges, Is.Empty);

            _rtc.Speak(_ally, true);
            Assert.That(_speakerChanges, Has.Count.EqualTo(1));
            Assert.That(_speakerChanges[0], Is.EqualTo(Names(_ally)));
            Assert.That(_session.Speakers, Is.EqualTo(Names(_ally)));
            Assert.That(_session.WantsReceiving(_enemy), Is.False);
            Assert.That(_session.WantsReceiving(_ally), Is.True);
        }

        [Test]
        public void CuttingSomeoneWhoIsSpeakingStopsReportingThem()
        {
            Connect();
            _rtc.Enter(_enemy);
            _rtc.Speak(_enemy, true);
            Assert.That(_session.Speakers, Is.EqualTo(Names(_enemy)));

            _session.SetReceiving(_enemy, false);

            Assert.That(_rtc.ReceivingRequests(), Is.EqualTo(Names(_enemy + "=false")));
            Assert.That(_session.Speakers, Is.Empty);
            Assert.That(_speakerChanges, Has.Count.EqualTo(2));
            Assert.That(_speakerChanges[1], Is.Empty);

            _rtc.AnswerReceiving("NotFound");
            Assert.That(_log, Has.Member("[Voice] receiving=false participant=" + _enemy + " failed reason=NotFound"));

            _session.SetReceiving(_enemy, true);
            Assert.That(_rtc.Receiving, Is.Empty, "the service never stopped receiving them");
            Assert.That(_session.Speakers, Is.EqualTo(Names(_enemy)));
        }

        [Test]
        public void SpeakersAreReportedOnlyWhenTheyChangeAndSortedById()
        {
            Connect();
            _rtc.Enter(_enemy);
            _rtc.Enter(_ally);

            _rtc.Speak(_enemy, true);
            _rtc.Speak(_enemy, true);
            _rtc.Speak(_self, true);
            _rtc.Speak(_ally, true);
            _rtc.Speak(_ally, false);
            _rtc.Exit(_enemy);
            _rtc.Speak(_enemy, false);

            Assert.That(_speakerChanges, Has.Count.EqualTo(5));
            Assert.That(_speakerChanges[0], Is.EqualTo(Names(_enemy)));
            Assert.That(_speakerChanges[1], Is.EqualTo(Names(_self, _enemy)));
            Assert.That(_speakerChanges[2], Is.EqualTo(Names(_self, _ally, _enemy)));
            Assert.That(_speakerChanges[3], Is.EqualTo(Names(_self, _enemy)));
            Assert.That(_speakerChanges[4], Is.EqualTo(Names(_self)), "a participant who leaves stops speaking");
        }

        [Test]
        public void ALostConnectionForgetsTheRoomAndReappliesTheWishesWhenItComesBack()
        {
            _session.SetReceiving(_enemy, false);
            Connect();
            _session.SetSending(true);
            _rtc.Enter(_ally);
            _rtc.Speak(_ally, true);

            _rtc.Drop("NoConnection");

            Assert.That(_session.State, Is.EqualTo(VoiceSessionState.Connecting));
            Assert.That(_session.IsSending, Is.False);
            Assert.That(_session.Speakers, Is.Empty);
            Assert.That(_log, Has.Member("[Voice] connection lost lobby=match-lobby reason=NoConnection"));

            _rtc.AnswerSending(null);
            Assert.That(_session.IsSending, Is.False, "a reply from before the drop is ignored");

            _rtc.Connect();
            _rtc.Enter(_enemy);
            Assert.That(_rtc.SendingValues(), Is.EqualTo(Flags(true)));
            Assert.That(_rtc.VolumeValues(), Is.EqualTo(Levels(1f)));
            Assert.That(_rtc.ReceivingRequests(), Is.EqualTo(Names(_enemy + "=false")));
            _rtc.AnswerSending(null);
            Assert.That(_session.IsSending, Is.True);
        }

        [Test]
        public void LeavingIgnoresLateRepliesAndKeepsTheWishesForTheNextRoom()
        {
            Connect();
            _session.SetSending(true);
            _rtc.Enter(_ally);
            _rtc.Speak(_ally, true);

            _session.Leave();

            Assert.That(_rtc.Leaves, Is.EqualTo(1));
            Assert.That(_session.State, Is.EqualTo(VoiceSessionState.Idle));
            Assert.That(_session.LobbyId, Is.Null);
            Assert.That(_session.Speakers, Is.Empty);
            Assert.That(_speakerChanges[_speakerChanges.Count - 1], Is.Empty);
            Assert.That(_log, Has.Member("[Voice] left lobby=match-lobby"));

            _rtc.AnswerSending(null);
            _rtc.Speak(_ally, false);
            Assert.That(_session.IsSending, Is.False);
            Assert.That(_rtc.Sending, Is.Empty);

            _session.Leave();
            Assert.That(_rtc.Leaves, Is.EqualTo(1), "leaving twice does nothing");

            _session.Join(_lobby, _ => { });
            _rtc.AnswerJoin(null);
            _rtc.Connect();
            Assert.That(_rtc.SendingValues(), Is.EqualTo(Flags(true)));
        }

        [Test]
        public void LeavingBeforeTheJoinIsAnsweredDropsTheAnswer()
        {
            var answers = 0;
            _session.Join(_lobby, _ => answers++);

            _session.Leave();
            _rtc.AnswerJoin(null);

            Assert.That(answers, Is.Zero);
            Assert.That(_session.State, Is.EqualTo(VoiceSessionState.Idle));
        }

        [Test]
        public void AFailedJoinReturnsToIdleWithTheReason()
        {
            string joinError = null;
            _session.Join(_lobby, error => joinError = error);

            _rtc.AnswerJoin("the EOS Lobby, RTC or RTC Audio interface is unavailable");

            Assert.That(joinError, Is.EqualTo("the EOS Lobby, RTC or RTC Audio interface is unavailable"));
            Assert.That(_session.State, Is.EqualTo(VoiceSessionState.Idle));
            Assert.That(_session.LobbyId, Is.Null);
            Assert.That(_log, Has.Member("[Voice] join failed lobby=match-lobby reason=the EOS Lobby, RTC or RTC Audio interface is unavailable"));

            _session.Join(_lobby, _ => { });
            Assert.That(_session.State, Is.EqualTo(VoiceSessionState.Joining), "can join again");
        }

        [Test]
        public void JoiningTwiceOrWithoutALobbyIsRejected()
        {
            _session.Join(_lobby, _ => { });

            Assert.Throws<InvalidOperationException>(() => _session.Join("other", _ => { }));
            using var other = new VoiceSession(new FakeVoiceRtc(), _ => { });
            Assert.Throws<ArgumentException>(() => other.Join(string.Empty, _ => { }));
            Assert.Throws<ArgumentException>(() => _session.SetReceiving(string.Empty, false));
        }

        [Test]
        public void TheVolumeIsClampedAndNaNIsRejected()
        {
            Connect();

            _session.SetVolume(5f);
            Assert.That(_session.Volume, Is.EqualTo(VoiceSession.MaxVolume));
            Assert.That(_rtc.VolumeValues(), Is.EqualTo(Levels(VoiceSession.MaxVolume)));
            _rtc.AnswerVolume(null);

            _session.SetVolume(-1f);
            Assert.That(_session.Volume, Is.Zero);
            _rtc.AnswerVolume("InvalidParameters");
            Assert.That(_log, Has.Member("[Voice] volume=0.00 failed reason=InvalidParameters"));

            Assert.Throws<ArgumentOutOfRangeException>(() => _session.SetVolume(float.NaN));
        }

        [Test]
        public void TickAdvancesTheRtc()
        {
            _session.Tick();
            _session.Tick();

            Assert.That(_rtc.Ticks, Is.EqualTo(2));
        }

        [Test]
        public void DisposingLeavesTheRoomDisposesTheRtcAndStopsNotifying()
        {
            Connect();
            _rtc.Enter(_ally);

            _session.Dispose();
            var changes = _speakerChanges.Count;
            _rtc.Speak(_ally, true);
            _session.Tick();

            Assert.That(_rtc.Leaves, Is.EqualTo(1));
            Assert.That(_rtc.Disposed, Is.True);
            Assert.That(_rtc.Ticks, Is.Zero);
            Assert.That(_speakerChanges, Has.Count.EqualTo(changes));
            Assert.Throws<ObjectDisposedException>(() => _session.SetSending(true));
            Assert.Throws<ObjectDisposedException>(() => _session.Join(_lobby, _ => { }));
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Names(params string[] names) => names;

        private static bool[] Flags(params bool[] flags) => flags;

        private static float[] Levels(params float[] levels) => levels;

        private static VoiceSessionState[] States(params VoiceSessionState[] states) => states;

        private static string[] ToArray(IReadOnlyList<string> list)
        {
            var array = new string[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                array[i] = list[i];
            }

            return array;
        }

        /// <summary>要求を溜めておき、テストが順に応える偽の RTC。知らせはテストが起こす。</summary>
        private sealed class FakeVoiceRtc : IVoiceRtc
        {
            private Action<string> _join;

            public event Action<bool, string> ConnectionChanged;

            public event Action<string, bool> ParticipantChanged;

            public event Action<string, bool> SpeakingChanged;

            public string JoinedLobby { get; private set; }

            public int Leaves { get; private set; }

            public int Ticks { get; private set; }

            public bool Disposed { get; private set; }

            public List<KeyValuePair<bool, Action<string>>> Sending { get; } = new List<KeyValuePair<bool, Action<string>>>();

            public List<KeyValuePair<string, Action<string>>> Receiving { get; } = new List<KeyValuePair<string, Action<string>>>();

            public List<KeyValuePair<float, Action<string>>> Volumes { get; } = new List<KeyValuePair<float, Action<string>>>();

            public void Tick()
            {
                Ticks++;
            }

            public void JoinRoom(string lobbyId, Action<string> done)
            {
                JoinedLobby = lobbyId;
                _join = done;
            }

            public void LeaveRoom()
            {
                Leaves++;
            }

            public void UpdateSending(bool enabled, Action<string> done)
            {
                Sending.Add(new KeyValuePair<bool, Action<string>>(enabled, done));
            }

            public void UpdateReceiving(string participantId, bool enabled, Action<string> done)
            {
                Receiving.Add(new KeyValuePair<string, Action<string>>(participantId + "=" + (enabled ? "true" : "false"), done));
            }

            public void UpdateReceivingVolume(float volume, Action<string> done)
            {
                Volumes.Add(new KeyValuePair<float, Action<string>>(volume, done));
            }

            public void Dispose()
            {
                Disposed = true;
            }

            public bool[] SendingValues() => Sending.ConvertAll(request => request.Key).ToArray();

            public float[] VolumeValues() => Volumes.ConvertAll(request => request.Key).ToArray();

            public string[] ReceivingRequests() => Receiving.ConvertAll(request => request.Key).ToArray();

            public void AnswerJoin(string error)
            {
                var done = _join;
                _join = null;
                done(error);
            }

            public void AnswerSending(string error) => Answer(Sending, error);

            public void AnswerReceiving(string error) => Answer(Receiving, error);

            public void AnswerVolume(string error) => Answer(Volumes, error);

            public void Connect() => ConnectionChanged?.Invoke(true, null);

            public void Drop(string reason) => ConnectionChanged?.Invoke(false, reason);

            public void Enter(string participantId) => ParticipantChanged?.Invoke(participantId, true);

            public void Exit(string participantId) => ParticipantChanged?.Invoke(participantId, false);

            public void Speak(string participantId, bool speaking) => SpeakingChanged?.Invoke(participantId, speaking);

            private static void Answer<T>(List<KeyValuePair<T, Action<string>>> requests, string error)
            {
                Assert.That(requests, Is.Not.Empty, "no request is waiting for an answer");
                var done = requests[0].Value;
                requests.RemoveAt(0);
                done(error);
            }
        }
    }
}
