using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    // 公開の専用サーバーを EOS のロビー検索で一覧にし、選んだものへの接続先を返す。
    // ロビーのサービスは一覧を開いている間だけ持ち、閉じたら手放す。入ったサーバーの試合ロビーはサービスごと結果に渡すので、その後の読み直しはサービスを作り直す。
    public sealed class ServerBrowser : IDisposable
    {
        private readonly Func<ILobbyBackend> _createBackend;
        private readonly IServerPinger _pinger;
        private readonly string _version;
        private readonly float _timeoutSeconds;
        private readonly Action<ServerBrowserStatus?> _publish;
        private readonly Action<string> _log;
        private readonly Dictionary<string, LobbyInfo> _lobbies = new Dictionary<string, LobbyInfo>(StringComparer.Ordinal);
        private readonly List<ServerListing> _listings = new List<ServerListing>();
        private readonly List<ILobbyBackend> _retired = new List<ILobbyBackend>();
        private ILobbyBackend _backend;
        private bool _loggedIn;
        private Step _step;
        private int _generation;
        private float _now;
        private float _deadline;
        private MatchLobbyJoin _lobbyJoin;
        private bool _joinsMatchLobby;
        private bool _disposed;

        public ServerBrowser(
            Func<ILobbyBackend> createBackend, IServerPinger pinger, string version, Action<ServerBrowserStatus?> publish, Action<string> log)
            : this(createBackend, pinger, version, LobbyLaunchOptions.ServerBrowserTimeoutSeconds, publish, log)
        {
        }

        // pinger が null なら ping を測らず、どの行も測れていないものとして出す。
        public ServerBrowser(
            Func<ILobbyBackend> createBackend,
            IServerPinger pinger,
            string version,
            float timeoutSeconds,
            Action<ServerBrowserStatus?> publish,
            Action<string> log)
        {
            _createBackend = createBackend ?? throw new ArgumentNullException(nameof(createBackend));
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            if (!(timeoutSeconds > 0f) || float.IsInfinity(timeoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "must be a positive finite number");
            }

            _pinger = pinger;
            _version = version;
            _timeoutSeconds = timeoutSeconds;
        }

        private enum Step
        {
            Closed,
            LoggingIn,
            Searching,
            Ready,
            Unavailable,
            Joining,
        }

        public string BucketId => ServerLobby.BucketId(_version);

        public ServerBrowserStatus? Status { get; private set; }

        public bool IsOpen => _step != Step.Closed;

        public bool IsLoading => _step == Step.LoggingIn || _step == Step.Searching;

        public bool IsJoining => _step == Step.Joining;

        // 入るサーバーのボイスの部屋 (試合ロビー) にも入るか。入れなくても接続はする (MatchLobbyJoin)。
        public bool JoinsMatchLobby
        {
            get => _joinsMatchLobby;
            set
            {
                if (IsJoining)
                {
                    throw new InvalidOperationException("set JoinsMatchLobby while no server is being joined");
                }

                _joinsMatchLobby = value;
            }
        }

        // 読み直しの間は前の行を出したまま Loading にする。行が消えて選んでいた場所を失わないため。
        public bool Refresh()
        {
            if (_disposed || IsLoading || IsJoining)
            {
                return false;
            }

            if (_backend == null && !TryCreateBackend())
            {
                return false;
            }

            _generation++;
            _pinger?.CancelAll();
            _deadline = _now + _timeoutSeconds;
            Publish(ServerBrowserStage.Loading);
            if (_loggedIn)
            {
                Search();
                return true;
            }

            _step = Step.LoggingIn;
            _log(ServerBrowserLog.LoggingIn(BucketId));
            var generation = _generation;
            _backend.Login(Once<string>(generation, HandleLoggedIn));
            return true;
        }

        public void Join(string serverId, int partySize, Action<ServerJoinResult> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (partySize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(partySize), partySize, "must be at least 1");
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ServerBrowser));
            }

            if (IsJoining)
            {
                NotJoined(serverId, ServerJoinOutcome.Failed, ServerBrowserLog.Busy(), done);
                return;
            }

            if (_step != Step.Ready || _backend == null)
            {
                NotJoined(serverId, ServerJoinOutcome.Failed, ServerBrowserLog.NotLoaded(), done);
                return;
            }

            var listing = Find(serverId);
            if (listing == null || !_lobbies.TryGetValue(listing.Id, out var lobby))
            {
                NotJoined(serverId, ServerJoinOutcome.NotFound, ServerBrowserLog.NotListed(), done);
                return;
            }

            // 一覧の人数は検索した時のものなので、ここで通ってもサーバーが満員で断ることはある (その理由は接続の切断で出る)
            if (!listing.CanTake(partySize))
            {
                NotJoined(serverId, ServerJoinOutcome.Full, ServerBrowserLog.NoRoom(listing.FreeSlots, partySize), done);
                return;
            }

            NetLaunchOptions options;
            try
            {
                options = ServerLobby.ConnectOptions(lobby);
            }
            catch (ArgumentException e)
            {
                NotJoined(serverId, ServerJoinOutcome.Failed, e.Message, done);
                return;
            }

            _log(ServerBrowserLog.Picked(listing));
            if (!_joinsMatchLobby)
            {
                done(ServerJoinResult.Joined(listing.Id, options, null));
                return;
            }

            _step = Step.Joining;
            _deadline = _now + _timeoutSeconds;
            var generation = _generation;
            var backend = _backend;
            _lobbyJoin = new MatchLobbyJoin(backend, _log);
            _lobbyJoin.Begin(lobby, matchLobby => HandleLobbyJoined(generation, backend, listing.Id, options, matchLobby, done));
        }

        public void Tick(float now)
        {
            if (_disposed)
            {
                return;
            }

            _now = now;
            DisposeRetired();
            _backend?.Tick();
            _pinger?.Tick();
            if (now < _deadline)
            {
                return;
            }

            if (IsLoading)
            {
                Fail(ServerBrowserLog.TimedOut(_timeoutSeconds, _step == Step.LoggingIn));
                return;
            }

            // サーバーはもう決まっているので、ロビーに入れないだけで失敗にせず、ボイス無しでつなぐ
            if (IsJoining)
            {
                _lobbyJoin.GiveUp(MatchLobbyLog.TimedOut(_timeoutSeconds));
            }
        }

        // 閉じた一覧のために検索や ping を続けず、ロビーのサービスも手放す。入る途中のものも捨てる。
        public void Close()
        {
            if (_disposed || (_step == Step.Closed && _backend == null && Status == null))
            {
                return;
            }

            _generation++;
            _lobbyJoin?.Cancel();
            _lobbyJoin = null;
            _pinger?.CancelAll();
            _step = Step.Closed;
            _lobbies.Clear();
            _listings.Clear();
            RetireBackend();
            Status = null;
            _publish(null);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Close();
            _disposed = true;
            _pinger?.Dispose();
            DisposeRetired();
        }

        private bool TryCreateBackend()
        {
            ILobbyBackend backend;
            try
            {
                backend = _createBackend();
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                Fail(ServerBrowserLog.Unavailable(e.Message));
                return false;
            }

            if (backend == null)
            {
                Fail(ServerBrowserLog.Unavailable("the EOS lobby factory returned no backend"));
                return false;
            }

            _backend = backend;
            _loggedIn = false;
            _backend.LoggedOut += HandleLoggedOut;
            return true;
        }

        private void HandleLoggedIn(string userId, string error)
        {
            if (_step != Step.LoggingIn)
            {
                return;
            }

            if (error == null && string.IsNullOrWhiteSpace(userId))
            {
                error = "the lobby service returned no user id";
            }

            if (error != null)
            {
                Fail("EOS login failed: " + error);
                return;
            }

            _loggedIn = true;
            _log(ServerBrowserLog.LoggedIn(userId));
            Search();
        }

        private void Search()
        {
            _step = Step.Searching;
            _log(ServerBrowserLog.Searching(BucketId));
            _backend.SearchLobbies(BucketId, LobbyLaunchOptions.MaxBrowserResults, Once<IReadOnlyList<LobbyInfo>>(_generation, HandleSearched));
        }

        private void HandleSearched(IReadOnlyList<LobbyInfo> lobbies, string error)
        {
            if (_step != Step.Searching)
            {
                return;
            }

            if (error != null)
            {
                Fail("search failed: " + error);
                return;
            }

            _lobbies.Clear();
            _listings.Clear();
            var found = 0;
            foreach (var lobby in lobbies ?? Array.Empty<LobbyInfo>())
            {
                if (lobby == null)
                {
                    continue;
                }

                found++;
                if (_lobbies.ContainsKey(lobby.Id) || !ServerListing.TryFrom(lobby, _version, out var listing))
                {
                    continue;
                }

                _lobbies.Add(lobby.Id, lobby);
                _listings.Add(listing);
            }

            _log(ServerBrowserLog.Searched(found, _listings.Count, BucketId));
            _step = Step.Ready;
            Publish(ServerBrowserStage.Ready);
            PingAll();
        }

        // 同じ機械で別のポートに立つサーバーは同じ宛先なので、宛先ごとに 1 回だけ測る。
        private void PingAll()
        {
            if (_pinger == null)
            {
                return;
            }

            var addresses = new List<string>();
            foreach (var listing in _listings)
            {
                if (!addresses.Contains(listing.Address))
                {
                    addresses.Add(listing.Address);
                }
            }

            var generation = _generation;
            foreach (var address in addresses)
            {
                var pinged = address;
                var called = false;
                _pinger.Ping(pinged, milliseconds =>
                {
                    if (called)
                    {
                        return;
                    }

                    called = true;
                    HandlePinged(generation, pinged, milliseconds);
                });
            }
        }

        private void HandlePinged(int generation, string address, int? milliseconds)
        {
            if (_disposed || generation != _generation || (_step != Step.Ready && _step != Step.Joining))
            {
                return;
            }

            var ping = milliseconds.HasValue && milliseconds.Value >= 0 ? milliseconds : null;
            var changed = false;
            for (var i = 0; i < _listings.Count; i++)
            {
                if (_listings[i].Address == address && _listings[i].PingMilliseconds != ping)
                {
                    _listings[i] = _listings[i].WithPing(ping);
                    changed = true;
                }
            }

            if (changed)
            {
                Publish(ServerBrowserStage.Ready);
            }
        }

        // 入れた試合ロビーはこのサービスを閉じるので、結果と一緒に渡し、一覧はサービスを手放す。
        // 諦めたときもサービスを閉じる。MatchLobbyJoin.GiveUp は遅れて入れたロビーから出るのをサービスを閉じる側に任せるため。
        private void HandleLobbyJoined(
            int generation, ILobbyBackend backend, string serverId, NetLaunchOptions options, MatchLobby matchLobby, Action<ServerJoinResult> done)
        {
            if (_disposed || generation != _generation || _step != Step.Joining)
            {
                matchLobby?.Dispose();
                return;
            }

            _lobbyJoin = null;
            if (ReferenceEquals(backend, _backend))
            {
                if (matchLobby != null)
                {
                    _backend.LoggedOut -= HandleLoggedOut;
                    _backend = null;
                    _loggedIn = false;
                }
                else
                {
                    RetireBackend();
                }
            }

            _step = _backend != null ? Step.Ready : Step.Closed;
            done(ServerJoinResult.Joined(serverId, options, matchLobby));
        }

        private void HandleLoggedOut(string reason)
        {
            _loggedIn = false;
            if (_disposed)
            {
                return;
            }

            if (IsLoading)
            {
                Fail("logged out: " + reason);
                return;
            }

            if (IsJoining)
            {
                _lobbyJoin.GiveUp("logged out: " + reason);
            }
        }

        private void Fail(string reason)
        {
            // ログイン中の失敗はサービスを手放す。EOS のサービスは 2 度目の Login を断るので、持ったままでは「更新」が効かなくなる。
            var wasLoggingIn = _step == Step.LoggingIn;
            _generation++;
            _pinger?.CancelAll();
            if (wasLoggingIn)
            {
                RetireBackend();
            }

            _step = Step.Unavailable;
            _lobbies.Clear();
            _listings.Clear();
            _log(ServerBrowserLog.Failed(reason));
            Publish(ServerBrowserStage.Unavailable);
        }

        private void NotJoined(string serverId, ServerJoinOutcome outcome, string reason, Action<ServerJoinResult> done)
        {
            _log(ServerBrowserLog.NotJoined(serverId, outcome, reason));
            done(ServerJoinResult.NotJoined(outcome, serverId, reason));
        }

        private ServerListing Find(string serverId)
        {
            if (serverId == null)
            {
                return null;
            }

            foreach (var listing in _listings)
            {
                if (string.Equals(listing.Id, serverId, StringComparison.Ordinal))
                {
                    return listing;
                }
            }

            return null;
        }

        private void Publish(ServerBrowserStage stage)
        {
            Status = new ServerBrowserStatus(stage, _listings);
            _publish(Status);
        }

        // 呼び戻しの中 (UI がその場で閉じるときを含む) でサービスを閉じないよう、次の Tick まで閉じずに置く。
        private void RetireBackend()
        {
            if (_backend == null)
            {
                return;
            }

            _backend.LoggedOut -= HandleLoggedOut;
            _retired.Add(_backend);
            _backend = null;
            _loggedIn = false;
        }

        private void DisposeRetired()
        {
            if (_retired.Count == 0)
            {
                return;
            }

            var retired = _retired.ToArray();
            _retired.Clear();
            foreach (var backend in retired)
            {
                backend.Dispose();
            }
        }

        private Action<T, string> Once<T>(int generation, Action<T, string> handler)
        {
            var called = false;
            return (result, error) =>
            {
                if (called || _disposed || generation != _generation)
                {
                    return;
                }

                called = true;
                handler(result, error);
            };
        }
    }
}
