using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Shooter.NetV2;
// RTC・RTC Audio と Lobby の名前空間を両方開くと型名がぶつかりうるので、RTC と RTC Audio は別名で参照する
using EosRtc = Epic.OnlineServices.RTC;
using EosRtcAudio = Epic.OnlineServices.RTCAudio;

namespace Shooter.NetEos
{
    /// <summary>
    /// 試合のロビーの RTC 部屋で話す <see cref="IVoiceSession"/>。望む値を覚えて反映する手順は <see cref="VoiceSession"/> に任せ、
    /// ここでは EOS の Lobby・RTC・RTC Audio インターフェースを呼ぶ。送るの入り切りは <c>UpdateSending</c>、
    /// 参加者ごとに受けるかは <c>UpdateReceiving</c>、音量は <c>UpdateReceivingVolume</c> で行う。
    /// </summary>
    /// <remarks>
    /// EOS のプラットフォームとログインは、<see cref="EosLobbyBackend"/> か通信路が済ませたものを使う。
    /// ロビーにはメンバーとして先に入っておく (<see cref="ILobbyBackend.JoinMatchLobby"/>)。
    /// </remarks>
    internal sealed class EosVoiceSession : IVoiceSession
    {
        private readonly VoiceSession _session;

        public EosVoiceSession(Action<string> log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            _session = new VoiceSession(new Room(log), log);
        }

        public event Action<IReadOnlyList<string>> SpeakersChanged
        {
            add => _session.SpeakersChanged += value;
            remove => _session.SpeakersChanged -= value;
        }

        public event Action<VoiceSessionState> StateChanged
        {
            add => _session.StateChanged += value;
            remove => _session.StateChanged -= value;
        }

        public VoiceSessionState State => _session.State;

        public string LobbyId => _session.LobbyId;

        public IReadOnlyList<string> Speakers => _session.Speakers;

        public bool WantsSending => _session.WantsSending;

        public bool IsSending => _session.IsSending;

        /// <summary>
        /// ロビーの RTC 部屋に入るときの自分の音声の設定。押している間だけ話すので、マイクを切った状態で入り、
        /// <c>UpdateSending</c> で送り始める。
        /// </summary>
        public static LocalRTCOptions RoomOptions()
        {
            return new LocalRTCOptions { LocalAudioDeviceInputStartsMuted = true };
        }

        public void Tick() => _session.Tick();

        public void Join(string lobbyId, Action<string> done) => _session.Join(lobbyId, done);

        public void Leave() => _session.Leave();

        public void SetSending(bool sending) => _session.SetSending(sending);

        public void SetReceiving(string participantId, bool receiving) => _session.SetReceiving(participantId, receiving);

        public void SetVolume(float volume) => _session.SetVolume(volume);

        public void Dispose() => _session.Dispose();

        /// <summary>EOS の Lobby・RTC・RTC Audio インターフェースで、ロビーの RTC 部屋 1 つを呼ぶ。</summary>
        private sealed class Room : IVoiceRtc
        {
            /// <summary>EOS の受ける音量で、音量を変えない値 (0 から 100 のうち 50)。</summary>
            private const float _eosUnityVolume = 50f;

            private readonly Action<string> _log;
            private PlatformInterfaceHandle _handle;
            private string _lobbyId;
            private string _roomName;
            private ulong _connectionNotification;
            private ulong _statusNotification;
            private ulong _updatedNotification;
            private bool _disposed;

            public Room(Action<string> log)
            {
                _log = log;
            }

            public event Action<bool, string> ConnectionChanged;

            public event Action<string, bool> ParticipantChanged;

            public event Action<string, bool> SpeakingChanged;

            private bool InRoom => _lobbyId != null && _handle != null && _handle.Alive;

            public void Tick()
            {
                if (!_disposed && _handle != null && _handle.Alive)
                {
                    EosPlatform.Tick();
                }
            }

            public void JoinRoom(string lobbyId, Action<string> done)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(EosVoiceSession));
                }

                if (_lobbyId != null)
                {
                    throw new InvalidOperationException("already in the voice room of lobby " + _lobbyId);
                }

                var handle = PlatformInterfaceHandle.Acquire(out var unavailable);
                if (handle == null)
                {
                    done(unavailable);
                    return;
                }

                var joinOptions = new JoinRTCRoomOptions
                {
                    LobbyId = lobbyId,
                    LocalUserId = handle.LocalUserId,
                    LocalRTCOptions = RoomOptions(),
                };
                var result = handle.Lobby.JoinRTCRoom(ref joinOptions);
                if (result != Result.Success)
                {
                    done("joining the RTC room of the EOS lobby id=" + lobbyId + " failed: " + result);
                    return;
                }

                var nameOptions = new GetRTCRoomNameOptions { LobbyId = lobbyId, LocalUserId = handle.LocalUserId };
                result = handle.Lobby.GetRTCRoomName(ref nameOptions, out var roomName);
                var room = Text(roomName);
                if (result != Result.Success || room == null)
                {
                    LeaveRtcRoom(handle, lobbyId);
                    done("reading the RTC room name of the EOS lobby id=" + lobbyId + " failed: " + result);
                    return;
                }

                _handle = handle;
                _lobbyId = lobbyId;
                _roomName = room;
                if (!AddNotifications())
                {
                    RemoveNotifications();
                    LeaveRtcRoom(handle, lobbyId);
                    Clear();
                    done("the EOS RTC notifications of the lobby id=" + lobbyId + " could not be registered");
                    return;
                }

                done(null);
            }

            public void LeaveRoom()
            {
                if (_lobbyId == null)
                {
                    return;
                }

                RemoveNotifications();
                if (_handle.Alive)
                {
                    LeaveRtcRoom(_handle, _lobbyId);
                }

                Clear();
            }

            public void UpdateSending(bool enabled, Action<string> done)
            {
                if (!InRoom)
                {
                    done("not in an EOS voice room");
                    return;
                }

                var options = new EosRtcAudio.UpdateSendingOptions
                {
                    LocalUserId = _handle.LocalUserId,
                    RoomName = _roomName,
                    AudioStatus = enabled ? EosRtcAudio.RTCAudioStatus.Enabled : EosRtcAudio.RTCAudioStatus.Disabled,
                };
                _handle.Audio.UpdateSending(ref options, null, (ref EosRtcAudio.UpdateSendingCallbackInfo info) =>
                {
                    if (!_disposed)
                    {
                        done(info.ResultCode == Result.Success ? null : info.ResultCode.ToString());
                    }
                });
            }

            public void UpdateReceiving(string participantId, bool enabled, Action<string> done)
            {
                if (!InRoom)
                {
                    done("not in an EOS voice room");
                    return;
                }

                var participant = ProductUserId.FromString(participantId);
                if (participant == null || !participant.IsValid())
                {
                    done("not an EOS product user id: " + participantId);
                    return;
                }

                var options = new EosRtcAudio.UpdateReceivingOptions
                {
                    LocalUserId = _handle.LocalUserId,
                    RoomName = _roomName,
                    ParticipantId = participant,
                    AudioEnabled = enabled,
                };
                _handle.Audio.UpdateReceiving(ref options, null, (ref EosRtcAudio.UpdateReceivingCallbackInfo info) =>
                {
                    if (!_disposed)
                    {
                        done(info.ResultCode == Result.Success ? null : info.ResultCode.ToString());
                    }
                });
            }

            public void UpdateReceivingVolume(float volume, Action<string> done)
            {
                if (!InRoom)
                {
                    done("not in an EOS voice room");
                    return;
                }

                var options = new EosRtcAudio.UpdateReceivingVolumeOptions
                {
                    LocalUserId = _handle.LocalUserId,
                    RoomName = _roomName,
                    Volume = Math.Max(0f, Math.Min(VoiceSession.MaxVolume, volume)) * _eosUnityVolume,
                };
                _handle.Audio.UpdateReceivingVolume(ref options, null, (ref EosRtcAudio.UpdateReceivingVolumeCallbackInfo info) =>
                {
                    if (!_disposed)
                    {
                        done(info.ResultCode == Result.Success ? null : info.ResultCode.ToString());
                    }
                });
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                LeaveRoom();
                _disposed = true;
                ConnectionChanged = null;
                ParticipantChanged = null;
                SpeakingChanged = null;
            }

            private bool AddNotifications()
            {
                var connectionOptions = new AddNotifyRTCRoomConnectionChangedOptions();
                _connectionNotification = _handle.Lobby.AddNotifyRTCRoomConnectionChanged(
                    ref connectionOptions, null, HandleRoomConnectionChanged);

                var statusOptions = new EosRtc.AddNotifyParticipantStatusChangedOptions
                {
                    LocalUserId = _handle.LocalUserId,
                    RoomName = _roomName,
                };
                _statusNotification = _handle.Rtc.AddNotifyParticipantStatusChanged(ref statusOptions, null, HandleParticipantStatusChanged);

                var updatedOptions = new EosRtcAudio.AddNotifyParticipantUpdatedOptions
                {
                    LocalUserId = _handle.LocalUserId,
                    RoomName = _roomName,
                };
                _updatedNotification = _handle.Audio.AddNotifyParticipantUpdated(ref updatedOptions, null, HandleParticipantUpdated);

                return _connectionNotification != 0 && _statusNotification != 0 && _updatedNotification != 0;
            }

            private void RemoveNotifications()
            {
                if (_handle == null || !_handle.Alive)
                {
                    _connectionNotification = 0;
                    _statusNotification = 0;
                    _updatedNotification = 0;
                    return;
                }

                if (_connectionNotification != 0)
                {
                    _handle.Lobby.RemoveNotifyRTCRoomConnectionChanged(_connectionNotification);
                    _connectionNotification = 0;
                }

                if (_statusNotification != 0)
                {
                    _handle.Rtc.RemoveNotifyParticipantStatusChanged(_statusNotification);
                    _statusNotification = 0;
                }

                if (_updatedNotification != 0)
                {
                    _handle.Audio.RemoveNotifyParticipantUpdated(_updatedNotification);
                    _updatedNotification = 0;
                }
            }

            private void LeaveRtcRoom(PlatformInterfaceHandle handle, string lobbyId)
            {
                var options = new LeaveRTCRoomOptions { LobbyId = lobbyId, LocalUserId = handle.LocalUserId };
                var result = handle.Lobby.LeaveRTCRoom(ref options);
                if (result != Result.Success && result != Result.NotFound)
                {
                    _log(VoiceLog.LeaveFailed(lobbyId, result.ToString()));
                }
            }

            private void Clear()
            {
                _handle = null;
                _lobbyId = null;
                _roomName = null;
            }

            private void HandleRoomConnectionChanged(ref RTCRoomConnectionChangedCallbackInfo info)
            {
                if (_disposed || !InRoom || !string.Equals(Text(info.LobbyId), _lobbyId, StringComparison.Ordinal)
                    || !_handle.IsLocalUser(info.LocalUserId))
                {
                    return;
                }

                ConnectionChanged?.Invoke(info.IsConnected, info.IsConnected ? null : info.DisconnectReason.ToString());
            }

            private void HandleParticipantStatusChanged(ref EosRtc.ParticipantStatusChangedCallbackInfo info)
            {
                if (!IsThisRoom(info.LocalUserId, info.RoomName) || info.ParticipantId == null || !info.ParticipantId.IsValid()
                    || _handle.IsLocalUser(info.ParticipantId))
                {
                    return;
                }

                ParticipantChanged?.Invoke(
                    info.ParticipantId.ToString(), info.ParticipantStatus == EosRtc.RTCParticipantStatus.Joined);
            }

            private void HandleParticipantUpdated(ref EosRtcAudio.ParticipantUpdatedCallbackInfo info)
            {
                if (!IsThisRoom(info.LocalUserId, info.RoomName) || info.ParticipantId == null || !info.ParticipantId.IsValid())
                {
                    return;
                }

                SpeakingChanged?.Invoke(
                    info.ParticipantId.ToString(), info.Speaking && info.AudioStatus == EosRtcAudio.RTCAudioStatus.Enabled);
            }

            private bool IsThisRoom(ProductUserId localUserId, Utf8String roomName)
            {
                return !_disposed && InRoom && _handle.IsLocalUser(localUserId)
                    && string.Equals(Text(roomName), _roomName, StringComparison.Ordinal);
            }

            /// <summary>SDK の文字列を string にする。null や空なら null。</summary>
            private static string Text(Utf8String value)
            {
                // Utf8String の演算子を通さずに null を判定する
                if ((object)value == null)
                {
                    return null;
                }

                var text = value.ToString();
                return string.IsNullOrEmpty(text) ? null : text;
            }
        }

        /// <summary>部屋に入った時点のプラットフォームとログインと、そこから取ったインターフェース。</summary>
        private sealed class PlatformInterfaceHandle
        {
            private readonly Epic.OnlineServices.Platform.PlatformInterface _platform;

            private PlatformInterfaceHandle(
                Epic.OnlineServices.Platform.PlatformInterface platform, ProductUserId localUserId, LobbyInterface lobby, EosRtc.RTCInterface rtc,
                EosRtcAudio.RTCAudioInterface audio)
            {
                _platform = platform;
                LocalUserId = localUserId;
                Lobby = lobby;
                Rtc = rtc;
                Audio = audio;
            }

            public ProductUserId LocalUserId { get; }

            public LobbyInterface Lobby { get; }

            public EosRtc.RTCInterface Rtc { get; }

            public EosRtcAudio.RTCAudioInterface Audio { get; }

            /// <summary>部屋に入ったときのプラットフォームが、まだ解放されていないか。</summary>
            public bool Alive => _platform != null && ReferenceEquals(EosPlatform.Current, _platform);

            public static PlatformInterfaceHandle Acquire(out string error)
            {
                var platform = EosPlatform.Current;
                var localUserId = EosPlatform.LocalUserId;
                if (platform == null || localUserId == null)
                {
                    error = "voice needs an EOS login; log in to the lobby or the EOS transport first";
                    return null;
                }

                var lobby = platform.GetLobbyInterface();
                var rtc = EosPlatform.Rtc;
                var audio = EosPlatform.RtcAudio;
                if (lobby == null || rtc == null || audio == null)
                {
                    error = "the EOS Lobby, RTC or RTC Audio interface is unavailable";
                    return null;
                }

                error = null;
                return new PlatformInterfaceHandle(platform, localUserId, lobby, rtc, audio);
            }

            public bool IsLocalUser(ProductUserId userId)
            {
                return userId != null
                    && string.Equals(userId.ToString(), LocalUserId.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
