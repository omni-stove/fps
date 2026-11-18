using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // IVoiceSession は参加者の一覧を出さず、サービスは入ってきた人の声を受ける状態で始めるので、対応のある人は部屋に入る前から受けるかを決めておき、
    // 対応の無い人は話し始めて IVoiceSession.Speakers に出たときに切る。
    public sealed class ClientVoice : IDisposable
    {
        private readonly IVoiceSession _session;
        private readonly VoiceRoster _roster;
        private readonly ClientBlocks _blocks;
        private readonly Dictionary<int, Team> _teams = new Dictionary<int, Team>();
        private readonly Dictionary<string, bool> _receiving = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _participants = new List<string>();
        private readonly List<int> _speakingIds = new List<int>();
        private VoiceSettings _settings = VoiceSettings.Default;
        private int _localPlayerId = -1;
        private Team? _localTeam;
        private bool _talking;
        private bool _entered;
        private int _enterEpoch;
        private VoiceStatus? _status;
        private bool _disposed;

        public ClientVoice(IVoiceSession session, VoiceRoster roster, ClientBlocks blocks = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _blocks = blocks;
            _session.SpeakersChanged += HandleSpeakersChanged;
            _session.StateChanged += HandleStateChanged;
            _roster.Changed += Refresh;
            if (_blocks != null)
            {
                _blocks.Changed += Refresh;
            }

            _session.SetVolume(_settings.Volume);
        }

        public event Action<VoiceStatus?> StatusChanged;

        public VoiceStatus? Status => _status;

        public VoiceSettings Settings => _settings;

        public bool IsTalking => _talking;

        public bool IsEntered => _entered;

        public void Enter(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("a lobby id is required", nameof(lobbyId));
            }

            ThrowIfDisposed();
            if (_entered && string.Equals(_session.LobbyId, lobbyId, StringComparison.Ordinal))
            {
                return;
            }

            Leave();
            var epoch = ++_enterEpoch;
            _entered = true;
            try
            {
                _session.Join(lobbyId, error =>
                {
                    if (_disposed || error == null || epoch != _enterEpoch)
                    {
                        return;
                    }

                    _entered = false;
                    ApplySending();
                    Publish();
                });
            }
            catch
            {
                _entered = false;
                throw;
            }

            ApplySending();
            Publish();
        }

        public void Leave()
        {
            if (_disposed || !_entered)
            {
                return;
            }

            _entered = false;
            _enterEpoch++;
            _session.Leave();
            ApplySending();
            Publish();
        }

        public void SetTalking(bool talking)
        {
            ThrowIfDisposed();
            _talking = talking;
            ApplySending();
            Publish();
        }

        public void ApplySettings(VoiceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            ThrowIfDisposed();
            _settings = settings;
            _session.SetVolume(settings.Volume);
            ApplySending();
            Publish();
        }

        // 差分を埋め終えていないスナップショットはチームが正しくないので読まない。
        public void Apply(SnapshotMessage snapshot, int localPlayerId)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ThrowIfDisposed();
            if (!snapshot.IsComplete)
            {
                return;
            }

            _localPlayerId = localPlayerId;
            _teams.Clear();
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var player = ref players[i];
                _teams[player.PlayerId] = player.Team;
            }

            _localTeam = _teams.TryGetValue(localPlayerId, out var team) ? team : (Team?)null;
            Refresh();
        }

        public void Tick()
        {
            if (_disposed)
            {
                return;
            }

            _session.Tick();
            if (!_disposed)
            {
                Publish();
            }
        }

        public bool ShouldReceive(string productUserId)
        {
            if (!_roster.TryGetPlayerId(productUserId, out var playerId))
            {
                return false;
            }

            if (playerId == _localPlayerId)
            {
                return true;
            }

            if (_blocks != null && _blocks.IsMutedUser(productUserId))
            {
                return false;
            }

            return _localTeam.HasValue && _teams.TryGetValue(playerId, out var team) && team == _localTeam.Value;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _session.SpeakersChanged -= HandleSpeakersChanged;
            _session.StateChanged -= HandleStateChanged;
            _roster.Changed -= Refresh;
            if (_blocks != null)
            {
                _blocks.Changed -= Refresh;
            }

            _session.Dispose();
            _entered = false;
            if (_status.HasValue)
            {
                _status = null;
                StatusChanged?.Invoke(null);
            }

            StatusChanged = null;
        }

        private void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            _participants.Clear();
            _participants.AddRange(_roster.ProductUserIds);
            foreach (var participant in _receiving.Keys)
            {
                if (!_roster.TryGetPlayerId(participant, out _))
                {
                    _participants.Add(participant);
                }
            }

            foreach (var participant in _participants)
            {
                Decide(participant);
            }

            Publish();
        }

        private void Decide(string productUserId)
        {
            var receive = ShouldReceive(productUserId);
            if (_receiving.TryGetValue(productUserId, out var current) && current == receive)
            {
                return;
            }

            _receiving[productUserId] = receive;
            _session.SetReceiving(productUserId, receive);
        }

        private void HandleSpeakersChanged(IReadOnlyList<string> speakers)
        {
            if (_disposed)
            {
                return;
            }

            // SetReceiving の中で話している人の一覧が変わり、この知らせが入れ子で来るので、写してから回す
            var copy = new string[speakers.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = speakers[i];
            }

            foreach (var speaker in copy)
            {
                Decide(speaker);
            }

            Publish();
        }

        private void HandleStateChanged(VoiceSessionState state)
        {
            if (!_disposed)
            {
                Publish();
            }
        }

        private void ApplySending()
        {
            var sending = _entered && _talking && _settings.MicrophoneEnabled;
            if (_session.WantsSending != sending)
            {
                _session.SetSending(sending);
            }
        }

        private void Publish()
        {
            if (!_entered)
            {
                if (_status.HasValue)
                {
                    _status = null;
                    StatusChanged?.Invoke(null);
                }

                return;
            }

            CollectSpeakers();
            var microphone = _settings.MicrophoneEnabled;
            var localSpeaking = microphone && _session.WantsSending && _session.IsSending;

            // 毎フレーム呼ぶので、変わらないときは VoiceStatus (一覧を写す) を作らない
            if (_status.HasValue && Same(_status.Value, localSpeaking, microphone))
            {
                return;
            }

            var status = new VoiceStatus(localSpeaking, _speakingIds, microphone);
            _status = status;
            StatusChanged?.Invoke(status);
        }

        private bool Same(VoiceStatus status, bool localSpeaking, bool microphone)
        {
            if (status.IsLocalSpeaking != localSpeaking || status.MicrophoneAvailable != microphone
                || status.SpeakingIds.Count != _speakingIds.Count)
            {
                return false;
            }

            for (var i = 0; i < _speakingIds.Count; i++)
            {
                if (status.SpeakingIds[i] != _speakingIds[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void CollectSpeakers()
        {
            _speakingIds.Clear();
            foreach (var speaker in _session.Speakers)
            {
                if (_roster.TryGetPlayerId(speaker, out var playerId) && playerId != _localPlayerId && !_speakingIds.Contains(playerId)
                    && ShouldReceive(speaker))
                {
                    _speakingIds.Add(playerId);
                }
            }

            _speakingIds.Sort();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ClientVoice));
            }
        }
    }
}
