using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.NetV2.Tests
{
    public sealed class ServerBrowserTests
    {
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _owner = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _version = "0.3.0";
        private const float _timeout = 10f;

        private List<FakeLobbyBackend> _backends;
        private FakePinger _pinger;
        private List<string> _log;
        private List<ServerBrowserStatus?> _published;
        private List<ServerJoinResult> _results;
        private ServerBrowser _browser;

        [SetUp]
        public void SetUp()
        {
            _backends = new List<FakeLobbyBackend>();
            _pinger = new FakePinger();
            _log = new List<string>();
            _published = new List<ServerBrowserStatus?>();
            _results = new List<ServerJoinResult>();
            _browser = new ServerBrowser(CreateBackend, _pinger, _version, _timeout, _published.Add, _log.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _browser.Dispose();
            _pinger.Dispose();
        }

        private FakeLobbyBackend Backend => _backends[_backends.Count - 1];

        private ServerBrowserStatus Last => _published[_published.Count - 1].Value;

        private ILobbyBackend CreateBackend()
        {
            var backend = new FakeLobbyBackend();
            _backends.Add(backend);
            return backend;
        }

        private static LobbyInfo Server(
            string id, string endpoint, int players, string region = null, string map = null, NetMatchSettings match = null, string version = _version)
        {
            return new LobbyInfo(
                id, _owner, ServerLobby.MaxMembers(NetMatchSettings.DefaultMaxPlayers), new[] { _owner },
                ServerLobby.Attributes(endpoint, version, players, match ?? NetMatchSettings.Default, null, region, map));
        }

        private void Load(params LobbyInfo[] lobbies)
        {
            Assert.That(_browser.Refresh(), Is.True);
            Backend.CompleteLogin(_self);
            Backend.CompleteSearch(lobbies);
        }

        private static List<string> Ids(IReadOnlyList<ServerListing> listings)
        {
            var ids = new List<string>();
            foreach (var listing in listings)
            {
                ids.Add(listing.Id);
            }

            return ids;
        }

        [Test]
        public void ListsThePublicServersOfThisVersionWithTheirRegionModeMapAndPlayers()
        {
            Assert.That(_browser.Refresh(), Is.True);
            Assert.That(Last.Stage, Is.EqualTo(ServerBrowserStage.Loading));
            Assert.That(Backend.Logins, Is.EqualTo(1));
            Assert.That(_browser.IsLoading, Is.True);

            Backend.CompleteLogin(_self);

            Assert.That(Backend.SearchedBucket, Is.EqualTo("shooter-server:0.3.0"));
            Assert.That(Backend.SearchedMax, Is.EqualTo(LobbyLaunchOptions.MaxBrowserResults));
            Backend.CompleteSearch(new List<LobbyInfo>
            {
                Server("one", "203.0.113.1:7777", 3, "europe", "harbor"),
                Server("two", "203.0.113.2:7777", 0, "asia", "quarry"),
                Server("private", "203.0.113.3:7777", 0, match: new NetMatchSettings(privateMatch: true, joinCode: "ABC234")),
                Server("old", "203.0.113.4:7777", 0, version: "0.2.0"),
                Server("full", "203.0.113.5:7777", NetMatchSettings.DefaultMaxPlayers),
                null,
            });

            Assert.That(_browser.IsLoading, Is.False);
            Assert.That(Last.Stage, Is.EqualTo(ServerBrowserStage.Ready));
            Assert.That(Ids(Last.Servers), Is.EqualTo(new[] { "one", "two", "full" }), "private and other-version servers cannot be joined");
            var one = Last.Find("one");
            Assert.That(one.Region, Is.EqualTo("europe"));
            Assert.That(one.Mode, Is.EqualTo("tdm"));
            Assert.That(one.Map, Is.EqualTo("harbor"));
            Assert.That(one.Players, Is.EqualTo(3));
            Assert.That(one.Capacity, Is.EqualTo(8));
            Assert.That(Last.Find("full").IsFull, Is.True);
            Assert.That(_browser.Status, Is.EqualTo(Last));
            Assert.That(_log, Has.Member("[Lobby] browser: found 5 lobby(s) in shooter-server:0.3.0, listing 3"));
        }

        [Test]
        public void EachAddressIsPingedOnceAndTheRowsShowTheResult()
        {
            Load(
                Server("one", "203.0.113.1:7777", 1),
                Server("two", "203.0.113.1:7778", 2),
                Server("three", "203.0.113.2:7777", 3));

            Assert.That(_pinger.Asked, Is.EqualTo(new[] { "203.0.113.1", "203.0.113.2" }));
            Assert.That(Last.Find("one").PingMilliseconds, Is.Null);

            _pinger.Answer("203.0.113.1", 35);

            Assert.That(Last.Find("one").PingMilliseconds, Is.EqualTo(35));
            Assert.That(Last.Find("two").PingMilliseconds, Is.EqualTo(35));
            Assert.That(Last.Find("three").PingMilliseconds, Is.Null);

            _pinger.Answer("203.0.113.2", null);

            Assert.That(Last.Find("three").PingMilliseconds, Is.Null, "an unanswered ping stays unmeasured");
        }

        [Test]
        public void RefreshKeepsTheOldRowsWhileSearchingAndDoesNotLogInAgain()
        {
            Load(Server("one", "203.0.113.1:7777", 1));
            _pinger.Answer("203.0.113.1", 20);

            Assert.That(_browser.Refresh(), Is.True);

            Assert.That(Last.Stage, Is.EqualTo(ServerBrowserStage.Loading));
            Assert.That(Ids(Last.Servers), Is.EqualTo(new[] { "one" }));
            Assert.That(Backend.Logins, Is.EqualTo(1));
            Assert.That(Backend.Searches, Is.EqualTo(2));
            Assert.That(_pinger.Cancels, Is.EqualTo(2), "pings of the old list are dropped");
            Assert.That(_browser.Refresh(), Is.False, "a search is already running");

            Backend.CompleteSearch(new List<LobbyInfo> { Server("two", "203.0.113.2:7777", 0) });

            Assert.That(Ids(Last.Servers), Is.EqualTo(new[] { "two" }));
            Assert.That(_backends, Has.Count.EqualTo(1));
        }

        [Test]
        public void ALateAnswerFromAnOldSearchIsIgnored()
        {
            Load(Server("one", "203.0.113.1:7777", 1));
            var stalePing = _pinger.Pending("203.0.113.1");
            _browser.Refresh();

            stalePing(99);

            Assert.That(Last.Stage, Is.EqualTo(ServerBrowserStage.Loading));
            Assert.That(Last.Find("one").PingMilliseconds, Is.Null);
        }

        [Test]
        public void ASearchErrorMakesTheListUnavailable()
        {
            _browser.Refresh();
            Backend.CompleteLogin(_self);
            Backend.CompleteSearch(null, "TimedOut");

            Assert.That(Last.Stage, Is.EqualTo(ServerBrowserStage.Unavailable));
            Assert.That(Last.Servers, Is.Empty);
            Assert.That(_log, Has.Member("[Lobby] browser: unavailable reason=search failed: TimedOut"));
            Assert.That(_browser.Refresh(), Is.True, "it can be tried again");
        }

        [Test]
        public void ALoginErrorMakesTheListUnavailable()
        {
            _browser.Refresh();
            Backend.CompleteLogin(null, "InvalidCredentials");

            Assert.That(Last.Stage, Is.EqualTo(ServerBrowserStage.Unavailable));
            Assert.That(Backend.Searches, Is.Zero);
            Assert.That(_log, Has.Member("[Lobby] browser: unavailable reason=EOS login failed: InvalidCredentials"));
        }

        [Test]
        public void NoAnswerFromTheLobbyServiceTimesOut()
        {
            _browser.Tick(1f);
            _browser.Refresh();

            _browser.Tick(10.9f);
            Assert.That(Last.Stage, Is.EqualTo(ServerBrowserStage.Loading));
            _browser.Tick(11f);

            Assert.That(Last.Stage, Is.EqualTo(ServerBrowserStage.Unavailable));
            Assert.That(_log, Has.Member("[Lobby] browser: unavailable reason=no answer from the lobby service after 10s while logging in"));

            Backend.CompleteLogin(_self);
            Assert.That(Backend.Searches, Is.Zero, "a late login does not search");
        }

        [Test]
        public void AMissingLobbyServiceIsUnavailable()
        {
            var browser = new ServerBrowser(() => null, null, _version, _published.Add, _log.Add);

            Assert.That(browser.Refresh(), Is.False);
            Assert.That(Last.Stage, Is.EqualTo(ServerBrowserStage.Unavailable));

            var throwing = new ServerBrowser(() => throw new InvalidOperationException("no EOS"), null, _version, _published.Add, _log.Add);
            Assert.That(throwing.Refresh(), Is.False);
            Assert.That(_log, Has.Member("[Lobby] browser: unavailable reason=the lobby service is not available: no EOS"));
        }

        [Test]
        public void WithoutAPingerEveryRowIsUnmeasured()
        {
            var browser = new ServerBrowser(CreateBackend, null, _version, _published.Add, _log.Add);
            browser.Refresh();
            Backend.CompleteLogin(_self);
            Backend.CompleteSearch(new List<LobbyInfo> { Server("one", "203.0.113.1:7777", 1) });

            Assert.That(Last.Find("one").PingMilliseconds, Is.Null);
            browser.Dispose();
        }

        [Test]
        public void JoiningAListedServerConnectsOverUdp()
        {
            Load(Server("one", "203.0.113.1:7790", 1));

            _browser.Join("one", 1, _results.Add);

            Assert.That(_results, Has.Count.EqualTo(1));
            var result = _results[0];
            Assert.That(result.Outcome, Is.EqualTo(ServerJoinOutcome.Joined));
            Assert.That(result.ServerId, Is.EqualTo("one"));
            Assert.That(result.Options.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(result.Options.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(result.Options.Address, Is.EqualTo("203.0.113.1"));
            Assert.That(result.Options.Port, Is.EqualTo(7790));
            Assert.That(result.MatchLobby, Is.Null);
        }

        [Test]
        public void JoiningAFullServerOrOneWithoutRoomForTheParty()
        {
            Load(Server("full", "203.0.113.1:7777", NetMatchSettings.DefaultMaxPlayers), Server("six", "203.0.113.2:7777", 6));

            _browser.Join("full", 1, _results.Add);
            _browser.Join("six", 3, _results.Add);
            _browser.Join("six", 2, _results.Add);

            Assert.That(_results[0].Outcome, Is.EqualTo(ServerJoinOutcome.Full));
            Assert.That(_results[0].Reason, Is.EqualTo("the server is full"));
            Assert.That(_results[1].Outcome, Is.EqualTo(ServerJoinOutcome.Full));
            Assert.That(_results[1].Reason, Is.EqualTo("the server has room for 2, not a party of 3"));
            Assert.That(_results[2].Outcome, Is.EqualTo(ServerJoinOutcome.Joined));
        }

        [Test]
        public void JoiningAServerNotInTheListOrBeforeLoading()
        {
            _browser.Join("one", 1, _results.Add);
            Assert.That(_results[0].Outcome, Is.EqualTo(ServerJoinOutcome.Failed));
            Assert.That(_results[0].Reason, Is.EqualTo("the server list has not been loaded"));

            Load(Server("one", "203.0.113.1:7777", 1), Server("private", "203.0.113.3:7777", 0, match: new NetMatchSettings(privateMatch: true)));

            _browser.Join("private", 1, _results.Add);
            _browser.Join(null, 1, _results.Add);

            Assert.That(_results[1].Outcome, Is.EqualTo(ServerJoinOutcome.NotFound));
            Assert.That(_results[2].Outcome, Is.EqualTo(ServerJoinOutcome.NotFound));
        }

        [Test]
        public void JoinsTheServersVoiceLobbyAndHandsTheServiceOver()
        {
            _browser.JoinsMatchLobby = true;
            Load(Server("one", "203.0.113.1:7777", 1));
            var first = Backend;

            _browser.Join("one", 1, _results.Add);

            Assert.That(_browser.IsJoining, Is.True);
            Assert.That(first.MatchLobbyJoins, Is.EqualTo(new[] { "one" }));
            Assert.That(_results, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => _browser.JoinsMatchLobby = false);
            Assert.That(_browser.Refresh(), Is.False, "no refresh while joining");
            _browser.Join("one", 1, _results.Add);
            Assert.That(_results[0].Reason, Is.EqualTo("a server is already being joined"));

            first.CompleteMatchLobbyJoin(Server("one", "203.0.113.1:7777", 1));

            var result = _results[1];
            Assert.That(result.Outcome, Is.EqualTo(ServerJoinOutcome.Joined));
            Assert.That(result.MatchLobby, Is.Not.Null);
            Assert.That(result.MatchLobby.LobbyId, Is.EqualTo("one"));
            Assert.That(_browser.IsJoining, Is.False);

            _browser.Close();
            _browser.Tick(1f);
            Assert.That(first.Disposed, Is.Zero, "the match lobby owns the service now");

            Assert.That(_browser.Refresh(), Is.True);
            Assert.That(_backends, Has.Count.EqualTo(2), "the next list uses a new service");
            result.MatchLobby.Dispose();
            Assert.That(first.Disposed, Is.EqualTo(1));
        }

        [Test]
        public void AVoiceLobbyThatDoesNotAnswerStillConnectsAtTheDeadline()
        {
            _browser.JoinsMatchLobby = true;
            Load(Server("one", "203.0.113.1:7777", 1));
            _browser.Tick(1f);
            _browser.Join("one", 1, _results.Add);

            _browser.Tick(10.9f);
            Assert.That(_results, Is.Empty);
            _browser.Tick(11f);

            Assert.That(_results[0].Outcome, Is.EqualTo(ServerJoinOutcome.Joined));
            Assert.That(_results[0].MatchLobby, Is.Null);
            Assert.That(_browser.IsJoining, Is.False);
            Assert.That(_browser.Refresh(), Is.True, "the service is still the browser's");
        }

        [Test]
        public void ALogOutWhileJoiningTheVoiceLobbyStillConnects()
        {
            _browser.JoinsMatchLobby = true;
            Load(Server("one", "203.0.113.1:7777", 1));
            _browser.Join("one", 1, _results.Add);

            Backend.LogOut("expired");

            Assert.That(_results[0].Outcome, Is.EqualTo(ServerJoinOutcome.Joined));
            Assert.That(_results[0].MatchLobby, Is.Null);

            _browser.Refresh();
            Assert.That(Backend.Logins, Is.EqualTo(2), "logging out means logging in again");
        }

        [Test]
        public void ALogOutWhileSearchingMakesTheListUnavailable()
        {
            _browser.Refresh();
            Backend.CompleteLogin(_self);

            Backend.LogOut("expired");

            Assert.That(Last.Stage, Is.EqualTo(ServerBrowserStage.Unavailable));
            Assert.That(_log, Has.Member("[Lobby] browser: unavailable reason=logged out: expired"));
        }

        [Test]
        public void ClosingDropsTheServiceOnTheNextTickAndClearsTheList()
        {
            Load(Server("one", "203.0.113.1:7777", 1));
            var first = Backend;

            _browser.Close();

            Assert.That(_published[_published.Count - 1], Is.Null);
            Assert.That(_browser.Status, Is.Null);
            Assert.That(_browser.IsOpen, Is.False);
            Assert.That(first.Disposed, Is.Zero, "not inside a callback that may come from the service");

            _browser.Tick(1f);

            Assert.That(first.Disposed, Is.EqualTo(1));
            Assert.That(first.Ticks, Is.Zero, "a closed service is not ticked");

            _browser.Refresh();
            Assert.That(_backends, Has.Count.EqualTo(2));
        }

        [Test]
        public void ClosingWhileSearchingIgnoresTheAnswer()
        {
            _browser.Refresh();
            var first = Backend;
            first.CompleteLogin(_self);
            _browser.Close();
            var published = _published.Count;

            first.CompleteSearch(new List<LobbyInfo> { Server("one", "203.0.113.1:7777", 1) });

            Assert.That(_published, Has.Count.EqualTo(published));
        }

        [Test]
        public void DisposingClosesThePingerAndTheService()
        {
            Load(Server("one", "203.0.113.1:7777", 1));
            var first = Backend;

            _browser.Dispose();
            _browser.Dispose();

            Assert.That(first.Disposed, Is.EqualTo(1));
            Assert.That(_pinger.Disposed, Is.True);
            Assert.That(_browser.Refresh(), Is.False);
            Assert.Throws<ObjectDisposedException>(() => _browser.Join("one", 1, _results.Add));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ServerBrowser(null, null, _version, _published.Add, _log.Add));
            Assert.Throws<ArgumentNullException>(() => new ServerBrowser(CreateBackend, null, _version, null, _log.Add));
            Assert.Throws<ArgumentNullException>(() => new ServerBrowser(CreateBackend, null, _version, _published.Add, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerBrowser(CreateBackend, null, _version, 0f, _published.Add, _log.Add));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ServerBrowser(CreateBackend, null, _version, float.PositiveInfinity, _published.Add, _log.Add));
            Assert.Throws<ArgumentNullException>(() => _browser.Join("one", 1, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _browser.Join("one", 0, _results.Add));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerJoinResult.NotJoined(ServerJoinOutcome.Joined, "one", "x"));
            Assert.Throws<ArgumentNullException>(() => ServerJoinResult.Joined("one", null, null));
        }

        private sealed class FakePinger : IServerPinger
        {
            private readonly Dictionary<string, Action<int?>> _pending = new Dictionary<string, Action<int?>>();

            public List<string> Asked { get; } = new List<string>();

            public int Cancels { get; private set; }

            public bool Disposed { get; private set; }

            public void Ping(string address, Action<int?> done)
            {
                Asked.Add(address);
                _pending[address] = done;
            }

            public Action<int?> Pending(string address)
            {
                return _pending[address];
            }

            public void Answer(string address, int? milliseconds)
            {
                var done = _pending[address];
                _pending.Remove(address);
                done(milliseconds);
            }

            public void Tick()
            {
            }

            public void CancelAll()
            {
                Cancels++;
                _pending.Clear();
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class FakeLobbyBackend : ILobbyBackend
        {
            private Action<string, string> _login;
            private Action<IReadOnlyList<LobbyInfo>, string> _search;
            private Action<LobbyInfo, string> _joinMatch;

            public event Action<string> LoggedOut;

            public event Action<string, string> MemberAttributesChanged
            {
                add { }
                remove { }
            }

            public int Ticks { get; private set; }

            public int Logins { get; private set; }

            public int Searches { get; private set; }

            public string SearchedBucket { get; private set; }

            public int SearchedMax { get; private set; }

            public int Disposed { get; private set; }

            public List<string> MatchLobbyJoins { get; } = new List<string>();

            public void Tick()
            {
                Ticks++;
            }

            public void Login(Action<string, string> done)
            {
                Logins++;
                _login = done;
            }

            public void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done)
            {
                Searches++;
                SearchedBucket = bucketId;
                SearchedMax = maxResults;
                _search = done;
            }

            public void CreateMemberLobby(
                string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done)
            {
                done(null, LobbyBackendErrors.NotSupported);
            }

            public void JoinMemberLobby(string lobbyId, Action<LobbyInfo, string> done)
            {
                done(null, LobbyBackendErrors.NotSupported);
            }

            public void LeaveMemberLobby(string lobbyId, Action<string> done)
            {
                done(LobbyBackendErrors.NotSupported);
            }

            public void ReadMemberLobby(string lobbyId, Action<LobbyInfo, string> done)
            {
                done(null, LobbyBackendErrors.NotSupported);
            }

            public void SetMemberAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
            {
                done(LobbyBackendErrors.NotSupported);
            }

            public void ReadMemberAttributes(string lobbyId, string userId, Action<IReadOnlyList<LobbyAttribute>, string> done)
            {
                done(null, LobbyBackendErrors.NotSupported);
            }

            public void JoinMatchLobby(string lobbyId, Action<LobbyInfo, string> done)
            {
                MatchLobbyJoins.Add(lobbyId);
                _joinMatch = done;
            }

            public void LeaveMatchLobby(string lobbyId, Action<string> done)
            {
                done(LobbyBackendErrors.NotSupported);
            }

            public void Dispose()
            {
                Disposed++;
            }

            public void CompleteLogin(string userId, string error = null)
            {
                _login(userId, error);
            }

            public void CompleteSearch(IReadOnlyList<LobbyInfo> lobbies, string error = null)
            {
                _search(lobbies, error);
            }

            public void CompleteMatchLobbyJoin(LobbyInfo lobby, string error = null)
            {
                _joinMatch(lobby, error);
            }

            public void LogOut(string reason)
            {
                LoggedOut?.Invoke(reason);
            }
        }
    }
}
