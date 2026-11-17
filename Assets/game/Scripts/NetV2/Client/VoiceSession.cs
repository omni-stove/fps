using System;
using System.Collections.Generic;

namespace Shooter.NetV2
{
    public sealed class VoiceSession : IVoiceSession
    {
        public const float DefaultVolume = Shooter.Core.VoiceSettings.DefaultVolume;
        public const float MaxVolume = Shooter.Core.VoiceSettings.MaxVolume;

        private readonly IVoiceRtc _rtc;
        private readonly Action<string> _log;
        private readonly Setting<bool> _sending = new Setting<bool>(false);
        private readonly Setting<float> _volume = new Setting<float>(DefaultVolume);
        private readonly HashSet<string> _muted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Setting<bool>> _receiving = new Dictionary<string, Setting<bool>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _speaking = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<string> _speakers = Array.Empty<string>();
        private VoiceSessionState _state;
        private string _lobbyId;
        private int _joinEpoch;
        private int _connectionEpoch;
        private bool _disposed;

        public VoiceSession(IVoiceRtc rtc, Action<string> log)
        {
            _rtc = rtc ?? throw new ArgumentNullException(nameof(rtc));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _rtc.ConnectionChanged += HandleConnectionChanged;
            _rtc.ParticipantChanged += HandleParticipantChanged;
            _rtc.SpeakingChanged += HandleSpeakingChanged;
        }

        public event Action<IReadOnlyList<string>> SpeakersChanged;

        public event Action<VoiceSessionState> StateChanged;

        public VoiceSessionState State => _state;

        public string LobbyId => _lobbyId;

        public IReadOnlyList<string> Speakers => _speakers;

        public bool WantsSending => _sending.Wanted;

        public bool IsSending => _state == VoiceSessionState.Connected && _sending.Applied == true;

        public float Volume => _volume.Wanted;

        public void Tick()
        {
            if (!_disposed)
            {
                _rtc.Tick();
            }
        }

        public void Join(string lobbyId, Action<string> done)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("a lobby id is required", nameof(lobbyId));
            }

            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            ThrowIfDisposed();
            if (_state != VoiceSessionState.Idle)
            {
                throw new InvalidOperationException("already in the voice room of lobby " + _lobbyId);
            }

            _lobbyId = lobbyId;
            var epoch = ++_joinEpoch;
            SetState(VoiceSessionState.Joining);
            _log(VoiceLog.Joining(lobbyId));
            _rtc.JoinRoom(lobbyId, error =>
            {
                if (_disposed || epoch != _joinEpoch)
                {
                    return;
                }

                if (error != null)
                {
                    _lobbyId = null;
                    SetState(VoiceSessionState.Idle);
                    _log(VoiceLog.JoinFailed(lobbyId, error));
                    done(error);
                    return;
                }

                // つながった知らせが返事より先に来ていたら、Connected のままにする
                if (_state == VoiceSessionState.Joining)
                {
                    SetState(VoiceSessionState.Connecting);
                }

                done(null);
            });
        }

        public void Leave()
        {
            if (_disposed || _state == VoiceSessionState.Idle)
            {
                return;
            }

            var lobbyId = _lobbyId;
            _joinEpoch++;
            Disconnect();
            _rtc.LeaveRoom();
            _lobbyId = null;
            SetState(VoiceSessionState.Idle);
            _log(VoiceLog.Left(lobbyId));
            PublishSpeakers();
        }

        public void SetSending(bool sending)
        {
            ThrowIfDisposed();
            _sending.Want(sending);
            PumpSending();
        }

        public void SetReceiving(string participantId, bool receiving)
        {
            if (string.IsNullOrEmpty(participantId))
            {
                throw new ArgumentException("a participant id is required", nameof(participantId));
            }

            ThrowIfDisposed();
            if (receiving)
            {
                _muted.Remove(participantId);
            }
            else
            {
                _muted.Add(participantId);
            }

            if (_receiving.TryGetValue(participantId, out var setting))
            {
                setting.Want(receiving);
                PumpReceiving(participantId, setting);
            }

            PublishSpeakers();
        }

        public void SetVolume(float volume)
        {
            if (float.IsNaN(volume))
            {
                throw new ArgumentOutOfRangeException(nameof(volume), "the voice volume is NaN");
            }

            ThrowIfDisposed();
            _volume.Want(Math.Max(0f, Math.Min(MaxVolume, volume)));
            PumpVolume();
        }

        public bool WantsReceiving(string participantId)
        {
            return !string.IsNullOrEmpty(participantId) && !_muted.Contains(participantId);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Leave();
            _disposed = true;
            _rtc.ConnectionChanged -= HandleConnectionChanged;
            _rtc.ParticipantChanged -= HandleParticipantChanged;
            _rtc.SpeakingChanged -= HandleSpeakingChanged;
            _rtc.Dispose();
            SpeakersChanged = null;
            StateChanged = null;
        }

        private void HandleConnectionChanged(bool connected, string reason)
        {
            if (_disposed || _state == VoiceSessionState.Idle)
            {
                return;
            }

            if (connected)
            {
                if (_state == VoiceSessionState.Connected)
                {
                    return;
                }

                // つないだ直後のサービスの送る・音量の値は分からないので送り直す。受けるかは、参加者が入った知らせのたびに反映する
                _connectionEpoch++;
                _sending.Forget();
                _volume.Forget();
                SetState(VoiceSessionState.Connected);
                _log(VoiceLog.Connected(_lobbyId));
                PumpSending();
                PumpVolume();
                foreach (var participant in new List<KeyValuePair<string, Setting<bool>>>(_receiving))
                {
                    PumpReceiving(participant.Key, participant.Value);
                }

                return;
            }

            if (_state != VoiceSessionState.Connected)
            {
                return;
            }

            Disconnect();
            SetState(VoiceSessionState.Connecting);
            _log(VoiceLog.ConnectionLost(_lobbyId, reason));
            PublishSpeakers();
        }

        private void HandleParticipantChanged(string participantId, bool inRoom)
        {
            if (_disposed || _state == VoiceSessionState.Idle || string.IsNullOrEmpty(participantId))
            {
                return;
            }

            if (!inRoom)
            {
                _receiving.Remove(participantId);
                if (_speaking.Remove(participantId))
                {
                    PublishSpeakers();
                }

                return;
            }

            if (_receiving.ContainsKey(participantId))
            {
                return;
            }

            // サービスは入ってきた人の声を受ける状態で始める
            var setting = new Setting<bool>(true);
            setting.Accept(true);
            setting.Want(!_muted.Contains(participantId));
            _receiving.Add(participantId, setting);
            PumpReceiving(participantId, setting);
        }

        private void HandleSpeakingChanged(string participantId, bool speaking)
        {
            if (_disposed || _state == VoiceSessionState.Idle || string.IsNullOrEmpty(participantId))
            {
                return;
            }

            var changed = speaking ? _speaking.Add(participantId) : _speaking.Remove(participantId);
            if (changed)
            {
                PublishSpeakers();
            }
        }

        private void Disconnect()
        {
            _connectionEpoch++;
            _sending.Forget();
            _volume.Forget();
            _receiving.Clear();
            _speaking.Clear();
        }

        private void PumpSending()
        {
            if (_state != VoiceSessionState.Connected || !_sending.NeedsRequest)
            {
                return;
            }

            var value = _sending.Wanted;
            var epoch = _connectionEpoch;
            _sending.Begin();
            _rtc.UpdateSending(value, error =>
            {
                if (_disposed || epoch != _connectionEpoch)
                {
                    return;
                }

                _sending.Finish(value, error);
                if (error != null)
                {
                    _log(VoiceLog.SendingFailed(value, error));
                }

                PumpSending();
            });
        }

        private void PumpReceiving(string participantId, Setting<bool> setting)
        {
            if (_state != VoiceSessionState.Connected || !setting.NeedsRequest)
            {
                return;
            }

            var value = setting.Wanted;
            var epoch = _connectionEpoch;
            setting.Begin();
            _rtc.UpdateReceiving(participantId, value, error =>
            {
                // 返事を待つ間に出ていった人の返事は捨てる
                if (_disposed || epoch != _connectionEpoch
                    || !_receiving.TryGetValue(participantId, out var current) || !ReferenceEquals(current, setting))
                {
                    return;
                }

                setting.Finish(value, error);
                if (error != null)
                {
                    _log(VoiceLog.ReceivingFailed(participantId, value, error));
                }

                PumpReceiving(participantId, setting);
            });
        }

        private void PumpVolume()
        {
            if (_state != VoiceSessionState.Connected || !_volume.NeedsRequest)
            {
                return;
            }

            var value = _volume.Wanted;
            var epoch = _connectionEpoch;
            _volume.Begin();
            _rtc.UpdateReceivingVolume(value, error =>
            {
                if (_disposed || epoch != _connectionEpoch)
                {
                    return;
                }

                _volume.Finish(value, error);
                if (error != null)
                {
                    _log(VoiceLog.VolumeFailed(value, error));
                }

                PumpVolume();
            });
        }

        private void PublishSpeakers()
        {
            var speakers = new List<string>(_speaking.Count);
            foreach (var participantId in _speaking)
            {
                if (!_muted.Contains(participantId))
                {
                    speakers.Add(participantId);
                }
            }

            speakers.Sort(StringComparer.Ordinal);
            if (SameSpeakers(_speakers, speakers))
            {
                return;
            }

            _speakers = speakers.ToArray();
            SpeakersChanged?.Invoke(_speakers);
        }

        private static bool SameSpeakers(IReadOnlyList<string> a, List<string> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private void SetState(VoiceSessionState state)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
            StateChanged?.Invoke(state);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VoiceSession));
            }
        }

        private sealed class Setting<T>
            where T : struct, IEquatable<T>
        {
            private T? _failed;
            private bool _pending;

            public Setting(T wanted)
            {
                Wanted = wanted;
            }

            public T Wanted { get; private set; }

            public T? Applied { get; private set; }

            // 断られた値を望む値が変わるまで要求し直さないのは、サービスが断り続ける値を返事のたびに要求し直さないため。
            public bool NeedsRequest => !_pending && !Matches(Applied) && !Matches(_failed);

            public void Want(T value)
            {
                if (!value.Equals(Wanted))
                {
                    Wanted = value;
                    _failed = null;
                }
            }

            public void Accept(T value)
            {
                Applied = value;
            }

            public void Begin()
            {
                _pending = true;
            }

            public void Finish(T value, string error)
            {
                _pending = false;
                if (error == null)
                {
                    Applied = value;
                    _failed = null;
                }
                else
                {
                    _failed = value;
                }
            }

            public void Forget()
            {
                Applied = null;
                _failed = null;
                _pending = false;
            }

            private bool Matches(T? value) => value.HasValue && value.Value.Equals(Wanted);
        }
    }
}
