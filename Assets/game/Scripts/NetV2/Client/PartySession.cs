using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    /// <summary>
    /// パーティ (EOS のメンバー用ロビー) を作る・招待のコードで入る・出る。リーダーは入るサーバーと状態を自分のメンバーの属性に書き
    /// (<see cref="ChooseServer"/>)、メンバーは間を置いてリーダーの属性を読み、新しいサーバーが決まったら <see cref="ServerChosen"/> で知らせる。
    /// リーダーは <see cref="PartyLobby.Leader"/> で決めるので、リーダーが抜けたら次の人がリーダーになる。
    /// 試合につなぐときは <see cref="WithParty"/> で <see cref="ConnectionExtras.PartyId"/> を載せ、サーバーが同じチームに入れる。
    /// </summary>
    /// <remarks>
    /// サービスはロビーの変化を知らせないので、<see cref="Tick"/> で間を置いてロビーとリーダーの属性を読み直す。
    /// ログは <see cref="PartyLog"/> の行で出す。
    /// </remarks>
    public sealed class PartySession : IDisposable
    {
        private readonly ILobbyBackend _backend;
        private readonly string _version;
        private readonly int _maxMembers;
        private readonly float _pollIntervalSeconds;
        private readonly Action<string> _log;

        private LoginStep _login;
        private string _userId;

        private PartyPhase _phase;
        private PartyError _error;
        private string _joinCode;
        private bool _requesting;
        private bool _leaveWhenJoined;
        private int _generation;
        private LobbyInfo _lobby;
        private string _leader;
        private PartyState _state;

        private long _match;
        private IReadOnlyList<LobbyAttribute> _pendingWrite;
        private bool _writing;

        private string _followedLeader;
        private long? _followedMatch;
        private bool _baselineLeader;
        private bool _reportedUnreadable;

        private bool _polling;
        private float _now;
        private float _nextPoll;
        private PartyStatus _status = PartyStatus.None;
        private bool _disposed;

        /// <param name="backend">ログイン前のロビーのサービス。このセッションが Dispose する。</param>
        /// <param name="version">自分のビルドの版 (Application.version)。パーティのバケットと版の照合に使う。空なら "0"。</param>
        /// <param name="log">ログの 1 行を受け取る。</param>
        public PartySession(ILobbyBackend backend, string version, Action<string> log)
            : this(backend, version, PartyLobby.DefaultMaxMembers, PartyLobby.DefaultPollIntervalSeconds, log)
        {
        }

        /// <param name="backend">ログイン前のロビーのサービス。このセッションが Dispose する。</param>
        /// <param name="version">自分のビルドの版。空なら "0"。</param>
        /// <param name="maxMembers">作るパーティに入れる人数 (<see cref="PartyLobby.MinMaxMembers"/> 〜 <see cref="PartyLobby.MaxMaxMembers"/>)。</param>
        /// <param name="pollIntervalSeconds">ロビーとリーダーの属性を読み直す間隔 (秒。0 より大きいこと)。</param>
        /// <param name="log">ログの 1 行を受け取る。</param>
        public PartySession(ILobbyBackend backend, string version, int maxMembers, float pollIntervalSeconds, Action<string> log)
        {
            if (!PartyLobby.IsValidMaxMembers(maxMembers))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxMembers), maxMembers, "must be " + PartyLobby.MinMaxMembers + "-" + PartyLobby.MaxMaxMembers);
            }

            if (!(pollIntervalSeconds > 0f) || float.IsInfinity(pollIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(pollIntervalSeconds), pollIntervalSeconds, "must be a positive finite number");
            }

            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _version = version;
            _maxMembers = maxMembers;
            _pollIntervalSeconds = pollIntervalSeconds;
            _backend.LoggedOut += HandleLoggedOut;
        }

        private enum LoginStep
        {
            None,
            LoggingIn,
            LoggedIn,

            /// <summary>ログインが切れた。同じサービスではログインし直せないので、セッションを作り直す。</summary>
            Expired,
        }

        /// <summary>状態が変わった。UI はこれを見せる。</summary>
        public event Action<PartyStatus> StatusChanged;

        /// <summary>メンバーのとき、リーダーが新しく入るサーバーを決めた。受け取った起動設定でそのサーバーにつなぐ。</summary>
        public event Action<NetLaunchOptions> ServerChosen;

        public PartyStatus Status => _status;

        /// <summary>パーティのロビーのバケット。</summary>
        public string BucketId => PartyLobby.BucketId(_version);

        /// <summary>試合につなぐときに <see cref="ConnectionExtras.PartyId"/> に載せる、入っているパーティの Id。入っていなければ null。</summary>
        public string PartyId =>
            _phase == PartyPhase.InParty && ConnectionExtras.IsValidField(_lobby.Id, ConnectionExtras.MaxPartyIdLength)
                ? _lobby.Id
                : null;

        public bool IsLeader => _phase == PartyPhase.InParty && IsSelf(_leader);

        /// <summary>
        /// 試合に入る人数。パーティに入っていれば最後に読んだメンバーの数、入っていなければ自分 1 人。
        /// リーダーはこれを <see cref="QuickMatchSearch.Begin(float, int, Action{NetLaunchOptions}, Action{string})"/> に渡し、全員が入れるサーバーを探す。
        /// </summary>
        public int MatchPlayers => _phase == PartyPhase.InParty ? Math.Max(1, _lobby.MemberCount) : 1;

        /// <summary>パーティを作って入り、リーダーになる。結果は <see cref="Status"/> で知らせる。</summary>
        /// <exception cref="InvalidOperationException">もうパーティに入っている・入ろうとしている。</exception>
        public void Create()
        {
            Begin(null);
        }

        /// <summary>
        /// 招待のコード (仲間の <see cref="PartyStatus.PartyId"/>) のパーティに入る。結果は <see cref="Status"/> で知らせ、
        /// コードが使えなければ <see cref="PartyError.InvalidCode"/> にする。前後の空白は取り除く。
        /// </summary>
        /// <exception cref="InvalidOperationException">もうパーティに入っている・入ろうとしている。</exception>
        public void Join(string inviteCode)
        {
            ThrowIfDisposed();
            ThrowIfBusy();
            var code = inviteCode?.Trim();
            if (!ConnectionExtras.IsValidField(code, ConnectionExtras.MaxPartyIdLength))
            {
                _log(PartyLog.Failed(PartyError.InvalidCode, "invite code '" + inviteCode + "' is not 1-"
                    + ConnectionExtras.MaxPartyIdLength + " characters of [0-9A-Za-z_-]"));
                _error = PartyError.InvalidCode;
                Publish();
                return;
            }

            Begin(code);
        }

        /// <summary>パーティから出る。作っている・入ろうとしている途中なら、入れた後に出る。入っていなければ何もしない。</summary>
        public void Leave()
        {
            ThrowIfDisposed();
            switch (_phase)
            {
                case PartyPhase.Joining when _requesting:
                    _leaveWhenJoined = true;
                    _phase = PartyPhase.Leaving;
                    Publish();
                    break;
                case PartyPhase.Joining:
                    _generation++;
                    Reset(PartyError.None);
                    break;
                case PartyPhase.InParty:
                    StartLeave();
                    break;
            }
        }

        /// <summary>リーダーのとき、試合を探し始めたことをメンバーに見せる。リーダーでなければ何もせず false。</summary>
        public bool BeginSearch()
        {
            return WriteLeaderState(PartyState.Searching, null);
        }

        /// <summary>
        /// リーダーのとき、入るサーバーを決めてメンバーに知らせる。同じサーバーを選び直してもメンバーはまたつなぐ。
        /// リーダーでなければ何もせず false。
        /// </summary>
        /// <param name="options">リーダーがつなぐ起動設定 (クイックマッチが返したもの)。UDP のクライアントであること。</param>
        /// <exception cref="ArgumentException">UDP のクライアントの起動設定でない。</exception>
        public bool ChooseServer(NetLaunchOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.Mode != NetLaunchMode.Client || options.Transport != NetTransportKind.Udp)
            {
                throw new ArgumentException("the party can only share a UDP client connection, not " + options, nameof(options));
            }

            return WriteLeaderState(PartyState.InMatch, options.Endpoint);
        }

        /// <summary>リーダーのとき、試合を出た・探すのをやめたことをメンバーに見せる。リーダーでなければ何もせず false。</summary>
        public bool ReturnToIdle()
        {
            return WriteLeaderState(PartyState.Idle, null);
        }

        /// <summary>extras のパーティの欄を、入っているパーティ (<see cref="PartyId"/>) にした欄を返す。</summary>
        public ConnectionExtras WithParty(ConnectionExtras extras)
        {
            if (extras == null)
            {
                throw new ArgumentNullException(nameof(extras));
            }

            return new ConnectionExtras(PartyId, extras.JoinCode, extras.SkillRating, extras.ProductUserId);
        }

        /// <summary>サービスを進め、間を置いてロビーとリーダーの属性を読み直す。毎フレーム呼ぶ。</summary>
        /// <param name="now">今の時刻 (秒)。</param>
        public void Tick(float now)
        {
            if (_disposed)
            {
                return;
            }

            _now = now;
            _backend.Tick();
            if (_phase != PartyPhase.InParty || _polling || now < _nextPoll)
            {
                return;
            }

            Poll();
        }

        /// <summary>入っているパーティから出る要求だけ出して、バックエンドを閉じる。以後のコールバックは呼ばない。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_phase == PartyPhase.InParty)
            {
                _backend.LeaveMemberLobby(_lobby.Id, _ => { });
            }

            _disposed = true;
            _backend.LoggedOut -= HandleLoggedOut;
            _backend.Dispose();
        }

        private void Begin(string joinCode)
        {
            ThrowIfDisposed();
            ThrowIfBusy();
            _generation++;
            _phase = PartyPhase.Joining;
            _joinCode = joinCode;
            _error = PartyError.None;
            _leaveWhenJoined = false;
            Publish();
            switch (_login)
            {
                case LoginStep.None:
                    _login = LoginStep.LoggingIn;
                    _log(PartyLog.LoggingIn());
                    _backend.Login(Once<string>(HandleLoggedIn));
                    break;
                case LoginStep.LoggedIn:
                    Request();
                    break;
                case LoginStep.Expired:
                    Fail(PartyError.LoggedOut, "the lobby service logged out earlier; create a new party session");
                    break;
            }
        }

        private void HandleLoggedIn(string userId, string error)
        {
            if (_login != LoginStep.LoggingIn)
            {
                return;
            }

            if (error == null && string.IsNullOrWhiteSpace(userId))
            {
                error = "the lobby service returned no user id";
            }

            if (error != null)
            {
                _login = LoginStep.None;
                if (_phase == PartyPhase.Joining)
                {
                    Fail(PartyError.LoginFailed, "EOS login failed: " + error);
                }

                return;
            }

            _login = LoginStep.LoggedIn;
            _userId = userId;
            _log(PartyLog.LoggedIn(userId));
            if (_phase == PartyPhase.Joining)
            {
                Request();
                return;
            }

            Publish();
        }

        private void Request()
        {
            _requesting = true;
            var generation = _generation;
            if (_joinCode == null)
            {
                _log(PartyLog.Creating(BucketId, _maxMembers));
                _backend.CreateMemberLobby(
                    BucketId, _maxMembers, PartyLobby.LobbyAttributes(_version), Once<LobbyInfo>(generation, HandleEntered));
            }
            else
            {
                _log(PartyLog.Joining(_joinCode));
                _backend.JoinMemberLobby(_joinCode, Once<LobbyInfo>(generation, HandleEntered));
            }
        }

        private void HandleEntered(LobbyInfo lobby, string error)
        {
            _requesting = false;
            var creating = _joinCode == null;
            if (error == null && lobby == null)
            {
                error = "the lobby service returned no lobby";
            }

            if (error != null)
            {
                if (_leaveWhenJoined)
                {
                    _log(PartyLog.Left(_joinCode ?? "-", error));
                    Reset(PartyError.None);
                    return;
                }

                var failure = creating ? PartyError.CreateFailed : PartyError.JoinFailed;
                if (LobbyBackendErrors.IsNotSupported(error))
                {
                    failure = PartyError.Unavailable;
                }

                Fail(failure, (creating ? "creating a party failed: " : "joining party " + _joinCode + " failed: ") + error);
                return;
            }

            _lobby = lobby;
            if (_leaveWhenJoined)
            {
                StartLeave();
                return;
            }

            if (!PartyLobby.IsSameVersion(lobby, _version))
            {
                lobby.TryGetText(PartyLobby.VersionKey, out var lobbyVersion);
                _backend.LeaveMemberLobby(lobby.Id, _ => { });
                Fail(PartyError.VersionMismatch, "party " + lobby.Id + " is for version " + lobbyVersion + ", not " + _version);
                return;
            }

            _phase = PartyPhase.InParty;
            _leader = PartyLobby.Leader(lobby);
            _state = PartyState.Idle;
            _baselineLeader = false;
            _nextPoll = _now;
            _log(PartyLog.Joined(lobby, _leader));
            Publish();
        }

        private void StartLeave()
        {
            _generation++;
            var generation = _generation;
            var partyId = _lobby.Id;
            _phase = PartyPhase.Leaving;
            _pendingWrite = null;
            _writing = false;
            _polling = false;
            _log(PartyLog.Leaving(partyId));
            Publish();
            _backend.LeaveMemberLobby(partyId, Once(generation, error =>
            {
                _log(PartyLog.Left(partyId, error));
                Reset(PartyError.None);
            }));
        }

        private void Poll()
        {
            _polling = true;
            _nextPoll = _now + _pollIntervalSeconds;
            FlushWrite();
            if (_phase != PartyPhase.InParty)
            {
                return;
            }

            _backend.ReadMemberLobby(_lobby.Id, Once<LobbyInfo>(_generation, HandleLobbyRead));
        }

        private void HandleLobbyRead(LobbyInfo lobby, string error)
        {
            if (error != null)
            {
                _polling = false;
                RetryOrGiveUp("reading the party", error);
                return;
            }

            if (lobby == null || !lobby.HasMember(_userId))
            {
                Fail(PartyError.Removed, "no longer a member of party " + _lobby.Id);
                return;
            }

            ApplyLobby(lobby);
            var leader = _leader;
            if (leader == null || IsSelf(leader))
            {
                _polling = false;
                return;
            }

            _backend.ReadMemberAttributes(
                lobby.Id, leader, Once<IReadOnlyList<LobbyAttribute>>(_generation, (attributes, readError) =>
                    HandleLeaderRead(leader, attributes, readError)));
        }

        private void ApplyLobby(LobbyInfo lobby)
        {
            if (!SameMembers(_lobby, lobby))
            {
                _log(PartyLog.MembersChanged(lobby));
            }

            _lobby = lobby;
            var leader = PartyLobby.Leader(lobby);
            if (!string.Equals(leader, _leader, StringComparison.OrdinalIgnoreCase))
            {
                _log(PartyLog.LeaderChanged(lobby.Id, _leader, leader));
                _leader = leader;
                _state = PartyState.Idle;
                _baselineLeader = true;
                _reportedUnreadable = false;
                _pendingWrite = null;
                if (IsSelf(leader))
                {
                    // 前にリーダーだったときの古い決定が属性に残っていると、メンバーがそのサーバーへ入り直してしまう
                    _pendingWrite = PartyLobby.LeaderAttributes(PartyState.Idle, null, _match);
                    FlushWrite();
                }
            }

            Publish();
        }

        private void HandleLeaderRead(string leader, IReadOnlyList<LobbyAttribute> attributes, string error)
        {
            _polling = false;
            if (error != null)
            {
                RetryOrGiveUp("reading the leader", error);
                return;
            }

            if (!string.Equals(leader, _leader, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!PartyLobby.TryReadLeader(attributes ?? Array.Empty<LobbyAttribute>(), out var state, out var endpoint, out var match))
            {
                if (!_reportedUnreadable)
                {
                    _reportedUnreadable = true;
                    _log(PartyLog.UnreadableLeader(_lobby.Id, leader));
                }

                return;
            }

            _reportedUnreadable = false;
            if (state != _state || (state == PartyState.InMatch && match != _followedMatch))
            {
                _log(PartyLog.LeaderState(_lobby.Id, state, endpoint, match));
            }

            _state = state;
            if (_baselineLeader)
            {
                // リーダーが替わった直後に読んだ決定は、新しいリーダーが前に書いた古いものかもしれないので追わない
                _baselineLeader = false;
                _followedLeader = leader;
                _followedMatch = state == PartyState.InMatch ? match : (long?)null;
                Publish();
                return;
            }

            var alreadyFollowed = string.Equals(_followedLeader, leader, StringComparison.OrdinalIgnoreCase) && _followedMatch == match;
            if (state != PartyState.InMatch || alreadyFollowed)
            {
                Publish();
                return;
            }

            _followedLeader = leader;
            _followedMatch = match;
            _match = Math.Max(_match, match);
            _log(PartyLog.Following(_lobby.Id, leader, endpoint, match));
            Publish();
            ServerChosen?.Invoke(PartyLobby.ConnectOptions(endpoint));
        }

        private bool WriteLeaderState(PartyState state, string endpoint)
        {
            ThrowIfDisposed();
            if (!IsLeader)
            {
                return false;
            }

            if (state == PartyState.InMatch)
            {
                _match++;
            }

            _pendingWrite = PartyLobby.LeaderAttributes(state, endpoint, _match);
            _state = state;
            _log(PartyLog.LeaderState(_lobby.Id, state, endpoint, _match));
            Publish();
            FlushWrite();
            return true;
        }

        /// <summary>リーダーの属性の書き込みを 1 つずつ出す。失敗したら次に読み直すときに出し直す。</summary>
        private void FlushWrite()
        {
            if (_writing || _pendingWrite == null || _phase != PartyPhase.InParty)
            {
                return;
            }

            var attributes = _pendingWrite;
            _writing = true;
            _backend.SetMemberAttributes(_lobby.Id, attributes, Once(_generation, error =>
            {
                _writing = false;
                if (error != null)
                {
                    RetryOrGiveUp("writing the leader state", error);
                    return;
                }

                if (ReferenceEquals(_pendingWrite, attributes))
                {
                    _pendingWrite = null;
                    return;
                }

                FlushWrite();
            }));
        }

        private void RetryOrGiveUp(string what, string error)
        {
            if (LobbyBackendErrors.IsNotSupported(error))
            {
                Fail(PartyError.Unavailable, what + " is not supported by the lobby service");
                return;
            }

            _log(PartyLog.Retrying(what, _lobby.Id, error));
        }

        private void HandleLoggedOut(string reason)
        {
            if (_disposed)
            {
                return;
            }

            _login = LoginStep.Expired;
            _userId = null;
            if (_phase != PartyPhase.None)
            {
                Fail(PartyError.LoggedOut, "logged out: " + reason);
                return;
            }

            Publish();
        }

        private void Fail(PartyError error, string reason)
        {
            _log(PartyLog.Failed(error, reason));
            _generation++;
            Reset(error);
        }

        private void Reset(PartyError error)
        {
            _phase = PartyPhase.None;
            _error = error;
            _joinCode = null;
            _requesting = false;
            _leaveWhenJoined = false;
            _lobby = null;
            _leader = null;
            _state = PartyState.Idle;
            _match = 0;
            _pendingWrite = null;
            _writing = false;
            _followedLeader = null;
            _followedMatch = null;
            _baselineLeader = false;
            _reportedUnreadable = false;
            _polling = false;
            Publish();
        }

        private void Publish()
        {
            var status = _phase == PartyPhase.None
                ? new PartyStatus(PartyPhase.None, null, _userId, 0, Array.Empty<string>(), null, PartyState.Idle, _error)
                : new PartyStatus(
                    _phase,
                    _lobby?.Id,
                    _userId,
                    _lobby?.MaxMembers ?? _maxMembers,
                    _lobby?.Members ?? Array.Empty<string>(),
                    _leader,
                    _state,
                    _error);
            if (status.Equals(_status))
            {
                return;
            }

            _status = status;
            StatusChanged?.Invoke(status);
        }

        private bool IsSelf(string userId)
        {
            return _userId != null && string.Equals(userId, _userId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameMembers(LobbyInfo before, LobbyInfo after)
        {
            if (before == null || before.MemberCount != after.MemberCount)
            {
                return false;
            }

            for (var i = 0; i < before.MemberCount; i++)
            {
                if (!string.Equals(before.Members[i], after.Members[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PartySession));
            }
        }

        private void ThrowIfBusy()
        {
            if (_phase != PartyPhase.None)
            {
                throw new InvalidOperationException("the party session is already " + _phase);
            }
        }

        /// <summary>Dispose 後と 2 回目以降の完了を捨てるコールバックにする。ログインはパーティをまたぐので世代を見ない。</summary>
        private Action<T, string> Once<T>(Action<T, string> handler)
        {
            var called = false;
            return (result, error) =>
            {
                if (called || _disposed)
                {
                    return;
                }

                called = true;
                handler(result, error);
            };
        }

        /// <summary>Dispose 後・2 回目以降・出た後や入り直した後 (世代が変わった後) の完了を捨てるコールバックにする。</summary>
        private Action<T, string> Once<T>(int generation, Action<T, string> handler)
        {
            var inner = Once(handler);
            return (result, error) =>
            {
                if (generation == _generation)
                {
                    inner(result, error);
                }
            };
        }

        private Action<string> Once(int generation, Action<string> handler)
        {
            var called = false;
            return error =>
            {
                if (called || _disposed || generation != _generation)
                {
                    return;
                }

                called = true;
                handler(error);
            };
        }
    }
}
