using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientLeaderboardTests
    {
        private const string _me = "0123456789abcdef0123456789abcdef";
        private const string _ace = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _bee = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private FakeLeaderboardBackend _backend;
        private List<LeaderboardStatus?> _published;
        private List<string> _log;
        private ClientLeaderboard _leaderboard;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeLeaderboardBackend();
            _published = new List<LeaderboardStatus?>();
            _log = new List<string>();
            _leaderboard = new ClientLeaderboard(_backend, new LeaderboardSpec(pageSize: 2, refreshSeconds: 30f), _published.Add, _log.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _leaderboard.Dispose();
        }

        [Test]
        public void LogsInThenListsTheTopAndMarksThePlayer()
        {
            Assert.That(_leaderboard.Open("kills"), Is.True);

            Assert.That(_published, Is.EqualTo(new LeaderboardStatus?[] { new LeaderboardStatus("kills", LeaderboardStage.Loading, null) }));
            Assert.That(_backend.Logins, Is.EqualTo(1));
            Assert.That(_backend.Queries, Is.Empty, "the board is queried after the login");

            _backend.CompleteLogin(_me);

            Assert.That(_backend.Queries, Has.Count.EqualTo(1));
            Assert.That(_backend.Queries[0].BoardId, Is.EqualTo("kills"));
            Assert.That(_backend.Queries[0].MaxRows, Is.EqualTo(2));
            Assert.That(_backend.Queries[0].LocalUserId, Is.EqualTo(_me));

            _backend.CompleteQuery(new LeaderboardPage(
                new[] { new LeaderboardEntry(1, _ace, "Ace", 9000000000L), new LeaderboardEntry(2, _me.ToUpperInvariant(), "Me", 12) },
                new LeaderboardEntry(2, _me, "Me", 12)));

            Assert.That(_published[^1], Is.EqualTo(new LeaderboardStatus("kills", LeaderboardStage.Ready, new[]
            {
                new LeaderboardRow(1, "Ace", 9000000000L, false),
                new LeaderboardRow(2, "Me", 12, true),
            })));
            Assert.That(_log, Does.Contain("[Leaderboard] logged in user=" + _me));
            Assert.That(_log, Does.Contain("[Leaderboard] loaded board=kills rows=2 localRank=2"));
        }

        [Test]
        public void AddsThePlayerBelowTheTopWhenTheyAreRankedLower()
        {
            OpenLoggedIn("kills");

            _backend.CompleteQuery(new LeaderboardPage(
                new[] { new LeaderboardEntry(1, _ace, "Ace", 30), new LeaderboardEntry(2, _bee, "Bee", 20), new LeaderboardEntry(3, "c", "Cee", 10) },
                new LeaderboardEntry(57, _me, "Me", 1)));

            Assert.That(_leaderboard.Status.Value.Rows, Is.EqualTo(new[]
            {
                new LeaderboardRow(1, "Ace", 30, false),
                new LeaderboardRow(2, "Bee", 20, false),
                new LeaderboardRow(57, "Me", 1, true),
            }), "only the page size of top rows, then the player's own row");
        }

        [Test]
        public void LeavesOutRowsWithoutARankAndAnUnrankedPlayer()
        {
            var rows = ClientLeaderboard.BuildRows(
                new LeaderboardPage(new[] { new LeaderboardEntry(0, _ace, "Ace", 30), new LeaderboardEntry(1, _bee, null, 20) }, new LeaderboardEntry(0, _me, "Me", 0)),
                _me,
                10);

            Assert.That(rows, Is.EqualTo(new[] { new LeaderboardRow(1, string.Empty, 20, false) }));
            Assert.That(ClientLeaderboard.BuildRows(LeaderboardPage.Empty, _me, 10), Is.Empty);
            Assert.That(
                ClientLeaderboard.BuildRows(new LeaderboardPage(null, new LeaderboardEntry(4, _ace, "Ace", 3)), _me, 10),
                Is.Empty,
                "a row for someone else is not shown as the player's own");
        }

        [Test]
        public void ShowsUnavailableWhenTheLoginFailsAndRetriesOnRefresh()
        {
            _leaderboard.Open("kills");
            _backend.CompleteLogin(null, "EOS SDK initialize failed: InvalidParameters");

            Assert.That(_published[^1], Is.EqualTo(new LeaderboardStatus("kills", LeaderboardStage.Unavailable, null)));
            Assert.That(_log, Does.Contain("[Leaderboard] logging in failed: EOS SDK initialize failed: InvalidParameters"));

            Assert.That(_leaderboard.Refresh(), Is.True);
            Assert.That(_backend.Logins, Is.EqualTo(2));
        }

        [Test]
        public void ShowsUnavailableWhenTheQueryFails()
        {
            OpenLoggedIn("kills");

            _backend.CompleteQuery(null, "querying EOS leaderboard kills failed: NotFound");

            Assert.That(_published[^1], Is.EqualTo(new LeaderboardStatus("kills", LeaderboardStage.Unavailable, null)));
            Assert.That(_log, Does.Contain("[Leaderboard] querying board=kills failed: querying EOS leaderboard kills failed: NotFound"));
        }

        [Test]
        public void RefusesABoardThatIsNotInTheTuning()
        {
            Assert.That(_leaderboard.Open("nope"), Is.False);

            Assert.That(_published, Is.Empty);
            Assert.That(_backend.Logins, Is.Zero);
            Assert.That(_log, Does.Contain("[Leaderboard] no leaderboard has the id 'nope' in the leaderboard tuning"));
        }

        [Test]
        public void SwitchingBoardsWaitsForTheQueryInFlightAndDropsItsResult()
        {
            OpenLoggedIn("kills");
            _leaderboard.Open("wins");

            Assert.That(_backend.Queries, Has.Count.EqualTo(1), "one query at a time");
            Assert.That(_published[^1], Is.EqualTo(new LeaderboardStatus("wins", LeaderboardStage.Loading, null)));

            _backend.CompleteQuery(new LeaderboardPage(new[] { new LeaderboardEntry(1, _ace, "Ace", 30) }, null));

            Assert.That(_published[^1], Is.EqualTo(new LeaderboardStatus("wins", LeaderboardStage.Loading, null)), "the kills rows are not shown as wins");
            Assert.That(_backend.Queries, Has.Count.EqualTo(2));
            Assert.That(_backend.Queries[1].BoardId, Is.EqualTo("wins"));

            _backend.CompleteQuery(new LeaderboardPage(new[] { new LeaderboardEntry(1, _bee, "Bee", 4) }, null));

            Assert.That(_published[^1], Is.EqualTo(new LeaderboardStatus("wins", LeaderboardStage.Ready, new[] { new LeaderboardRow(1, "Bee", 4, false) })));
        }

        [Test]
        public void RefreshKeepsTheRowsWhileLoadingAndRunsOnTheRefreshInterval()
        {
            OpenLoggedIn("kills");
            _backend.CompleteQuery(new LeaderboardPage(new[] { new LeaderboardEntry(1, _ace, "Ace", 30) }, null));
            var ready = new LeaderboardStatus("kills", LeaderboardStage.Ready, new[] { new LeaderboardRow(1, "Ace", 30, false) });

            _leaderboard.Tick(29f);
            Assert.That(_backend.Queries, Has.Count.EqualTo(1));
            Assert.That(_backend.Ticks, Is.EqualTo(1));

            _leaderboard.Tick(1f);

            Assert.That(_backend.Queries, Has.Count.EqualTo(2));
            Assert.That(_published[^1], Is.EqualTo(new LeaderboardStatus("kills", LeaderboardStage.Loading, ready.Rows)));
            Assert.That(_leaderboard.Refresh(), Is.False, "a refresh while querying is not stacked");
            Assert.That(_backend.Logins, Is.EqualTo(1), "the login is kept");

            _backend.CompleteQuery(new LeaderboardPage(new[] { new LeaderboardEntry(1, _ace, "Ace", 31) }, null));

            Assert.That(_published[^1], Is.EqualTo(new LeaderboardStatus("kills", LeaderboardStage.Ready, new[] { new LeaderboardRow(1, "Ace", 31, false) })));
        }

        [Test]
        public void ClosingClearsTheStatusAndDropsALateResult()
        {
            OpenLoggedIn("kills");

            _leaderboard.Close();
            _backend.CompleteQuery(new LeaderboardPage(new[] { new LeaderboardEntry(1, _ace, "Ace", 30) }, null));
            _leaderboard.Tick(60f);

            Assert.That(_published[^1], Is.Null);
            Assert.That(_leaderboard.Status, Is.Null);
            Assert.That(_backend.Queries, Has.Count.EqualTo(1), "a closed board is not refreshed");
        }

        [Test]
        public void DisposeClosesAndDisposesTheBackend()
        {
            OpenLoggedIn("kills");

            _leaderboard.Dispose();
            _backend.CompleteQuery(new LeaderboardPage(new[] { new LeaderboardEntry(1, _ace, "Ace", 30) }, null));

            Assert.That(_backend.Disposed, Is.True);
            Assert.That(_published[^1], Is.Null);
            Assert.Throws<ObjectDisposedException>(() => _leaderboard.Open("kills"));
        }

        private void OpenLoggedIn(string boardId)
        {
            _leaderboard.Open(boardId);
            _backend.CompleteLogin(_me);
        }

        private sealed class FakeLeaderboardBackend : ILeaderboardBackend
        {
            private Action<string, string> _login;

            public int Ticks { get; private set; }

            public int Logins { get; private set; }

            public bool Disposed { get; private set; }

            public List<Query> Queries { get; } = new List<Query>();

            public void Tick()
            {
                Ticks++;
            }

            public void Login(Action<string, string> done)
            {
                Logins++;
                _login = done;
            }

            public void QueryRanks(string boardId, int maxRows, string localUserId, Action<LeaderboardPage, string> done)
            {
                Queries.Add(new Query(boardId, maxRows, localUserId, done));
            }

            public void CompleteLogin(string userId, string error = null)
            {
                var done = _login ?? throw new InvalidOperationException("no pending login");
                _login = null;
                done(userId, error);
            }

            public void CompleteQuery(LeaderboardPage page, string error = null)
            {
                foreach (var query in Queries)
                {
                    if (query.Done != null)
                    {
                        var done = query.Done;
                        query.Done = null;
                        done(page, error);
                        return;
                    }
                }

                throw new InvalidOperationException("no pending query");
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class Query
        {
            public Query(string boardId, int maxRows, string localUserId, Action<LeaderboardPage, string> done)
            {
                BoardId = boardId;
                MaxRows = maxRows;
                LocalUserId = localUserId;
                Done = done;
            }

            public string BoardId { get; }

            public int MaxRows { get; }

            public string LocalUserId { get; }

            public Action<LeaderboardPage, string> Done { get; set; }
        }
    }
}
