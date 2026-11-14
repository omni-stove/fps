using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ExecutionHudTextTests
    {
        private const int _alphaOne = 0;
        private const int _bravoOne = 1;
        private const int _bravoTwo = 2;

        private static readonly string[] _expectedPreparingLines = { "ラウンド 1 (2 本先取)", "開始まで 3 秒", "ALPHA 残り 1", "BRAVO 残り 2" };

        private static readonly string[] _expectedOwnSideFirst = { "ラウンド 1 (2 本先取)", "BRAVO 残り 2 (ダウン 1)", "ALPHA 残り 1" };

        private static readonly string[] _expectedLocalDowned =
        {
            "ラウンド 1 (2 本先取)", "ALPHA 残り 1 (ダウン 1)", "BRAVO 残り 2", "ダウン中 あと 15 秒",
        };

        private static readonly string[] _expectedExecuting =
        {
            "ラウンド 1 (2 本先取)", "ALPHA 残り 1", "BRAVO 残り 2 (ダウン 1)", "とどめ 66%",
        };

        private static readonly string[] _expectedAfterRound =
        {
            "ラウンド 2 (2 本先取)", "前のラウンド: ALPHA の勝ち", "開始まで 3 秒", "ALPHA 残り 1", "BRAVO 残り 2",
        };

        [Test]
        public void TheSmallTextsReadNaturally()
        {
            Assert.That(GameText.Resolve(ExecutionHudText.Round(2, 3)), Is.EqualTo("ラウンド 2 (3 本先取)"));
            Assert.That(GameText.Resolve(ExecutionHudText.Preparation(3)), Is.EqualTo("開始まで 3 秒"));
            Assert.That(GameText.Resolve(ExecutionHudText.RoundResult(Side.Bravo)), Is.EqualTo("前のラウンド: BRAVO の勝ち"));
            Assert.That(GameText.Resolve(ExecutionHudText.RoundResult(null)), Is.EqualTo("前のラウンド: 引き分け"));
            Assert.That(GameText.Resolve(ExecutionHudText.Remaining(Side.Bravo, 3, 1)), Is.EqualTo("BRAVO 残り 3 (ダウン 1)"));
            Assert.That(GameText.Resolve(ExecutionHudText.Remaining(Side.Alpha, 2, 0)), Is.EqualTo("ALPHA 残り 2"));
            Assert.That(GameText.Resolve(ExecutionHudText.Executing(40)), Is.EqualTo("とどめ 40%"));
        }

        [Test]
        public void TheTextsAreKeysOfTheRoundModeTable()
        {
            Assert.That(ExecutionHudText.Round(2, 3), Is.EqualTo(new LocalizedText("mode.execution.round", 2, 3)));
            Assert.That(ExecutionHudText.Preparation(3), Is.EqualTo(new LocalizedText("mode.execution.startsIn", 3)));
            Assert.That(ExecutionHudText.RoundResult(null), Is.EqualTo(new LocalizedText("mode.execution.previousRound", HudFormat.Draw)));
            Assert.That(ExecutionHudText.Executing(40), Is.EqualTo(new LocalizedText("mode.execution.executing", 40)));
        }

        [Test]
        public void TheSmallTextsRejectInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Round(0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Round(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Preparation(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Remaining(Side.Alpha, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Remaining(Side.Alpha, 1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => ExecutionHudText.Executing(101));
            Assert.Throws<ArgumentNullException>(() => ExecutionHudText.Lines(null, null, null));
            Assert.Throws<ArgumentNullException>(() => ExecutionHudText.RoundWins(null, null));
            Assert.Throws<ArgumentNullException>(() => ExecutionHudText.Result(null));
        }

        [Test]
        public void WhilePreparingTheLinesCountDownToTheStart()
        {
            var execution = Create();
            execution.Start();

            Assert.That(Resolved(ExecutionHudText.Lines(execution, _alphaOne, null)), Is.EqualTo(_expectedPreparingLines));
        }

        [Test]
        public void TheOwnSideComesFirstWithItsDowned()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            execution.ReportLethal(_bravoOne, _alphaOne);

            Assert.That(Resolved(ExecutionHudText.Lines(execution, _bravoTwo, null)), Is.EqualTo(_expectedOwnSideFirst));
        }

        [Test]
        public void ADownedLocalPlayerSeesTheBleedout()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            execution.ReportLethal(_alphaOne, _bravoOne);

            Assert.That(Resolved(ExecutionHudText.Lines(execution, _alphaOne, null)), Is.EqualTo(_expectedLocalDowned));
        }

        [Test]
        public void TheLocalPlayerSeesTheExecuteProgress()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            execution.ReportLethal(_bravoOne, _alphaOne);
            execution.Interact(_alphaOne, _bravoOne, 1f);

            Assert.That(Resolved(ExecutionHudText.Lines(execution, _alphaOne, _bravoOne)), Is.EqualTo(_expectedExecuting));
        }

        [Test]
        public void TheNextPreparationShowsThePreviousRound()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            WinRoundForAlpha(execution);

            Assert.That(Resolved(ExecutionHudText.Lines(execution, _alphaOne, null)), Is.EqualTo(_expectedAfterRound));
        }

        [Test]
        public void TheScoresAreRoundWinsWithTheOwnSideFirst()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            WinRoundForAlpha(execution);

            var scores = ExecutionHudText.RoundWins(execution, Side.Bravo);

            Assert.That(scores, Has.Count.EqualTo(2));
            Assert.That(GameText.Resolve(scores[0].Text), Is.EqualTo("BRAVO 0"));
            Assert.That(scores[0].IsOwn, Is.True);
            Assert.That(scores[0].IsLeading, Is.False);
            Assert.That(GameText.Resolve(scores[1].Text), Is.EqualTo("ALPHA 1"));
            Assert.That(scores[1].IsLeading, Is.True);
        }

        [Test]
        public void TheResultAppearsOnlyWhenTheMatchEnds()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            WinRoundForAlpha(execution);

            Assert.That(ExecutionHudText.Result(execution).IsEmpty, Is.True, "one round is not the match");

            execution.Tick(3f);
            WinRoundForAlpha(execution);

            Assert.That(GameText.Resolve(ExecutionHudText.Result(execution)), Is.EqualTo("ALPHA の勝ち"));
        }

        [Test]
        public void TheLogLinesNameThePlayers()
        {
            var execution = Create();
            execution.Start();
            execution.Tick(3f);
            WinRoundForAlpha(execution);

            Assert.That(ExecutionLog.Downed(2, 0), Is.EqualTo("[Mode] downed player=2 by=0"));
            Assert.That(ExecutionLog.Downed(2, null), Is.EqualTo("[Mode] downed player=2 by=-"));
            Assert.That(ExecutionLog.Revived(3, 2), Is.EqualTo("[Mode] revived player=2 by=3"));
            Assert.That(ExecutionLog.Executed(0, 2), Is.EqualTo("[Mode] executed victim=2 by=0"));
            Assert.That(ExecutionLog.BledOut(2, null), Is.EqualTo("[Mode] bledout player=2 credit=-"));
            Assert.That(ExecutionLog.Preparing(2), Is.EqualTo("[Mode] round=2 preparing"));
            Assert.That(ExecutionLog.Live(2), Is.EqualTo("[Mode] round=2 live"));
            Assert.That(ExecutionLog.RoundEnded(execution.Round, Side.Alpha), Is.EqualTo("[Mode] round=2 ended winner=0 wins=0:1,1:0"));
            Assert.That(ExecutionLog.RoundEnded(execution.Round, null), Is.EqualTo("[Mode] round=2 ended winner=draw wins=0:1,1:0"));
            Assert.Throws<ArgumentNullException>(() => ExecutionLog.RoundEnded(null, null));
        }

        private static string[] Resolved(IReadOnlyList<LocalizedText> lines)
        {
            return lines.Select(GameText.Resolve).ToArray();
        }

        private static ExecutionSession Create()
        {
            var execution = new ExecutionSession(2, 3f, 60f, ActionRules.Default.WithDowned(Downed.Revive), 1.5f);
            execution.Mode.AddPlayer(_alphaOne, Side.Alpha);
            execution.Mode.AddPlayer(_bravoOne, Side.Bravo);
            execution.Mode.AddPlayer(_bravoTwo, Side.Bravo);
            return execution;
        }

        private static void WinRoundForAlpha(ExecutionSession execution)
        {
            execution.ReportLethal(_bravoOne, _alphaOne);
            execution.Interact(_alphaOne, _bravoOne, 1.5f);
            execution.ReportLethal(_bravoTwo, _alphaOne);
            execution.Interact(_alphaOne, _bravoTwo, 1.5f);
            execution.Tick(0f);
        }
    }
}
