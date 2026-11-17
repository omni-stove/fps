using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.NetV2.Tests
{
    public sealed class MemberLobbiesTests
    {
        private const string _bucket = "shooter-party:0.3.0";
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _friend = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private FakeMemberLobbyApi _api;
        private List<string> _log;
        private List<string> _changes;
        private MemberLobbies _lobbies;

        [SetUp]
        public void SetUp()
        {
            _api = new FakeMemberLobbyApi();
            _log = new List<string>();
            _changes = new List<string>();
            _lobbies = new MemberLobbies(_api, _log.Add);
            _lobbies.MemberAttributesChanged += (lobbyId, userId) => _changes.Add(lobbyId + "/" + userId);
        }

        [TearDown]
        public void TearDown()
        {
            _lobbies.Dispose();
        }

        private static LobbyInfo Party(string id, params string[] members)
        {
            return new LobbyInfo(id, members[0], 4, members);
        }

        private static readonly string[] _updateMemberCalls = { "update-member id=L1 ready=1,team=a" };
        private static readonly string[] _joinedL1 = { "L1" };
        private static readonly string[] _joinWrongLog = { "[Lobby] member lobby join failed id=WRONG reason=not-found" };
        private static readonly string[] _joinCopyLeaveL2Calls = { "join id=L2", "copy id=L2", "leave id=L2" };
        private static readonly string[] _joinWrongCalls = { "join id=WRONG" };
        private static readonly string[] _copyL1Calls = { "copy id=L1" };
        private static readonly string[] _leaveL1Calls = { "leave id=L1" };
        private static readonly string[] _joinL2Calls = { "join id=L2" };
        private static readonly string[] _createJoinLeaveCalls =
        {
            "create bucket=shooter-party:0.3.0 max=4",
            "join id=L2",
            "leave id=L1",
        };

        private static readonly string[] _afterLateAnswersCalls =
        {
            "create bucket=shooter-party:0.3.0 max=4",
            "join id=L2",
            "leave id=L1",
            "leave id=L3",
            "leave id=L2",
        };

        private static LobbyAttribute[] ModeDuo()
        {
            return new[] { LobbyAttribute.OfText("mode", "duo") };
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Strings(params string[] values) => values;

        private void CreateL1()
        {
            _api.Lobbies["L1"] = Party("L1", _self);
            _lobbies.Create(_bucket, 4, Array.Empty<LobbyAttribute>(), (_, _) => { });
            _api.AnswerCreate("L1");
            _api.Calls.Clear();
        }

        [Test]
        public void CreatingMakesTheLobbyThenSetsItsAttributesThenReadsIt()
        {
            _api.Lobbies["L1"] = Party("L1", _self);
            LobbyInfo created = null;
            var failure = "not answered";
            _lobbies.Create(_bucket, 4, ModeDuo(), (lobby, error) =>
            {
                created = lobby;
                failure = error;
            });

            Assert.That(_api.Calls, Is.EqualTo(Strings("create bucket=shooter-party:0.3.0 max=4")));
            _api.AnswerCreate("L1");
            Assert.That(_api.Calls, Is.EqualTo(Strings("create bucket=shooter-party:0.3.0 max=4", "update-lobby id=L1 mode=duo")));
            Assert.That(failure, Is.EqualTo("not answered"), "the lobby is not ready until its attributes are set");

            _api.AnswerUpdate(null);
            Assert.That(_api.Calls, Is.EqualTo(Strings(
                "create bucket=shooter-party:0.3.0 max=4",
                "update-lobby id=L1 mode=duo",
                "copy id=L1")));
            Assert.That(failure, Is.Null);
            Assert.That(created.Id, Is.EqualTo("L1"));
            Assert.That(created.OwnerUserId, Is.EqualTo(_self));
            Assert.That(_lobbies.Joined, Is.EquivalentTo(Strings("L1")));
            Assert.That(_log, Is.EqualTo(Strings("[Lobby] member lobby created id=L1 bucket=shooter-party:0.3.0 members=1/4")));
        }

        [Test]
        public void CreatingWithoutAttributesSkipsTheUpdate()
        {
            _api.Lobbies["L1"] = Party("L1", _self);
            LobbyInfo created = null;
            _lobbies.Create(_bucket, 2, Array.Empty<LobbyAttribute>(), (lobby, _) => created = lobby);
            _api.AnswerCreate("L1");

            Assert.That(_api.Calls, Is.EqualTo(Strings("create bucket=shooter-party:0.3.0 max=2", "copy id=L1")));
            Assert.That(created.Id, Is.EqualTo("L1"));
        }

        [Test]
        public void AFailedCreateIsReturnedAndNothingIsJoined()
        {
            string failure = null;
            _lobbies.Create(_bucket, 4, ModeDuo(), (_, error) => failure = error);
            _api.AnswerCreate(null, "creating the EOS member lobby failed: TooManyRequests");

            Assert.That(failure, Is.EqualTo("creating the EOS member lobby failed: TooManyRequests"));
            Assert.That(_api.Calls, Is.EqualTo(Strings("create bucket=shooter-party:0.3.0 max=4")));
            Assert.That(_lobbies.Joined, Is.Empty);
            Assert.That(_log, Is.EqualTo(Strings(
                "[Lobby] member lobby create failed bucket=shooter-party:0.3.0 reason=creating the EOS member lobby failed: TooManyRequests")));
        }

        [Test]
        public void ACreatedLobbyWithoutAnIdIsAFailure()
        {
            string failure = null;
            _lobbies.Create(_bucket, 4, ModeDuo(), (_, error) => failure = error);
            _api.AnswerCreate(null);

            Assert.That(failure, Is.EqualTo("the lobby service created a lobby without an id"));
            Assert.That(_lobbies.Joined, Is.Empty);
        }

        [Test]
        public void AFailedAttributeUpdateLeavesTheNewLobby()
        {
            string failure = null;
            LobbyInfo created = null;
            _lobbies.Create(_bucket, 4, ModeDuo(), (lobby, error) =>
            {
                created = lobby;
                failure = error;
            });
            _api.AnswerCreate("L1");
            _api.AnswerUpdate("updating the EOS member lobby id=L1 failed: InvalidParameters");

            Assert.That(created, Is.Null);
            Assert.That(failure, Is.EqualTo("updating the EOS member lobby id=L1 failed: InvalidParameters"));
            Assert.That(_api.Calls, Is.EqualTo(Strings(
                "create bucket=shooter-party:0.3.0 max=4",
                "update-lobby id=L1 mode=duo",
                "leave id=L1")));
            Assert.That(_lobbies.Joined, Is.Empty);
            Assert.That(_log, Has.Member("[Lobby] member lobby abandoned id=L1 reason=updating the EOS member lobby id=L1 failed: InvalidParameters"));
        }

        [Test]
        public void AnUnreadableNewLobbyIsLeft()
        {
            _api.CopyError = "reading the EOS lobby id=L1 failed: NotFound";
            string failure = null;
            _lobbies.Create(_bucket, 4, Array.Empty<LobbyAttribute>(), (_, error) => failure = error);
            _api.AnswerCreate("L1");

            Assert.That(failure, Is.EqualTo("reading the EOS lobby id=L1 failed: NotFound"));
            Assert.That(_api.Calls, Is.EqualTo(Strings("create bucket=shooter-party:0.3.0 max=4", "copy id=L1", "leave id=L1")));
            Assert.That(_lobbies.Joined, Is.Empty);
        }

        [Test]
        public void TheAttributesAreCopiedWhenTheRequestIsMade()
        {
            var attributes = new List<LobbyAttribute> { LobbyAttribute.OfText("mode", "duo") };
            _lobbies.Create(_bucket, 4, attributes, (_, _) => { });
            attributes.Add(LobbyAttribute.OfNumber("late", 1));
            _api.AnswerCreate("L1");

            Assert.That(_api.Calls[1], Is.EqualTo("update-lobby id=L1 mode=duo"));
        }

        [Test]
        public void JoiningByCodeTrimsTheCodeThenJoinsThenReadsTheLobby()
        {
            _api.Lobbies["L2"] = Party("L2", _friend, _self);
            LobbyInfo joined = null;
            var failure = "not answered";
            _lobbies.Join("  L2 \n", (lobby, error) =>
            {
                joined = lobby;
                failure = error;
            });

            Assert.That(_api.Calls, Is.EqualTo(Strings("join id=L2")));
            _api.AnswerJoin(null);

            Assert.That(_api.Calls, Is.EqualTo(Strings("join id=L2", "copy id=L2")));
            Assert.That(failure, Is.Null);
            Assert.That(joined.OwnerUserId, Is.EqualTo(_friend));
            Assert.That(joined.Members, Is.EqualTo(new[] { _friend, _self }));
            Assert.That(_lobbies.Joined, Is.EquivalentTo(Strings("L2")));
            Assert.That(_log, Is.EqualTo(Strings("[Lobby] member lobby joined id=L2 members=2/4")));
        }

        [Test]
        public void AnUnknownCodeFailsWithNotFound()
        {
            string failure = null;
            _lobbies.Join("WRONG", (_, error) => failure = error);
            _api.AnswerJoin(LobbyBackendErrors.NotFound);

            Assert.That(LobbyBackendErrors.IsNotFound(failure), Is.True);
            Assert.That(_api.Calls, Is.EqualTo(_joinWrongCalls));
            Assert.That(_lobbies.Joined, Is.Empty);
            Assert.That(_log, Is.EqualTo(_joinWrongLog));
        }

        [Test]
        public void AnUnreadableJoinedLobbyIsLeft()
        {
            _api.CopyError = "reading the EOS lobby id=L2 failed: InvalidState";
            string failure = null;
            _lobbies.Join("L2", (_, error) => failure = error);
            _api.AnswerJoin(null);

            Assert.That(failure, Is.EqualTo("reading the EOS lobby id=L2 failed: InvalidState"));
            Assert.That(_api.Calls, Is.EqualTo(_joinCopyLeaveL2Calls));
            Assert.That(_lobbies.Joined, Is.Empty);
        }

        [Test]
        public void JoiningALobbyAlreadyJoinedReturnsItWithoutJoiningAgain()
        {
            CreateL1();
            LobbyInfo joined = null;
            _lobbies.Join("L1", (lobby, _) => joined = lobby);

            Assert.That(_api.Calls, Is.EqualTo(_copyL1Calls));
            Assert.That(joined.Id, Is.EqualTo("L1"));
        }

        [Test]
        public void LeavingForgetsTheLobby()
        {
            CreateL1();
            var failure = "not answered";
            _lobbies.Leave("L1", error => failure = error);

            Assert.That(_api.Calls, Is.EqualTo(_leaveL1Calls));
            Assert.That(_lobbies.Joined, Is.EquivalentTo(_joinedL1), "still joined until the service answers");
            _api.AnswerLeave(null);

            Assert.That(failure, Is.Null);
            Assert.That(_lobbies.Joined, Is.Empty);
            Assert.That(_log, Has.Member("[Lobby] member lobby left id=L1"));
        }

        [Test]
        public void LeavingALobbyThatIsGoneCountsAsLeft()
        {
            CreateL1();
            var failure = "not answered";
            _lobbies.Leave("L1", error => failure = error);
            _api.AnswerLeave(LobbyBackendErrors.NotFound);

            Assert.That(failure, Is.Null);
            Assert.That(_lobbies.Joined, Is.Empty);
        }

        [Test]
        public void AFailedLeaveKeepsTheLobby()
        {
            CreateL1();
            string failure = null;
            _lobbies.Leave("L1", error => failure = error);
            _api.AnswerLeave("leaving the EOS member lobby id=L1 failed: TimedOut");

            Assert.That(failure, Is.EqualTo("leaving the EOS member lobby id=L1 failed: TimedOut"));
            Assert.That(_lobbies.Joined, Is.EquivalentTo(_joinedL1));
            Assert.That(_log, Has.Member("[Lobby] member lobby leave failed id=L1 reason=leaving the EOS member lobby id=L1 failed: TimedOut"));
        }

        [Test]
        public void SettingMemberAttributesUpdatesOnlyTheMember()
        {
            CreateL1();
            var failure = "not answered";
            _lobbies.SetMemberAttributes("L1", new[] { LobbyAttribute.OfNumber("ready", 1), LobbyAttribute.OfText("team", "a") }, error => failure = error);

            Assert.That(_api.Calls, Is.EqualTo(_updateMemberCalls));
            _api.AnswerUpdate(null);
            Assert.That(failure, Is.Null);
        }

        [Test]
        public void AFailedMemberUpdateIsReturned()
        {
            CreateL1();
            string failure = null;
            _lobbies.SetMemberAttributes("L1", new[] { LobbyAttribute.OfNumber("ready", 1) }, error => failure = error);
            _api.AnswerUpdate(LobbyBackendErrors.NotFound);

            Assert.That(failure, Is.EqualTo(LobbyBackendErrors.NotFound));
        }

        [Test]
        public void SettingNoMemberAttributesSucceedsWithoutCallingTheService()
        {
            var failure = "not answered";
            _lobbies.SetMemberAttributes("L1", Array.Empty<LobbyAttribute>(), error => failure = error);

            Assert.That(failure, Is.Null);
            Assert.That(_api.Calls, Is.Empty);
        }

        [Test]
        public void ReadingMemberAttributesCopiesThem()
        {
            CreateL1();
            _api.MemberAttributes["L1/" + _friend] = new[] { LobbyAttribute.OfNumber("ready", 1) };
            IReadOnlyList<LobbyAttribute> read = null;
            var failure = "not answered";
            _lobbies.ReadMemberAttributes("L1", _friend, (attributes, error) =>
            {
                read = attributes;
                failure = error;
            });

            Assert.That(_api.Calls, Is.EqualTo(new[] { "copy-member id=L1 user=" + _friend }));
            Assert.That(failure, Is.Null);
            Assert.That(read, Has.Count.EqualTo(1));
            Assert.That(read[0].ToString(), Is.EqualTo("ready=1"));
        }

        [Test]
        public void AMemberWithoutAttributesReadsAsEmpty()
        {
            IReadOnlyList<LobbyAttribute> read = null;
            _lobbies.ReadMemberAttributes("L1", _friend, (attributes, _) => read = attributes);

            Assert.That(read, Is.Empty);
        }

        [Test]
        public void AFailedMemberReadIsReturned()
        {
            _api.CopyError = "reading the EOS member lobby id=L1 failed: InvalidState";
            IReadOnlyList<LobbyAttribute> read = Array.Empty<LobbyAttribute>();
            string failure = null;
            _lobbies.ReadMemberAttributes("L1", _friend, (attributes, error) =>
            {
                read = attributes;
                failure = error;
            });

            Assert.That(read, Is.Null);
            Assert.That(failure, Is.EqualTo("reading the EOS member lobby id=L1 failed: InvalidState"));
        }

        [Test]
        public void MemberUpdatesAreForwardedOnlyForJoinedLobbies()
        {
            CreateL1();
            _api.RaiseMemberUpdate("L1", _friend);
            _api.RaiseMemberUpdate("match-lobby", _friend);
            _api.RaiseMemberUpdate("L1", null);

            Assert.That(_changes, Is.EqualTo(new[] { "L1/" + _friend }));

            _lobbies.Leave("L1", _ => { });
            _api.AnswerLeave(null);
            _api.RaiseMemberUpdate("L1", _friend);
            Assert.That(_changes, Has.Count.EqualTo(1), "no updates after leaving");
        }

        [Test]
        public void LoggingOutForgetsTheLobbiesAndFailsPendingRequests()
        {
            CreateL1();
            string failure = null;
            _lobbies.Join("L2", (_, error) => failure = error);
            _lobbies.Forget();
            _api.AnswerJoin(null);

            Assert.That(failure, Is.EqualTo("the lobby was left or the login ended before the request finished"));
            Assert.That(_lobbies.Joined, Is.Empty);
            Assert.That(_api.Calls, Is.EqualTo(_joinL2Calls), "nothing to leave once the login is gone");

            _lobbies.Dispose();
            Assert.That(_api.Calls, Is.EqualTo(_joinL2Calls));
        }

        [Test]
        public void LeavingWhileTheAttributesAreSetFailsTheCreate()
        {
            _api.Lobbies["L1"] = Party("L1", _self);
            string failure = null;
            _lobbies.Create(_bucket, 4, ModeDuo(), (_, error) => failure = error);
            _api.AnswerCreate("L1");
            _lobbies.Leave("L1", _ => { });
            _api.AnswerLeave(null);
            _api.AnswerUpdate(null);

            Assert.That(failure, Is.EqualTo("the lobby was left or the login ended before the request finished"));
            Assert.That(_api.Calls, Has.No.Member("copy id=L1"));
        }

        [Test]
        public void DisposingLeavesTheJoinedLobbiesAndAnyLobbyThatArrivesLate()
        {
            CreateL1();
            var answered = false;
            _lobbies.Create(_bucket, 4, ModeDuo(), (_, _) => answered = true);
            _lobbies.Join("L2", (_, _) => answered = true);

            _lobbies.Dispose();
            _lobbies.Dispose();
            Assert.That(_api.Calls, Is.EqualTo(_createJoinLeaveCalls));

            _api.AnswerCreate("L3");
            _api.AnswerJoin(null);
            Assert.That(answered, Is.False, "no callbacks after Dispose");
            Assert.That(_api.Calls, Is.EqualTo(_afterLateAnswersCalls));

            _api.AnswerLeave("leaving the EOS member lobby id=L1 failed: TimedOut");
            Assert.That(_log, Has.Member("[Lobby] member lobby leave failed id=L1 reason=leaving the EOS member lobby id=L1 failed: TimedOut"));
            _api.RaiseMemberUpdate("L1", _friend);
            Assert.That(_changes, Is.Empty);

            Assert.Throws<ObjectDisposedException>(() => _lobbies.Create(_bucket, 4, ModeDuo(), (_, _) => { }));
            Assert.Throws<ObjectDisposedException>(() => _lobbies.Join("L4", (_, _) => { }));
            Assert.Throws<ObjectDisposedException>(() => _lobbies.Leave("L1", _ => { }));
            Assert.Throws<ObjectDisposedException>(() => _lobbies.SetMemberAttributes("L1", ModeDuo(), _ => { }));
            Assert.Throws<ObjectDisposedException>(() => _lobbies.ReadMemberAttributes("L1", _friend, (_, _) => { }));
            Assert.Throws<ObjectDisposedException>(() => _lobbies.Read("L1", (_, _) => { }));
        }

        [Test]
        public void ReadingReturnsTheLobby()
        {
            CreateL1();
            LobbyInfo read = null;
            var failure = "not answered";
            _lobbies.Read("L1", (lobby, error) =>
            {
                read = lobby;
                failure = error;
            });

            Assert.That(failure, Is.Null);
            Assert.That(read.Id, Is.EqualTo("L1"));
            Assert.That(_lobbies.Joined, Is.EquivalentTo(_joinedL1));
        }

        [Test]
        public void ReadingALobbyThatIsGoneReturnsNothingAndForgetsIt()
        {
            CreateL1();
            _api.CopyError = LobbyBackendErrors.NotFound;
            LobbyInfo read = Party("x", _self);
            var failure = "not answered";
            _lobbies.Read("L1", (lobby, error) =>
            {
                read = lobby;
                failure = error;
            });

            Assert.That(read, Is.Null);
            Assert.That(failure, Is.Null);
            Assert.That(_lobbies.Joined, Is.Empty);
        }

        [Test]
        public void ReadingFailsWithTheReasonWhenTheLobbyCannotBeRead()
        {
            CreateL1();
            _api.CopyError = "reading the EOS lobby id=L1 failed: TimedOut";
            LobbyInfo read = Party("x", _self);
            string failure = null;
            _lobbies.Read("L1", (lobby, error) =>
            {
                read = lobby;
                failure = error;
            });

            Assert.That(read, Is.Null);
            Assert.That(failure, Is.EqualTo("reading the EOS lobby id=L1 failed: TimedOut"));
            Assert.That(_lobbies.Joined, Is.EquivalentTo(_joinedL1));
        }

        [Test]
        public void ReadingChecksItsArguments()
        {
            Assert.Throws<ArgumentException>(() => _lobbies.Read("", (_, _) => { }));
            Assert.Throws<ArgumentNullException>(() => _lobbies.Read("L1", null));
            Assert.That(_api.Calls, Is.Empty);
        }

        [Test]
        public void ChecksItsArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new MemberLobbies(null, _log.Add));
            Assert.Throws<ArgumentNullException>(() => new MemberLobbies(_api, null));
            Assert.Throws<ArgumentException>(() => _lobbies.Create("", 4, ModeDuo(), (_, _) => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => _lobbies.Create(_bucket, 0, ModeDuo(), (_, _) => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => _lobbies.Create(_bucket, LobbyLaunchOptions.MaxMaxPlayers + 1, ModeDuo(), (_, _) => { }));
            Assert.Throws<ArgumentNullException>(() => _lobbies.Create(_bucket, 4, null, (_, _) => { }));
            Assert.Throws<ArgumentException>(() => _lobbies.Create(_bucket, 4, new LobbyAttribute[] { null }, (_, _) => { }));
            Assert.Throws<ArgumentNullException>(() => _lobbies.Create(_bucket, 4, ModeDuo(), null));
            Assert.Throws<ArgumentException>(() => _lobbies.Join("  ", (_, _) => { }));
            Assert.Throws<ArgumentException>(() => _lobbies.Join(null, (_, _) => { }));
            Assert.Throws<ArgumentNullException>(() => _lobbies.Join("L1", null));
            Assert.Throws<ArgumentException>(() => _lobbies.Leave("", _ => { }));
            Assert.Throws<ArgumentNullException>(() => _lobbies.SetMemberAttributes("L1", null, _ => { }));
            Assert.Throws<ArgumentException>(() => _lobbies.ReadMemberAttributes("L1", "", (_, _) => { }));
            Assert.That(_api.Calls, Is.Empty, "rejected requests do not reach the service");
        }

        private sealed class FakeMemberLobbyApi : IMemberLobbyApi
        {
            private readonly Queue<Action<string, string>> _creates = new Queue<Action<string, string>>();
            private readonly Queue<Action<string>> _updates = new Queue<Action<string>>();
            private readonly Queue<Action<string>> _joins = new Queue<Action<string>>();
            private readonly Queue<Action<string>> _leaves = new Queue<Action<string>>();

            public event Action<string, string> MemberUpdateReceived;

            public List<string> Calls { get; } = new List<string>();

            public Dictionary<string, LobbyInfo> Lobbies { get; } = new Dictionary<string, LobbyInfo>();

            public Dictionary<string, IReadOnlyList<LobbyAttribute>> MemberAttributes { get; } =
                new Dictionary<string, IReadOnlyList<LobbyAttribute>>();

            public string CopyError { get; set; }

            public void CreateLobby(string bucketId, int maxMembers, Action<string, string> done)
            {
                Calls.Add("create bucket=" + bucketId + " max=" + maxMembers);
                _creates.Enqueue(done);
            }

            public void UpdateLobbyAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
            {
                Calls.Add("update-lobby id=" + lobbyId + " " + Describe(attributes));
                _updates.Enqueue(done);
            }

            public void UpdateMemberAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
            {
                Calls.Add("update-member id=" + lobbyId + " " + Describe(attributes));
                _updates.Enqueue(done);
            }

            public void JoinLobby(string lobbyId, Action<string> done)
            {
                Calls.Add("join id=" + lobbyId);
                _joins.Enqueue(done);
            }

            public void LeaveLobby(string lobbyId, Action<string> done)
            {
                Calls.Add("leave id=" + lobbyId);
                _leaves.Enqueue(done);
            }

            public LobbyInfo CopyLobby(string lobbyId, out string error)
            {
                Calls.Add("copy id=" + lobbyId);
                if (CopyError != null)
                {
                    error = CopyError;
                    return null;
                }

                error = null;
                return Lobbies.TryGetValue(lobbyId, out var lobby) ? lobby : new LobbyInfo(lobbyId, _self, 4, new[] { _self });
            }

            public IReadOnlyList<LobbyAttribute> CopyMemberAttributes(string lobbyId, string userId, out string error)
            {
                Calls.Add("copy-member id=" + lobbyId + " user=" + userId);
                if (CopyError != null)
                {
                    error = CopyError;
                    return null;
                }

                error = null;
                return MemberAttributes.TryGetValue(lobbyId + "/" + userId, out var attributes) ? attributes : null;
            }

            public void AnswerCreate(string lobbyId, string error = null) => _creates.Dequeue()(lobbyId, error);

            public void AnswerUpdate(string error) => _updates.Dequeue()(error);

            public void AnswerJoin(string error) => _joins.Dequeue()(error);

            public void AnswerLeave(string error) => _leaves.Dequeue()(error);

            public void RaiseMemberUpdate(string lobbyId, string userId) => MemberUpdateReceived?.Invoke(lobbyId, userId);

            private static string Describe(IReadOnlyList<LobbyAttribute> attributes)
            {
                var parts = new string[attributes.Count];
                for (var i = 0; i < parts.Length; i++)
                {
                    parts[i] = attributes[i].ToString();
                }

                return string.Join(",", parts);
            }
        }
    }
}
