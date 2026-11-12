using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary>Match と TeamDeathmatch を IGameMode 越しに同じ手順で動かし、共通の振る舞いを確かめる。</summary>
    public sealed class GameModeTests
    {
        private const float _timeLimitSeconds = 10f;
        private const int _player = 1;
        private const int _enemy = 11;

        /// <summary>制限時間 10 秒、_player が _enemy を 1 回倒せば終わる設定で、開始前のモードを作る。</summary>
        public static IEnumerable<TestCaseData> Modes()
        {
            yield return new TestCaseData(new Func<IGameMode>(() => new Match(_timeLimitSeconds, 1, _player)))
                .SetArgDisplayNames(nameof(Match));
            yield return new TestCaseData(new Func<IGameMode>(() =>
            {
                var match = new TeamDeathmatch(new TeamDeathmatchSpec(1, _timeLimitSeconds, 3f));
                match.AddPlayer(_player, Team.Alpha);
                match.AddPlayer(_enemy, Team.Bravo);
                return match;
            })).SetArgDisplayNames(nameof(TeamDeathmatch));
        }

        [TestCaseSource(nameof(Modes))]
        public void StartMovesFromWaitingToPlaying(Func<IGameMode> create)
        {
            var mode = create();
            var states = new List<GameModeState>();
            mode.StateChanged += states.Add;

            Assert.That(mode.State, Is.EqualTo(GameModeState.Waiting));

            mode.Start();

            Assert.That(mode.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(states, Is.EqualTo(new[] { GameModeState.Playing }));
        }

        [TestCaseSource(nameof(Modes))]
        public void WaitingIgnoresTickKillAndDeath(Func<IGameMode> create)
        {
            var mode = create();
            var states = new List<GameModeState>();
            mode.StateChanged += states.Add;

            mode.Tick(100f);
            mode.ReportKill(_player, _enemy);
            mode.ReportDeath(_player);

            Assert.That(mode.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(states, Is.Empty);
        }

        [TestCaseSource(nameof(Modes))]
        public void RejectsInvalidTick(Func<IGameMode> create)
        {
            var mode = create();
            mode.Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => mode.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.Tick(float.NaN));
        }

        [TestCaseSource(nameof(Modes))]
        public void TimeUpEnds(Func<IGameMode> create)
        {
            var mode = create();
            var states = new List<GameModeState>();
            mode.StateChanged += states.Add;
            mode.Start();

            mode.Tick(_timeLimitSeconds - 1f);
            Assert.That(mode.State, Is.EqualTo(GameModeState.Playing));

            mode.Tick(1f);

            Assert.That(mode.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(states, Is.EqualTo(new[] { GameModeState.Playing, GameModeState.Ended }));
        }

        [TestCaseSource(nameof(Modes))]
        public void ReachingKillTargetEndsAndNothingChangesAfter(Func<IGameMode> create)
        {
            var mode = create();
            var states = new List<GameModeState>();
            mode.StateChanged += states.Add;
            mode.Start();

            mode.ReportKill(_player, _enemy);
            mode.Tick(100f);
            mode.ReportKill(_enemy, _player);
            mode.ReportDeath(_player);

            Assert.That(mode.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(states, Is.EqualTo(new[] { GameModeState.Playing, GameModeState.Ended }));
        }

        [Test]
        public void ResultsAreReadFromModeSpecificProperties()
        {
            var match = new Match(_timeLimitSeconds, 1, _player);
            var deathmatch = new TeamDeathmatch(new TeamDeathmatchSpec(1, _timeLimitSeconds, 3f));
            deathmatch.AddPlayer(_player, Team.Alpha);
            deathmatch.AddPlayer(_enemy, Team.Bravo);

            foreach (var mode in new IGameMode[] { match, deathmatch })
            {
                mode.Start();
                mode.ReportKill(_player, _enemy);
            }

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Result, Is.EqualTo(MatchResult.Won));
            Assert.That(deathmatch.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(deathmatch.Winner, Is.EqualTo(Team.Alpha));
        }
    }
}
