using System;
using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Net.Tests
{
    public sealed class LobbyServiceTests
    {
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _owner = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _bucket = "fps:test";

        private FakeLobbyBackend _backend;
        private LobbyService _service;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeLobbyBackend();
            _service = new LobbyService(_backend, _bucket);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        private void LogIn()
        {
            _service.Login((id, error) => { });
            _backend.CompleteLogin(_self);
        }

        private static LobbyInfo Lobby(string id, string owner, params string[] members)
        {
            return new LobbyInfo(id, owner, 8, members);
        }

        [Test]
        public void LoginRemembersTheUser()
        {
            string result = null;
            _service.Login((id, error) => result = id);

            Assert.That(_service.IsBusy, Is.True);
            _backend.CompleteLogin(_self);

            Assert.That(result, Is.EqualTo(_self));
            Assert.That(_service.LocalUserId, Is.EqualTo(_self));
            Assert.That(_service.IsLoggedIn, Is.True);
            Assert.That(_service.IsBusy, Is.False);
        }

        [Test]
        public void LoginFailureIsReported()
        {
            string failure = null;
            _service.Login((id, error) => failure = error);
            _backend.CompleteLogin(null, "network down");

            Assert.That(failure, Is.EqualTo("network down"));
            Assert.That(_service.IsLoggedIn, Is.False);
        }

        [Test]
        public void LoginWithoutUserIdIsAFailure()
        {
            string failure = null;
            _service.Login((id, error) => failure = error);
            _backend.CompleteLogin(" ");

            Assert.That(failure, Is.Not.Null);
            Assert.That(_service.IsLoggedIn, Is.False);
        }

        [Test]
        public void LobbiesNeedALogin()
        {
            Assert.Throws<InvalidOperationException>(() => _service.CreateLobby((lobby, error) => { }));
            Assert.Throws<InvalidOperationException>(() => _service.SearchLobbies((lobbies, error) => { }));
            Assert.Throws<InvalidOperationException>(() => _service.JoinLobby("l1", (lobby, error) => { }));
        }

        [Test]
        public void CreateUsesEightPlayersAndTheBucket()
        {
            LogIn();
            LobbyInfo created = null;
            _service.CreateLobby((lobby, error) => created = lobby);

            Assert.That(_backend.CreatedMaxMembers, Is.EqualTo(LobbyLaunchOptions.DefaultMaxPlayers));
            Assert.That(_backend.CreatedBucket, Is.EqualTo(_bucket));

            var info = Lobby("l1", _self, _self);
            _backend.CompleteCreate(info);

            Assert.That(created, Is.SameAs(info));
            Assert.That(_service.Current, Is.SameAs(info));
            Assert.That(_service.IsOwner, Is.True);
        }

        [TestCase(1)]
        [TestCase(65)]
        public void CreateRejectsInvalidMaxPlayers(int maxPlayers)
        {
            LogIn();

            Assert.Throws<ArgumentOutOfRangeException>(() => _service.CreateLobby(maxPlayers, (lobby, error) => { }));
        }

        [Test]
        public void OnlyOneOperationAtATime()
        {
            LogIn();
            _service.SearchLobbies((lobbies, error) => { });

            Assert.Throws<InvalidOperationException>(() => _service.CreateLobby((lobby, error) => { }));
        }

        [Test]
        public void CannotEnterASecondLobby()
        {
            LogIn();
            _service.CreateLobby((lobby, error) => { });
            _backend.CompleteCreate(Lobby("l1", _self, _self));

            Assert.Throws<InvalidOperationException>(() => _service.JoinLobby("l2", (lobby, error) => { }));
        }

        [Test]
        public void SearchReturnsAnEmptyListForNoResults()
        {
            LogIn();
            IReadOnlyList<LobbyInfo> found = null;
            _service.SearchLobbies((lobbies, error) => found = lobbies);
            _backend.CompleteSearch(null);

            Assert.That(found, Is.Empty);
            Assert.That(_backend.SearchedBucket, Is.EqualTo(_bucket));
        }

        [Test]
        public void JoinEntersTheLobby()
        {
            LogIn();
            LobbyInfo joined = null;
            _service.JoinLobby("l1", (lobby, error) => joined = lobby);

            Assert.That(_backend.JoinedId, Is.EqualTo("l1"));
            _backend.CompleteJoin(Lobby("l1", _owner, _owner, _self));

            Assert.That(joined.Id, Is.EqualTo("l1"));
            Assert.That(_service.IsOwner, Is.False);
        }

        [Test]
        public void JoinFailureLeavesTheServiceOutside()
        {
            LogIn();
            string failure = null;
            _service.JoinLobby("l1", (lobby, error) => failure = error);
            _backend.CompleteJoin(null, "full");

            Assert.That(failure, Is.EqualTo("full"));
            Assert.That(_service.Current, Is.Null);
            Assert.That(_service.IsBusy, Is.False);
        }

        [Test]
        public void OwnerClosesTheLobbyAndMemberLeavesIt()
        {
            LogIn();
            _service.CreateLobby((lobby, error) => { });
            _backend.CompleteCreate(Lobby("l1", _self, _self));
            _service.LeaveLobby();
            _backend.CompleteLeave();

            _service.JoinLobby("l2", (lobby, error) => { });
            _backend.CompleteJoin(Lobby("l2", _owner, _owner, _self));
            string result = "not called";
            _service.LeaveLobby(error => result = error);
            _backend.CompleteLeave();

            Assert.That(_backend.Leaves, Is.EqualTo(new[] { "destroy:l1", "leave:l2" }));
            Assert.That(result, Is.Null);
            Assert.That(_service.Current, Is.Null);
        }

        [Test]
        public void LeaveOutsideALobbyDoesNothing()
        {
            var called = false;
            _service.LeaveLobby(error => called = error == null);

            Assert.That(called, Is.True);
            Assert.That(_backend.Leaves, Is.Empty);
        }

        [Test]
        public void LeaveDuringJoinLeavesOnceJoined()
        {
            LogIn();
            string failure = null;
            _service.JoinLobby("l1", (lobby, error) => failure = error);
            _service.LeaveLobby();
            _backend.CompleteJoin(Lobby("l1", _owner, _owner, _self));

            Assert.That(failure, Is.Not.Null);
            Assert.That(_backend.Leaves, Is.EqualTo(new[] { "leave:l1" }));
            Assert.That(_service.Current, Is.Null);
        }

        [Test]
        public void MemberChangesOfTheCurrentLobbyAreForwarded()
        {
            LogIn();
            _service.CreateLobby((lobby, error) => { });
            _backend.CompleteCreate(Lobby("l1", _self, _self));
            LobbyInfo changed = null;
            _service.MembersChanged += lobby => changed = lobby;

            _backend.RaiseMembersChanged(Lobby("other", _owner, _owner));
            Assert.That(changed, Is.Null);

            var update = Lobby("l1", _self, _self, _owner);
            _backend.RaiseMembersChanged(update);
            Assert.That(changed, Is.SameAs(update));
            Assert.That(_service.Current.MemberCount, Is.EqualTo(2));
        }

        [Test]
        public void RemovalClearsTheCurrentLobby()
        {
            LogIn();
            _service.JoinLobby("l1", (lobby, error) => { });
            _backend.CompleteJoin(Lobby("l1", _owner, _owner, _self));
            string reason = null;
            _service.Removed += r => reason = r;

            _backend.RaiseRemoved("l1", "closed");

            Assert.That(reason, Is.EqualTo("closed"));
            Assert.That(_service.Current, Is.Null);
        }

        [Test]
        public void DisposeLeavesAndIgnoresLateCallbacks()
        {
            LogIn();
            _service.CreateLobby((lobby, error) => { });
            _backend.CompleteCreate(Lobby("l1", _self, _self));
            var called = false;

            _service.Dispose();
            _backend.CompleteLeave();
            _service.Tick();

            Assert.That(_backend.Leaves, Is.EqualTo(new[] { "destroy:l1" }));
            Assert.That(_backend.Disposed, Is.True);
            Assert.That(_backend.Ticks, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => _service.Login((id, error) => called = true));
            Assert.That(called, Is.False);
        }

        [Test]
        public void CreatePassesTheAttributes()
        {
            LogIn();
            var attributes = new[] { LobbyAttribute.OfText("server", "203.0.113.10:7777"), LobbyAttribute.OfNumber("players", 0) };
            _service.CreateLobby(4, attributes, (lobby, error) => { });

            Assert.That(_backend.CreatedMaxMembers, Is.EqualTo(4));
            Assert.That(_backend.CreatedAttributes, Is.EqualTo(attributes));
        }

        [Test]
        public void CreateWithoutAttributesSendsAnEmptyList()
        {
            LogIn();
            _service.CreateLobby((lobby, error) => { });

            Assert.That(_backend.CreatedAttributes, Is.Empty);
        }

        [Test]
        public void CreateRejectsInvalidAttributes()
        {
            LogIn();
            var duplicated = new[] { LobbyAttribute.OfNumber("players", 0), LobbyAttribute.OfNumber("players", 1) };

            Assert.Throws<ArgumentNullException>(() => _service.CreateLobby(4, null, (lobby, error) => { }));
            Assert.Throws<ArgumentException>(() => _service.CreateLobby(4, new LobbyAttribute[] { null }, (lobby, error) => { }));
            Assert.Throws<ArgumentException>(() => _service.CreateLobby(4, duplicated, (lobby, error) => { }));
            Assert.That(_service.IsBusy, Is.False);
        }

        [Test]
        public void OwnerUpdatesTheAttributesOfTheCurrentLobby()
        {
            LogIn();
            _service.CreateLobby((lobby, error) => { });
            _backend.CompleteCreate(Lobby("l1", _self, _self));
            var players = LobbyAttribute.OfNumber("players", 1);
            var result = "not called";

            _service.UpdateAttributes(new[] { players }, error => result = error);

            Assert.That(_service.IsUpdating, Is.True);
            Assert.That(_service.IsBusy, Is.False);
            Assert.That(_backend.UpdatedLobbyId, Is.EqualTo("l1"));
            Assert.That(_backend.Updates.Count, Is.EqualTo(1));
            Assert.That(_backend.Updates[0], Is.EqualTo(new[] { players }));

            _backend.CompleteUpdate();

            Assert.That(result, Is.Null);
            Assert.That(_service.IsUpdating, Is.False);
        }

        [Test]
        public void UpdateFailureIsReported()
        {
            LogIn();
            _service.CreateLobby((lobby, error) => { });
            _backend.CompleteCreate(Lobby("l1", _self, _self));
            string failure = null;

            _service.UpdateAttributes(new[] { LobbyAttribute.OfNumber("players", 1) }, error => failure = error);
            _backend.CompleteUpdate("too many requests");

            Assert.That(failure, Is.EqualTo("too many requests"));
            Assert.That(_service.IsUpdating, Is.False);
            Assert.That(_service.Current.Id, Is.EqualTo("l1"));
        }

        [Test]
        public void OnlyTheOwnerCanUpdateOneAtATime()
        {
            var players = new[] { LobbyAttribute.OfNumber("players", 1) };
            LogIn();

            Assert.Throws<InvalidOperationException>(() => _service.UpdateAttributes(players, error => { }));

            _service.JoinLobby("l1", (lobby, error) => { });
            _backend.CompleteJoin(Lobby("l1", _owner, _owner, _self));
            Assert.Throws<InvalidOperationException>(() => _service.UpdateAttributes(players, error => { }));

            _service.LeaveLobby();
            _backend.CompleteLeave();
            _service.CreateLobby((lobby, error) => { });
            _backend.CompleteCreate(Lobby("l2", _self, _self));
            _service.UpdateAttributes(players, error => { });
            Assert.Throws<InvalidOperationException>(() => _service.UpdateAttributes(players, error => { }));
            Assert.Throws<ArgumentException>(() => _service.UpdateAttributes(Array.Empty<LobbyAttribute>(), error => { }));
            Assert.That(_backend.Updates.Count, Is.EqualTo(1));
        }

        [Test]
        public void LeavingDuringAnUpdateClosesTheLobbyAtOnce()
        {
            LogIn();
            _service.CreateLobby((lobby, error) => { });
            _backend.CompleteCreate(Lobby("l1", _self, _self));
            var result = "not called";
            _service.UpdateAttributes(new[] { LobbyAttribute.OfNumber("players", 1) }, error => result = error);

            _service.LeaveLobby();

            Assert.That(_backend.Leaves, Is.EqualTo(new[] { "destroy:l1" }));
            _backend.CompleteUpdate("not found");
            Assert.That(result, Is.EqualTo("not found"));
            Assert.That(_service.IsUpdating, Is.False);
        }

        [Test]
        public void LogoutRemovesTheLobbyAndAllowsLoggingInAgain()
        {
            LogIn();
            _service.CreateLobby((lobby, error) => { });
            _backend.CompleteCreate(Lobby("l1", _self, _self));
            var events = new List<string>();
            _service.Removed += reason => events.Add("removed");
            _service.LoggedOut += reason => events.Add("logged out: " + reason);

            _backend.RaiseLoggedOut("expired");

            Assert.That(events, Is.EqualTo(new[] { "removed", "logged out: expired" }));
            Assert.That(_service.Current, Is.Null);
            Assert.That(_service.IsLoggedIn, Is.False);

            LogIn();
            Assert.That(_service.LocalUserId, Is.EqualTo(_self));
        }

        [Test]
        public void LogoutBeforeLoginIsIgnored()
        {
            var loggedOut = false;
            _service.LoggedOut += reason => loggedOut = true;

            _backend.RaiseLoggedOut("expired");

            Assert.That(loggedOut, Is.False);
        }

        [Test]
        public void ConstructorRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new LobbyService(null, _bucket));
            Assert.Throws<ArgumentException>(() => _ = new LobbyService(new FakeLobbyBackend(), " "));
        }
    }
}
