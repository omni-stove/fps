using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.NetV2.Tests
{
    public sealed class PartySessionTests
    {
        private const string _a = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _b = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _c = "cccccccccccccccccccccccccccccccc";
        private const string _version = "0.3.0";
        private const string _rejoinKey = "0123456789abcdef0123456789abcdef";

        private FakeLobbyService _service;
        private List<string> _log;
        private List<PartySession> _sessions;
        private Dictionary<PartySession, FakePartyBackend> _backends;
        private Dictionary<PartySession, List<NetLaunchOptions>> _chosen;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _service = new FakeLobbyService();
            _log = new List<string>();
            _sessions = new List<PartySession>();
            _backends = new Dictionary<PartySession, FakePartyBackend>();
            _chosen = new Dictionary<PartySession, List<NetLaunchOptions>>();
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var session in _sessions)
            {
                session.Dispose();
            }
        }

        /// <summary>1 秒ごとに読み直すセッション。サーバーが決まった知らせは <see cref="_chosen"/> に貯める。</summary>
        private PartySession Session(string userId, int maxMembers = PartyLobby.DefaultMaxMembers)
        {
            var backend = new FakePartyBackend(_service, userId);
            var session = new PartySession(backend, _version, maxMembers, 1f, _log.Add);
            var chosen = new List<NetLaunchOptions>();
            session.ServerChosen += chosen.Add;
            _sessions.Add(session);
            _backends.Add(session, backend);
            _chosen.Add(session, chosen);
            return session;
        }

        /// <summary>時刻を進めずに、どのセッションも要求と完了の連なりを終えるまで進める。</summary>
        private void Settle()
        {
            for (var round = 0; round < 4; round++)
            {
                foreach (var session in _sessions)
                {
                    session.Tick(_now);
                }
            }
        }

        /// <summary>読み直す間隔ぶん時刻を進めて、どのセッションも読み直させる。</summary>
        private void Advance()
        {
            _now += 1f;
            Settle();
        }

        private PartySession CreateParty(string userId, int maxMembers = PartyLobby.DefaultMaxMembers)
        {
            var session = Session(userId, maxMembers);
            session.Create();
            Settle();
            return session;
        }

        private PartySession JoinParty(string userId, PartySession leader)
        {
            var session = Session(userId);
            session.Join(leader.Status.PartyId);
            Settle();
            return session;
        }

        private static NetLaunchOptions Server(string address, ushort port)
        {
            return new NetLaunchOptions(NetLaunchMode.Client, address, port);
        }

        [Test]
        public void CreateMakesAPartyAndLeadsIt()
        {
            var session = Session(_a);
            var statuses = new List<PartyStatus>();
            session.StatusChanged += statuses.Add;

            session.Create();
            Assert.That(session.Status.Phase, Is.EqualTo(PartyPhase.Joining));
            Settle();
            Advance();

            var lobby = _service.Only();
            var status = session.Status;
            Assert.That(status.Phase, Is.EqualTo(PartyPhase.InParty));
            Assert.That(status.PartyId, Is.EqualTo(lobby.Id));
            Assert.That(session.PartyId, Is.EqualTo(lobby.Id));
            Assert.That(status.LocalUserId, Is.EqualTo(_a));
            Assert.That(status.LeaderUserId, Is.EqualTo(_a));
            Assert.That(status.IsLeader, Is.True);
            Assert.That(session.IsLeader, Is.True);
            Assert.That(status.Members, Is.EqualTo(new[] { _a }));
            Assert.That(status.MaxMembers, Is.EqualTo(4));
            Assert.That(status.State, Is.EqualTo(PartyState.Idle));
            Assert.That(status.Error, Is.EqualTo(PartyError.None));

            Assert.That(lobby.Bucket, Is.EqualTo("shooter-party:0.3.0"));
            Assert.That(lobby.MaxMembers, Is.EqualTo(4));
            Assert.That(lobby.Snapshot().TryGetText("version", out var version), Is.True);
            Assert.That(version, Is.EqualTo("0.3.0"));

            Assert.That(statuses.Count, Is.EqualTo(2), "joining, then in the party; reading an unchanged party publishes nothing");
            Assert.That(statuses[0].Phase, Is.EqualTo(PartyPhase.Joining));
            Assert.That(statuses[1].Phase, Is.EqualTo(PartyPhase.InParty));
            Assert.That(_log, Has.Some.StartsWith("[Party] joined id=" + lobby.Id + " leader=" + _a));
        }

        [Test]
        public void JoiningWithTheInviteCodeShowsEveryMember()
        {
            var leader = CreateParty(_a);
            var member = JoinParty(_b, leader);

            Assert.That(member.Status.Phase, Is.EqualTo(PartyPhase.InParty));
            Assert.That(member.Status.PartyId, Is.EqualTo(leader.Status.PartyId));
            Assert.That(member.Status.LeaderUserId, Is.EqualTo(_a));
            Assert.That(member.Status.IsLeader, Is.False);
            Assert.That(member.Status.Members, Is.EqualTo(new[] { _a, _b }));
            Assert.That(leader.Status.Members, Is.EqualTo(new[] { _a }), "the leader sees the new member on its next read");

            Advance();

            Assert.That(leader.Status.Members, Is.EqualTo(new[] { _a, _b }));
            Assert.That(leader.Status.IsLeader, Is.True);
        }

        [Test]
        public void MatchPlayersCountsThePartyTheLeaderLastRead()
        {
            var leader = Session(_a);
            Assert.That(leader.MatchPlayers, Is.EqualTo(1), "alone before making a party");

            leader.Create();
            Assert.That(leader.MatchPlayers, Is.EqualTo(1), "alone while the party is being made");
            Settle();
            Assert.That(leader.MatchPlayers, Is.EqualTo(1));

            var second = JoinParty(_b, leader);
            var third = JoinParty(_c, leader);
            Assert.That(third.MatchPlayers, Is.EqualTo(3));
            Assert.That(leader.MatchPlayers, Is.EqualTo(1), "the leader sees the new members on its next read");

            Advance();
            Assert.That(leader.MatchPlayers, Is.EqualTo(3));
            Assert.That(second.MatchPlayers, Is.EqualTo(3));

            third.Leave();
            Settle();
            Advance();
            Assert.That(third.MatchPlayers, Is.EqualTo(1));
            Assert.That(leader.MatchPlayers, Is.EqualTo(2));

            leader.Leave();
            Settle();
            Assert.That(leader.MatchPlayers, Is.EqualTo(1));
        }

        [Test]
        public void JoinTrimsTheCodeAndRejectsAMalformedOne()
        {
            var leader = CreateParty(_a);
            var member = Session(_b);

            member.Join("not a code!");
            Assert.That(member.Status.Phase, Is.EqualTo(PartyPhase.None));
            Assert.That(member.Status.Error, Is.EqualTo(PartyError.InvalidCode));
            Assert.That(_backends[member].Logins, Is.Zero);

            member.Join("  " + leader.Status.PartyId + "\n");
            Settle();

            Assert.That(member.Status.Phase, Is.EqualTo(PartyPhase.InParty));
            Assert.That(member.Status.Error, Is.EqualTo(PartyError.None));
        }

        [Test]
        public void JoinFailsForAMissingParty()
        {
            var member = Session(_b);

            member.Join("missing-party");
            Settle();

            Assert.That(member.Status.Phase, Is.EqualTo(PartyPhase.None));
            Assert.That(member.Status.Error, Is.EqualTo(PartyError.JoinFailed));
            Assert.That(member.PartyId, Is.Null);
            Assert.That(_log, Has.Some.EqualTo("[Party] JoinFailed: joining party missing-party failed: NotFound"));
        }

        [Test]
        public void JoinFailsWhenThePartyIsFull()
        {
            var leader = CreateParty(_a, 2);
            JoinParty(_b, leader);
            var late = JoinParty(_c, leader);

            Assert.That(late.Status.Error, Is.EqualTo(PartyError.JoinFailed));
            Assert.That(_service.Only().Members, Is.EqualTo(new[] { _a, _b }));
        }

        [Test]
        public void JoinRefusesAPartyOfAnotherVersion()
        {
            var old = _service.Create("shooter-party:0.2.0", 4, new[] { LobbyAttribute.OfText("version", "0.2.0") }, _a);
            var member = Session(_b);

            member.Join(old.Id);
            Settle();

            Assert.That(member.Status.Error, Is.EqualTo(PartyError.VersionMismatch));
            Assert.That(old.Members, Is.EqualTo(new[] { _a }), "the member leaves the party it joined");
        }

        [Test]
        public void MembersFollowTheServerTheLeaderChooses()
        {
            var leader = CreateParty(_a);
            var member = JoinParty(_b, leader);
            var partyId = leader.Status.PartyId;

            Assert.That(leader.BeginSearch(), Is.True);
            Assert.That(leader.Status.State, Is.EqualTo(PartyState.Searching));
            Advance();
            Assert.That(member.Status.State, Is.EqualTo(PartyState.Searching));
            Assert.That(_chosen[member], Is.Empty);

            Assert.That(leader.ChooseServer(Server("203.0.113.2", 7790)), Is.True);
            Assert.That(leader.Status.State, Is.EqualTo(PartyState.InMatch));
            Advance();

            Assert.That(_chosen[member].Count, Is.EqualTo(1));
            var options = _chosen[member][0];
            Assert.That(options.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(options.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(options.Address, Is.EqualTo("203.0.113.2"));
            Assert.That(options.Port, Is.EqualTo(7790));
            Assert.That(member.Status.State, Is.EqualTo(PartyState.InMatch));
            Assert.That(_chosen[leader], Is.Empty, "the leader connects by itself");
            Assert.That(_log, Has.Some.EqualTo("[Party] following id=" + partyId + " leader=" + _a + " server=203.0.113.2:7790 match=1"));

            Advance();
            Advance();
            Assert.That(_chosen[member].Count, Is.EqualTo(1), "the same choice is followed once");

            Assert.That(leader.ReturnToIdle(), Is.True);
            Assert.That(leader.ChooseServer(Server("203.0.113.2", 7790)), Is.True);
            Advance();

            Assert.That(_chosen[member].Count, Is.EqualTo(2), "picking the same server again is a new match");
        }

        [Test]
        public void OnlyTheLeaderWritesTheState()
        {
            var leader = CreateParty(_a);
            var member = JoinParty(_b, leader);

            Assert.That(member.BeginSearch(), Is.False);
            Assert.That(member.ChooseServer(Server("203.0.113.2", 7790)), Is.False);
            Assert.That(member.ReturnToIdle(), Is.False);
            Assert.That(_backends[member].Writes, Is.Zero);
            Assert.Throws<ArgumentException>(() =>
                leader.ChooseServer(new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, 7777)));
            Assert.Throws<ArgumentNullException>(() => leader.ChooseServer(null));
            Assert.That(Session(_c).ChooseServer(Server("203.0.113.2", 7790)), Is.False, "not in a party");
        }

        [Test]
        public void AMemberJoiningMidMatchFollowsTheLeader()
        {
            var leader = CreateParty(_a);
            leader.ChooseServer(Server("203.0.113.5", 7777));
            Settle();

            var member = JoinParty(_b, leader);

            Assert.That(_chosen[member].Count, Is.EqualTo(1));
            Assert.That(_chosen[member][0].Address, Is.EqualTo("203.0.113.5"));
        }

        [Test]
        public void WhenTheLeaderLeavesTheNextMemberLeads()
        {
            var leader = CreateParty(_a);
            var second = JoinParty(_b, leader);
            var third = JoinParty(_c, leader);
            var partyId = leader.Status.PartyId;
            leader.ChooseServer(Server("203.0.113.2", 7790));
            Advance();
            Assert.That(_chosen[second].Count, Is.EqualTo(1));
            Assert.That(_chosen[third].Count, Is.EqualTo(1));

            leader.Leave();
            Assert.That(leader.Status.Phase, Is.EqualTo(PartyPhase.Leaving));
            Settle();
            Assert.That(leader.Status.Phase, Is.EqualTo(PartyPhase.None));
            Assert.That(leader.Status.Error, Is.EqualTo(PartyError.None));
            Assert.That(leader.PartyId, Is.Null);

            Advance();
            Advance();

            Assert.That(second.Status.IsLeader, Is.True);
            Assert.That(second.Status.Members, Is.EqualTo(new[] { _b, _c }));
            Assert.That(second.Status.State, Is.EqualTo(PartyState.Idle));
            Assert.That(third.Status.LeaderUserId, Is.EqualTo(_b));
            Assert.That(third.Status.State, Is.EqualTo(PartyState.Idle));
            Assert.That(_chosen[third].Count, Is.EqualTo(1), "the new leader's take-over is not a new match");
            Assert.That(_log, Has.Some.EqualTo("[Party] leader id=" + partyId + " " + _a + " -> " + _b));

            Assert.That(second.ChooseServer(Server("203.0.113.9", 7777)), Is.True);
            Advance();

            Assert.That(_chosen[third].Count, Is.EqualTo(2));
            Assert.That(_chosen[third][1].Address, Is.EqualTo("203.0.113.9"));
            Assert.That(_chosen[second].Count, Is.EqualTo(1));
        }

        [Test]
        public void WithoutAHandOverTheFirstRemainingMemberLeads()
        {
            _service.HandsOverOwnership = false;
            var leader = CreateParty(_a);
            var second = JoinParty(_b, leader);
            var third = JoinParty(_c, leader);

            leader.Leave();
            Settle();
            Advance();

            Assert.That(_service.Only().Owner, Is.EqualTo(_a), "the service has not handed the party over");
            Assert.That(second.Status.IsLeader, Is.True);
            Assert.That(third.Status.LeaderUserId, Is.EqualTo(_b));
            Assert.That(second.BeginSearch(), Is.True);
            Advance();
            Assert.That(third.Status.State, Is.EqualTo(PartyState.Searching));
        }

        [Test]
        public void AReturningLeaderDoesNotResendItsOldChoice()
        {
            var first = CreateParty(_a);
            var second = JoinParty(_b, first);
            var third = JoinParty(_c, first);
            var lobby = _service.Only();

            lobby.Owner = _b;
            Advance();
            Advance();
            second.ChooseServer(Server("203.0.113.2", 7790));
            Advance();
            Assert.That(_chosen[third].Count, Is.EqualTo(1));

            lobby.Owner = _a;
            Advance();
            Advance();

            // b の古い決定 (match=1) が属性に残っているうちに、c だけが読む
            lobby.Owner = _b;
            _now += 1f;
            for (var round = 0; round < 4; round++)
            {
                third.Tick(_now);
            }

            Assert.That(third.Status.LeaderUserId, Is.EqualTo(_b));
            Assert.That(_chosen[third].Count, Is.EqualTo(1));

            Advance();
            Advance();
            Assert.That(_chosen[third].Count, Is.EqualTo(1));
            Assert.That(third.Status.State, Is.EqualTo(PartyState.Idle), "the returning leader writes idle");

            second.ChooseServer(Server("203.0.113.3", 7777));
            Advance();
            Assert.That(_chosen[third].Count, Is.EqualTo(2));
            Assert.That(_chosen[third][1].Address, Is.EqualTo("203.0.113.3"));
        }

        [Test]
        public void AFailedLeaderWriteIsSentAgain()
        {
            var leader = CreateParty(_a);
            var member = JoinParty(_b, leader);
            _backends[leader].FailWrites = 1;

            leader.ChooseServer(Server("203.0.113.2", 7790));
            Settle();
            Assert.That(_log, Has.Some.EqualTo("[Party] writing the leader state failed id=party-1: TooManyRequests; retrying"));
            Assert.That(_backends[leader].Writes, Is.EqualTo(1));
            Assert.That(_service.Only().MemberAttributes.ContainsKey(_a), Is.False);

            Advance();
            Assert.That(_backends[leader].Writes, Is.EqualTo(2), "the leader writes again when it reads the party again");
            Assert.That(_chosen[member].Count, Is.EqualTo(1));
        }

        [Test]
        public void ThePartyIdGoesIntoTheConnectionPayload()
        {
            var leader = CreateParty(_a);
            var member = Session(_b);
            var extras = new ConnectionExtras(null, "code1", 1200, _b);
            Assert.That(member.WithParty(extras).PartyId, Is.Null);

            member.Join(leader.Status.PartyId);
            Settle();
            var payload = ConnectionPayload.Encode(_version, _rejoinKey, member.WithParty(extras));

            Assert.That(ConnectionPayload.TryDecode(payload, out _, out _, out var decoded), Is.True);
            Assert.That(decoded.PartyId, Is.EqualTo(leader.Status.PartyId));
            Assert.That(decoded.JoinCode, Is.EqualTo("code1"));
            Assert.That(decoded.SkillRating, Is.EqualTo(1200));
            Assert.That(decoded.ProductUserId, Is.EqualTo(_b));
            Assert.Throws<ArgumentNullException>(() => member.WithParty(null));
        }

        [Test]
        public void LeavingWhileCreatingLeavesOnceThePartyExists()
        {
            var session = Session(_a);
            session.Create();
            session.Tick(0f);

            session.Leave();
            Assert.That(session.Status.Phase, Is.EqualTo(PartyPhase.Leaving));
            Settle();

            Assert.That(session.Status.Phase, Is.EqualTo(PartyPhase.None));
            Assert.That(session.Status.Error, Is.EqualTo(PartyError.None));
            Assert.That(_service.Lobbies, Is.Empty, "the last member left, so the party is gone");
        }

        [Test]
        public void LeavingBeforeTheLoginEndsDropsTheRequest()
        {
            var session = Session(_a);
            session.Create();
            session.Leave();
            Settle();

            Assert.That(session.Status.Phase, Is.EqualTo(PartyPhase.None));
            Assert.That(session.Status.LocalUserId, Is.EqualTo(_a));
            Assert.That(_service.Lobbies, Is.Empty);

            session.Create();
            Settle();
            Assert.That(session.Status.Phase, Is.EqualTo(PartyPhase.InParty));
            Assert.That(_backends[session].Logins, Is.EqualTo(1), "the login carries over to the next party");
        }

        [Test]
        public void ARemovedMemberDropsTheParty()
        {
            var leader = CreateParty(_a);
            var member = JoinParty(_b, leader);

            _service.Only().Members.Remove(_b);
            Advance();

            Assert.That(member.Status.Phase, Is.EqualTo(PartyPhase.None));
            Assert.That(member.Status.Error, Is.EqualTo(PartyError.Removed));
            Assert.That(leader.Status.Members, Is.EqualTo(new[] { _a }));
        }

        [Test]
        public void LoggingOutDropsThePartyForGood()
        {
            var leader = CreateParty(_a);
            _backends[leader].LogOut("expired");

            Assert.That(leader.Status.Phase, Is.EqualTo(PartyPhase.None));
            Assert.That(leader.Status.Error, Is.EqualTo(PartyError.LoggedOut));
            Assert.That(leader.Status.LocalUserId, Is.Null);

            leader.Create();
            Settle();
            Assert.That(leader.Status.Error, Is.EqualTo(PartyError.LoggedOut));
            Assert.That(_backends[leader].Logins, Is.EqualTo(1));
        }

        [Test]
        public void TheLoginFailureIsReported()
        {
            var session = Session(_a);
            _backends[session].LoginError = "InvalidCredentials";

            session.Create();
            Settle();

            Assert.That(session.Status.Error, Is.EqualTo(PartyError.LoginFailed));
            Assert.That(_log, Has.Some.EqualTo("[Party] LoginFailed: EOS login failed: InvalidCredentials"));
        }

        [Test]
        public void AServiceWithoutPartiesGivesUp()
        {
            var session = Session(_a);
            _backends[session].Unsupported = true;

            session.Create();
            Settle();

            Assert.That(session.Status.Phase, Is.EqualTo(PartyPhase.None));
            Assert.That(session.Status.Error, Is.EqualTo(PartyError.Unavailable));
        }

        [Test]
        public void DisposeLeavesAndClosesTheBackend()
        {
            var leader = CreateParty(_a);
            var member = JoinParty(_b, leader);
            var backend = _backends[member];

            member.Dispose();
            member.Dispose();
            member.Tick(5f);

            Assert.That(backend.Disposed, Is.EqualTo(1));
            Assert.That(_service.Only().Members, Is.EqualTo(new[] { _a }));
            Assert.Throws<ObjectDisposedException>(() => member.Create());
        }

        [Test]
        public void CreateAndJoinWaitForThePartyToEnd()
        {
            var leader = CreateParty(_a);

            Assert.Throws<InvalidOperationException>(() => leader.Create());
            Assert.Throws<InvalidOperationException>(() => leader.Join("other"));
        }

        [Test]
        public void TheConstructorChecksItsArguments()
        {
            var backend = new FakePartyBackend(_service, _a);

            Assert.Throws<ArgumentNullException>(() => new PartySession(null, _version, _log.Add));
            Assert.Throws<ArgumentNullException>(() => new PartySession(backend, _version, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PartySession(backend, _version, 1, 1f, _log.Add));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PartySession(backend, _version, 17, 1f, _log.Add));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PartySession(backend, _version, 4, 0f, _log.Add));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PartySession(backend, _version, 4, float.NaN, _log.Add));
        }

        /// <summary>メンバー用ロビーを覚えておく、偽のロビーのサービス。どのユーザーのバックエンドも同じものを見る。</summary>
        private sealed class FakeLobbyService
        {
            private int _created;

            public Dictionary<string, FakeLobby> Lobbies { get; } = new Dictionary<string, FakeLobby>();

            /// <summary>オーナーが出たとき、残っている最初のメンバーをオーナーにするか (EOS と同じ)。</summary>
            public bool HandsOverOwnership { get; set; } = true;

            public FakeLobby Create(string bucket, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, string owner)
            {
                _created++;
                var lobby = new FakeLobby("party-" + _created, bucket, maxMembers, attributes, owner);
                Lobbies.Add(lobby.Id, lobby);
                return lobby;
            }

            public FakeLobby Only()
            {
                Assert.That(Lobbies.Count, Is.EqualTo(1));
                foreach (var lobby in Lobbies.Values)
                {
                    return lobby;
                }

                return null;
            }

            public void Leave(FakeLobby lobby, string userId)
            {
                lobby.Members.Remove(userId);
                lobby.MemberAttributes.Remove(userId);
                if (lobby.Members.Count == 0)
                {
                    Lobbies.Remove(lobby.Id);
                    return;
                }

                if (lobby.Owner == userId && HandsOverOwnership)
                {
                    lobby.Owner = lobby.Members[0];
                }
            }
        }

        private sealed class FakeLobby
        {
            public FakeLobby(string id, string bucket, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, string owner)
            {
                Id = id;
                Bucket = bucket;
                MaxMembers = maxMembers;
                Attributes = new List<LobbyAttribute>(attributes);
                Owner = owner;
                Members.Add(owner);
            }

            public string Id { get; }

            public string Bucket { get; }

            public int MaxMembers { get; }

            public List<LobbyAttribute> Attributes { get; }

            public string Owner { get; set; }

            public List<string> Members { get; } = new List<string>();

            public Dictionary<string, Dictionary<string, LobbyAttribute>> MemberAttributes { get; } =
                new Dictionary<string, Dictionary<string, LobbyAttribute>>();

            public LobbyInfo Snapshot()
            {
                return new LobbyInfo(Id, Owner, MaxMembers, Members.ToArray(), Attributes);
            }
        }

        /// <summary>
        /// 1 人のユーザーのバックエンド。要求はすぐサービスに反映し、完了のコールバックは次の <see cref="Tick"/> で呼ぶ。
        /// </summary>
        private sealed class FakePartyBackend : ILobbyBackend
        {
            private readonly FakeLobbyService _service;
            private readonly string _userId;
            private readonly List<Action> _pending = new List<Action>();
            private bool _disposed;

            public FakePartyBackend(FakeLobbyService service, string userId)
            {
                _service = service;
                _userId = userId;
            }

            public event Action<string> LoggedOut;

            public int Logins { get; private set; }

            public int Writes { get; private set; }

            public int Disposed { get; private set; }

            public string LoginError { get; set; }

            /// <summary>あと何回、属性の書き込みを失敗させるか。</summary>
            public int FailWrites { get; set; }

            /// <summary>パーティの要求をどれも <see cref="LobbyBackendErrors.NotSupported"/> で返すか。</summary>
            public bool Unsupported { get; set; }

            public void Tick()
            {
                if (_disposed)
                {
                    return;
                }

                var due = _pending.ToArray();
                _pending.Clear();
                foreach (var complete in due)
                {
                    complete();
                }
            }

            public void Login(Action<string, string> done)
            {
                Logins++;
                var error = LoginError;
                Later(() => done(error == null ? _userId : null, error));
            }

            public void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done)
            {
                Later(() => done(null, LobbyBackendErrors.NotSupported));
            }

            public void CreateMemberLobby(
                string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done)
            {
                if (Unsupported)
                {
                    Later(() => done(null, LobbyBackendErrors.NotSupported));
                    return;
                }

                var snapshot = _service.Create(bucketId, maxMembers, attributes, _userId).Snapshot();
                Later(() => done(snapshot, null));
            }

            public void JoinMemberLobby(string lobbyId, Action<LobbyInfo, string> done)
            {
                if (!_service.Lobbies.TryGetValue(lobbyId, out var lobby))
                {
                    Later(() => done(null, "NotFound"));
                    return;
                }

                if (lobby.Members.Count >= lobby.MaxMembers)
                {
                    Later(() => done(null, "LobbyTooLarge"));
                    return;
                }

                lobby.Members.Add(_userId);
                var snapshot = lobby.Snapshot();
                Later(() => done(snapshot, null));
            }

            public void LeaveMemberLobby(string lobbyId, Action<string> done)
            {
                if (_service.Lobbies.TryGetValue(lobbyId, out var lobby) && lobby.Members.Contains(_userId))
                {
                    _service.Leave(lobby, _userId);
                }

                Later(() => done(null));
            }

            public void ReadMemberLobby(string lobbyId, Action<LobbyInfo, string> done)
            {
                var snapshot = _service.Lobbies.TryGetValue(lobbyId, out var lobby) && lobby.Members.Contains(_userId)
                    ? lobby.Snapshot()
                    : null;
                Later(() => done(snapshot, null));
            }

            public void SetMemberAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
            {
                Writes++;
                if (FailWrites > 0)
                {
                    FailWrites--;
                    Later(() => done("TooManyRequests"));
                    return;
                }

                if (!_service.Lobbies.TryGetValue(lobbyId, out var lobby) || !lobby.Members.Contains(_userId))
                {
                    Later(() => done("NotFound"));
                    return;
                }

                if (!lobby.MemberAttributes.TryGetValue(_userId, out var mine))
                {
                    mine = new Dictionary<string, LobbyAttribute>();
                    lobby.MemberAttributes.Add(_userId, mine);
                }

                foreach (var attribute in attributes)
                {
                    mine[attribute.Key] = attribute;
                }

                Later(() => done(null));
            }

            public void ReadMemberAttributes(string lobbyId, string userId, Action<IReadOnlyList<LobbyAttribute>, string> done)
            {
                if (!_service.Lobbies.TryGetValue(lobbyId, out var lobby))
                {
                    Later(() => done(null, "NotFound"));
                    return;
                }

                IReadOnlyList<LobbyAttribute> attributes = lobby.MemberAttributes.TryGetValue(userId, out var theirs)
                    ? new List<LobbyAttribute>(theirs.Values)
                    : new List<LobbyAttribute>();
                Later(() => done(attributes, null));
            }

            public void JoinMatchLobby(string lobbyId, Action<LobbyInfo, string> done)
            {
                Later(() => done(null, LobbyBackendErrors.NotSupported));
            }

            public void LeaveMatchLobby(string lobbyId, Action<string> done)
            {
                Later(() => done(LobbyBackendErrors.NotSupported));
            }

            public void Dispose()
            {
                Disposed++;
                _disposed = true;
                _pending.Clear();
            }

            public void LogOut(string reason)
            {
                LoggedOut?.Invoke(reason);
            }

            private void Later(Action complete)
            {
                _pending.Add(complete);
            }
        }
    }
}
