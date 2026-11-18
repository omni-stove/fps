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
        private int? _skillRating;
        private float _now;
        private string _lastRetryReason;
        private bool _joinsMatchLobby;
        private string _preferredRegion = ControlSettings.AutoRegion;
        private MatchLobbyJoin _lobbyJoin;
        private NetLaunchOptions _picked;
        private MatchLobby _matchLobby;
        private bool _handedOver;
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
            JoiningLobby,
            Ready,
            Failed,
        }

        public string BucketId => ServerLobby.BucketId(_version);

        public bool IsFinished => _step == Step.Ready || _step == Step.Failed;

        public bool JoinsMatchLobby
        {
            get => _joinsMatchLobby;
            set
            {
                if (_step != Step.Idle)
                {
                    throw new InvalidOperationException("set JoinsMatchLobby before the quick match search begins");
                }

                _joinsMatchLobby = value;
            }
        }

        // 探している途中で替えると、同じ検索の中で選び方が変わってログと食い違うので、始める前だけ受ける。
        public string PreferredRegion
        {
            get => _preferredRegion;
            set
            {
                if (_step != Step.Idle)
                {
                    throw new InvalidOperationException("set PreferredRegion before the quick match search begins");
                }

                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "pass " + ControlSettings.AutoRegion + " to take any region");
                }

                if (!ControlSettings.IsValidRegion(value))
                {
                    throw new ArgumentException("'" + value + "' is not a region name", nameof(value));
                }

                _preferredRegion = value;
            }
        }

        public bool PrefersAnyRegion => _preferredRegion == ControlSettings.AutoRegion;

        // ready の引数で渡さないのは、ロビーに入らない呼び出し方 (JoinsMatchLobby が偽) の ready をそのまま使えるようにするため
        public MatchLobby TakeMatchLobby()
        {
            var lobby = _matchLobby;
            if (lobby != null)
            {
                _matchLobby = null;
                _handedOver = true;
                _backend.LoggedOut -= HandleLoggedOut;
            }

            return lobby;
        }

        public void Begin(float now, Action<NetLaunchOptions> ready, Action<string> failed)
        {
            Begin(now, 1, ready, failed);
        }

        public void Begin(float now, int partySize, Action<NetLaunchOptions> ready, Action<string> failed)
        {
            Begin(now, partySize, null, ready, failed);
        }

        public void Begin(float now, int partySize, int? skillRating, Action<NetLaunchOptions> ready, Action<string> failed)
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
            _skillRating = skillRating;
            _now = now;
            _schedule.Start(now);
            _step = Step.LoggingIn;
            _log(LogPrefix + "logging in to search " + BucketId
                + (partySize == 1 ? "" : " for a party of " + partySize.ToString(CultureInfo.InvariantCulture))
                + (skillRating.HasValue ? " near skill " + skillRating.Value.ToString(CultureInfo.InvariantCulture) : "")
                + (PrefersAnyRegion ? "" : " preferring region " + _preferredRegion));
            _backend.Login(Once<string>(HandleLoggedIn));
        }

        public void Tick(float now)
        {
            if (_disposed)
            {
                return;
            }

            _now = now;
            if (!_handedOver)
            {
                _backend.Tick();
            }

            if (IsFinished || _step == Step.Idle)
            {
                return;
            }

            // サーバーはもう決まっているので、ロビーに入れないだけで探すのをやめず、ボイス無しでつなぐ
            if (_step == Step.JoiningLobby)
            {
                if (_schedule.IsExpired(now))
                {
                    _lobbyJoin.GiveUp(MatchLobbyLog.TimedOut(_schedule.TimeoutSeconds));
                }

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
            _lobbyJoin?.Cancel();
            if (_handedOver)
            {
                return;
            }

            _backend.LoggedOut -= HandleLoggedOut;
            if (_matchLobby != null)
            {
                // ロビーはサービスと同じ物を閉じるので、サービスを 2 度閉じないようロビーの側から閉じる
                _matchLobby.Dispose();
                _matchLobby = null;
                return;
            }

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
            var lobby = LobbySelector.PickServer(found, _version, _partySize, _skillRating, _preferredRegion);
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
            _log(LogPrefix + "quick match picked id=" + lobby.Id + " server=" + endpoint
                + " players=" + players.ToString(CultureInfo.InvariantCulture) + "/" + ServerLobby.PlayerSlots(lobby).ToString(CultureInfo.InvariantCulture)
                + SkillText(lobby)
                + RegionText(lobby)
                + "; connecting " + options);
            if (!_joinsMatchLobby)
            {
                Ready(options);
                return;
            }

            _picked = options;
            _step = Step.JoiningLobby;
            _lobbyJoin = new MatchLobbyJoin(_backend, _log);
            _lobbyJoin.Begin(lobby, HandleLobbyJoined);
        }

        private void HandleLobbyJoined(MatchLobby lobby)
        {
            // 入れたロビーは同じサービスを閉じるので、ここで閉じずに捨て、この検索が閉じるときに出る
            if (_disposed || _step != Step.JoiningLobby)
            {
                return;
            }

            _matchLobby = lobby;
            Ready(_picked);
        }

        private void Ready(NetLaunchOptions options)
        {
            _step = Step.Ready;
            _ready(options);
        }

        private string SkillText(LobbyInfo lobby)
        {
            if (!_skillRating.HasValue)
            {
                return "";
            }

            var average = lobby.SkillAverage;
            return " skill=" + (average.HasValue ? average.Value.ToString(CultureInfo.InvariantCulture) : "-")
                + " (self " + _skillRating.Value.ToString(CultureInfo.InvariantCulture) + ")";
        }

        private string RegionText(LobbyInfo lobby)
        {
            var region = lobby.Region ?? "-";
            if (PrefersAnyRegion)
            {
                return " region=" + region;
            }

            return " region=" + region
                + (LobbySelector.IsInRegion(lobby, _preferredRegion) ? "" : " (none open in " + _preferredRegion + ")");
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
