using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    // QuickMatchSearch と違って探し直さないのは、コードの合うサーバーは 1 つで、無ければ待っても現れないことが多く、
    // 入れた人にすぐ理由を見せる方がよいため。
    public sealed class JoinCodeSearch : IDisposable
    {
        private readonly ILobbyBackend _backend;
        private readonly string _version;
        private readonly float _timeoutSeconds;
        private readonly Action<string> _log;
        private Action<JoinCodeResult> _done;
        private Step _step;
        private string _code;
        private int _partySize = 1;
        private float _deadline;
        private bool _joinsMatchLobby;
        private MatchLobbyJoin _lobbyJoin;
        private NetLaunchOptions _picked;
        private bool _handedOver;
        private bool _disposed;

        public JoinCodeSearch(ILobbyBackend backend, string version, Action<string> log)
            : this(backend, version, LobbyLaunchOptions.JoinCodeTimeoutSeconds, log)
        {
        }

        public JoinCodeSearch(ILobbyBackend backend, string version, float timeoutSeconds, Action<string> log)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            if (!(timeoutSeconds > 0f) || float.IsInfinity(timeoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "must be a positive finite number");
            }

            _version = version;
            _timeoutSeconds = timeoutSeconds;
            _backend.LoggedOut += HandleLoggedOut;
        }

        private enum Step
        {
            Idle,
            LoggingIn,
            Searching,
            JoiningLobby,
            Finished,
        }

        public string BucketId => ServerLobby.BucketId(_version);

        public bool IsFinished => _step == Step.Finished;

        public bool JoinsMatchLobby
        {
            get => _joinsMatchLobby;
            set
            {
                if (_step != Step.Idle)
                {
                    throw new InvalidOperationException("set JoinsMatchLobby before the join code search begins");
                }

                _joinsMatchLobby = value;
            }
        }

        public void Begin(float now, string code, Action<JoinCodeResult> done)
        {
            Begin(now, code, 1, done);
        }

        public void Begin(float now, string code, int partySize, Action<JoinCodeResult> done)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(JoinCodeSearch));
            }

            if (_step != Step.Idle)
            {
                throw new InvalidOperationException("the join code search has already begun");
            }

            if (partySize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(partySize), partySize, "must be at least 1");
            }

            _done = done ?? throw new ArgumentNullException(nameof(done));
            _partySize = partySize;
            _deadline = now + _timeoutSeconds;

            // 打ち間違いは UI の入力でふつうに起きるので、投げずに「見つからない」として返す
            if (!JoinCode.TryNormalize(code, out _code))
            {
                Finish(JoinCodeResult.NotJoined(JoinCodeOutcome.NotFound, JoinCodeLog.InvalidCode()));
                return;
            }

            _step = Step.LoggingIn;
            _log(JoinCodeLog.LoggingIn(BucketId, partySize));
            _backend.Login(Once<string>(HandleLoggedIn));
        }

        public void Tick(float now)
        {
            if (_disposed)
            {
                return;
            }

            if (!_handedOver)
            {
                _backend.Tick();
            }

            if (_step == Step.Idle || IsFinished || now < _deadline)
            {
                return;
            }

            // サーバーはもう決まっているので、ロビーに入れないだけで失敗にせず、ボイス無しでつなぐ
            if (_step == Step.JoiningLobby)
            {
                _lobbyJoin.GiveUp(MatchLobbyLog.TimedOut(_timeoutSeconds));
                return;
            }

            Fail(JoinCodeLog.TimedOut(_timeoutSeconds, _step == Step.LoggingIn));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lobbyJoin?.Cancel();
            if (_handedOver)
            {
                return;
            }

            _backend.LoggedOut -= HandleLoggedOut;
            _backend.Dispose();
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

            _log(JoinCodeLog.LoggedIn(userId));
            _step = Step.Searching;
            _backend.SearchLobbies(BucketId, LobbyLaunchOptions.MaxSearchResults, Once<IReadOnlyList<LobbyInfo>>(HandleSearched));
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

            var found = lobbies ?? Array.Empty<LobbyInfo>();
            var outcome = LobbySelector.PickByJoinCode(found, _code, _version, _partySize, out var lobby);
            _log(JoinCodeLog.Searched(found.Count, BucketId, outcome));
            if (outcome != JoinCodeOutcome.Joined)
            {
                Finish(JoinCodeResult.NotJoined(outcome, JoinCodeLog.NotJoinedReason(outcome, BucketId, _version, _partySize)));
                return;
            }

            NetLaunchOptions options;
            try
            {
                options = ServerLobby.ConnectOptions(lobby);
            }
            catch (ArgumentException e)
            {
                Fail(e.Message);
                return;
            }

            lobby.TryGetText(ServerLobby.ServerKey, out var endpoint);
            lobby.TryGetNumber(ServerLobby.PlayersKey, out var players);
            _log(JoinCodeLog.Picked(lobby.Id, endpoint, players, ServerLobby.PlayerSlots(lobby)));
            if (!_joinsMatchLobby)
            {
                Finish(JoinCodeResult.Joined(options, _code));
                return;
            }

            _picked = options;
            _step = Step.JoiningLobby;
            _lobbyJoin = new MatchLobbyJoin(_backend, _log);
            _lobbyJoin.Begin(lobby, HandleLobbyJoined);
        }

        // 入れたロビーは同じサービスを閉じるので、結果に載せないときは閉じずに捨て、この検索が閉じるときに出る
        private void HandleLobbyJoined(MatchLobby lobby)
        {
            if (_disposed || _step != Step.JoiningLobby)
            {
                return;
            }

            if (lobby != null)
            {
                _handedOver = true;
                _backend.LoggedOut -= HandleLoggedOut;
            }

            Finish(JoinCodeResult.Joined(_picked, _code, lobby));
        }

        private void HandleLoggedOut(string reason)
        {
            if (_disposed || IsFinished || _step == Step.Idle)
            {
                return;
            }

            if (_step == Step.JoiningLobby)
            {
                _lobbyJoin.GiveUp("logged out: " + reason);
                return;
            }

            Fail("logged out: " + reason);
        }

        private void Fail(string reason)
        {
            Finish(JoinCodeResult.NotJoined(JoinCodeOutcome.Failed, reason));
        }

        private void Finish(JoinCodeResult result)
        {
            if (IsFinished)
            {
                return;
            }

            _step = Step.Finished;
            if (result.Outcome != JoinCodeOutcome.Joined)
            {
                _log(JoinCodeLog.Finished(result.Outcome, result.Reason));
            }

            _done(result);
        }

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
    }
}
