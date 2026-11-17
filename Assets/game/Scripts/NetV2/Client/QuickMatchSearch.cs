using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;

namespace Shooter.NetV2
{
    public sealed class QuickMatchSearch : IDisposable
    {
        public const string LogPrefix = "[Lobby] ";

        private readonly ILobbyBackend _backend;
        private readonly string _version;
        private readonly RetrySchedule _schedule;
        private readonly Action<string> _log;
        private Action<NetLaunchOptions> _ready;
        private Action<string> _failed;
        private Step _step;
        private int _partySize = 1;
        private float _now;
        private string _lastRetryReason;
        private bool _disposed;

        public QuickMatchSearch(ILobbyBackend backend, string version, Action<string> log)
            : this(backend, version,
                new RetrySchedule(LobbyLaunchOptions.QuickMatchIntervalSeconds, LobbyLaunchOptions.QuickMatchTimeoutSeconds), log)
        {
        }

        public QuickMatchSearch(ILobbyBackend backend, string version, RetrySchedule schedule, Action<string> log)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _version = version;
            _backend.LoggedOut += HandleLoggedOut;
        }

        private enum Step
        {
            Idle,
            LoggingIn,
            Searching,
            WaitingToSearch,
            Ready,
            Failed,
        }

        public string BucketId => ServerLobby.BucketId(_version);

        public bool IsFinished => _step == Step.Ready || _step == Step.Failed;

        public void Begin(float now, Action<NetLaunchOptions> ready, Action<string> failed)
        {
            Begin(now, 1, ready, failed);
        }

        public void Begin(float now, int partySize, Action<NetLaunchOptions> ready, Action<string> failed)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(QuickMatchSearch));
            }

            if (_step != Step.Idle)
            {
                throw new InvalidOperationException("the quick match search has already begun");
            }

            if (partySize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(partySize), partySize, "must be at least 1");
            }

            _ready = ready ?? throw new ArgumentNullException(nameof(ready));
            _failed = failed ?? throw new ArgumentNullException(nameof(failed));
            _partySize = partySize;
            _now = now;
            _schedule.Start(now);
            _step = Step.LoggingIn;
            _log(LogPrefix + "logging in to search " + BucketId
                + (partySize == 1 ? "" : " for a party of " + partySize.ToString(CultureInfo.InvariantCulture)));
            _backend.Login(Once<string>(HandleLoggedIn));
        }

        public void Tick(float now)
        {
            if (_disposed)
            {
                return;
            }

            _now = now;
            _backend.Tick();
            if (IsFinished || _step == Step.Idle)
            {
                return;
            }

            if (_schedule.IsExpired(now))
            {
                Fail("no quick match server found after " + _schedule.TimeoutSeconds.ToString(CultureInfo.InvariantCulture) + "s"
                    + (_lastRetryReason == null ? "" : " (" + _lastRetryReason + ")"));
                return;
            }

            if (_step == Step.WaitingToSearch && _schedule.IsDue(now))
            {
                Search();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
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

            _log(LogPrefix + "logged in with the device id user=" + userId);
            Search();
        }

        private void Search()
        {
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
                Retry("search failed: " + error);
                return;
            }

            var found = lobbies ?? Array.Empty<LobbyInfo>();
            _log(LogPrefix + "found " + found.Count.ToString(CultureInfo.InvariantCulture) + " lobby(s) in " + BucketId);
            var lobby = LobbySelector.PickServer(found, _version, _partySize);
            if (lobby == null)
            {
                Retry(LobbySelector.DescribeNoServer(found, _version, _partySize));
                return;
            }

            NetLaunchOptions options;
            try
            {
                options = ServerLobby.ConnectOptions(lobby);
            }
            catch (ArgumentException e)
            {
                Retry(e.Message);
                return;
            }

            lobby.TryGetText(ServerLobby.ServerKey, out var endpoint);
            lobby.TryGetNumber(ServerLobby.PlayersKey, out var players);
            _step = Step.Ready;
            _log(LogPrefix + "quick match picked id=" + lobby.Id + " server=" + endpoint
                + " players=" + players.ToString(CultureInfo.InvariantCulture) + "/" + ServerLobby.PlayerSlots(lobby).ToString(CultureInfo.InvariantCulture)
                + "; connecting " + options);
            _ready(options);
        }

        private void HandleLoggedOut(string reason)
        {
            if (_disposed || IsFinished || _step == Step.Idle)
            {
                return;
            }

            Fail("logged out: " + reason);
        }

        private void Retry(string reason)
        {
            _lastRetryReason = reason;
            _step = Step.WaitingToSearch;
            _schedule.Wait(_now);
            _log(LogPrefix + reason + "; searching again in " + _schedule.IntervalSeconds.ToString(CultureInfo.InvariantCulture) + "s");
        }

        private void Fail(string reason)
        {
            if (IsFinished)
            {
                return;
            }

            _step = Step.Failed;
            _failed(reason);
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
