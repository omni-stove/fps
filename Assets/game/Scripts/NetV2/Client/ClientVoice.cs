using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    /// <summary>
    /// 試合中のボイスチャットを <see cref="IVoiceSession"/> で動かす。試合のロビーの RTC 部屋に入り、押している間だけ送り、
    /// スナップショットのチームを見て味方の声だけを受け、今の状態を <see cref="VoiceStatus"/> にする。Unity に依存しない。
    /// </summary>
    /// <remarks>
    /// 声を受けるのは、<see cref="VoiceRoster"/> で試合のプレイヤーと分かり、そのプレイヤーが自分と同じチームにいる人だけ。
    /// 対応の分からない人・試合にいない人・自分のチームが分からない間は受けない。
    /// 対応のある人は部屋に入る前から受けるかを決めておく。<see cref="IVoiceSession"/> は参加者の一覧を出さず、サービスは入ってきた人の声を
    /// 受ける状態で始めるので、対応の無い人は話し始めて <see cref="IVoiceSession.Speakers"/> に出たときに切る。
    /// </remarks>
    public sealed class ClientVoice : IDisposable
    {
        private readonly IVoiceSession _session;
        private readonly VoiceRoster _roster;
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

        /// <param name="session">部屋を呼ぶ側。このオブジェクトが Dispose する。</param>
        /// <param name="roster">参加者とプレイヤーの対応。変わったら受けるかを決め直す。</param>
        public ClientVoice(IVoiceSession session, VoiceRoster roster)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _session.SpeakersChanged += HandleSpeakersChanged;
            _session.StateChanged += HandleStateChanged;
            _roster.Changed += Refresh;
            _session.SetVolume(_settings.Volume);
        }

        /// <summary><see cref="Status"/> が変わった。</summary>
        public event Action<VoiceStatus?> StatusChanged;

        /// <summary>今の状態。部屋に入っていなければ null。</summary>
        public VoiceStatus? Status => _status;

        public VoiceSettings Settings => _settings;

        /// <summary>押して話そうとしているか (<see cref="SetTalking"/> で決めた値)。</summary>
        public bool IsTalking => _talking;

        /// <summary>部屋に入っている (入ろうとしている) か。</summary>
        public bool IsEntered => _entered;

        /// <summary>
        /// 試合のロビー <paramref name="lobbyId"/> の RTC 部屋に入る。もう同じロビーに入っていれば何もしない。別のロビーに入っていれば出てから入る。
        /// 入れなければ部屋の外に戻る (理由は <see cref="IVoiceSession"/> がログに出す)。
        /// </summary>
        /// <exception cref="ArgumentException">lobbyId が空。</exception>
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

        /// <summary>部屋から出る。入っていなければ何もしない。</summary>
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

        /// <summary>押して話すボタンを押しているか。押している間だけ (マイクが入っていれば) 送る。</summary>
        public void SetTalking(bool talking)
        {
            ThrowIfDisposed();
            _talking = talking;
            ApplySending();
            Publish();
        }

        /// <summary>マイクの入り切りと受ける声の音量を変える。</summary>
        /// <exception cref="ArgumentNullException">settings が null。</exception>
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

        /// <summary>
        /// スナップショットのプレイヤーのチームを覚え、受けるかを決め直す。<paramref name="localPlayerId"/> は自分のプレイヤーの Id。
        /// 差分を埋め終えていないスナップショットはチームが正しくないので読まない。
        /// </summary>
        /// <exception cref="ArgumentNullException">snapshot が null。</exception>
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

        /// <summary>SDK の処理とコールバックを進め、状態を出し直す。毎フレーム呼ぶ。</summary>
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

        /// <summary>参加者 <paramref name="productUserId"/> の声を受けるべきか (自分と、自分と同じチームのプレイヤー)。</summary>
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
            _session.Dispose();
            _entered = false;
            if (_status.HasValue)
            {
                _status = null;
                StatusChanged?.Invoke(null);
            }

            StatusChanged = null;
        }

        /// <summary>知っている参加者 (対応のある人と、前に決めた人) の受けるかを決め直す。</summary>
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
