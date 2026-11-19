using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // batchmode には入力が無いので、プレイヤーは出現点に立ったままで、感染させ合うのは bot だけになる。
    // 最初の感染者は乱数で選ばれるので、誰が選ばれても通る形 (選ばれた・武器を持ち替えた・どちらかの陣営が勝った) で確かめる。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class InfectedSmokeRun
    {
        public const string LogTag = "[InfectedSmokeRun]";

        public const string SecondsArgument = "-infectedSmokeSeconds";

        // 最初の感染者が戻って (InfectedSpec.DefaultInfectedRespawnSeconds) 生存者を追い始めるまでを見ても、決着まで長く待たない長さにする。
        public const float DefaultSeconds = 40f;

        // 待ちが長いとスイート全体が延びるので、選ばれるまでを縮める。
        public const float StartDelaySeconds = 2f;

        // 選ばれてから戻るまで (InfectedSpec.DefaultInfectedRespawnSeconds) と、武器を持ち替えるのを見る分。
        public const float MinimumSeconds = StartDelaySeconds + InfectedSpec.DefaultInfectedRespawnSeconds + 5f;

        // GameBootstrap の Awake と InfectedRunner の Start を済ませてから見始める。
        public const float WarmUpSeconds = 1f;

        public const float GraceSeconds = 30f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.InfectedSmokeRun.Active";
        private const string SecondsKey = "Shooter.InfectedSmokeRun.Seconds";
        private const string RequestedAtKey = "Shooter.InfectedSmokeRun.RequestedAt";

        private static readonly string _outbreakPrefix = InfectedLog.LogPrefix + " outbreak first=";
        private static readonly string _turnPrefix = InfectedLog.LogPrefix + " turn player=";
        private static readonly string _firstTurnSuffix = " by=first";
        private static readonly string _equipPrefix = InfectedLog.LogPrefix + " equip player=";
        private static readonly string _endedPrefix = ModeLog.Tag + " ended winner=";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static string _outbreakLine;
        private static int _turns;
        private static bool _sawFirstTurn;
        private static int _equips;
        private static string _endedLine;
        private static string _winner;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        public enum LineKind
        {
            Other,
            Outbreak,
            FirstTurn,
            Turn,
            Equip,
            Ended,
        }

        private enum Step
        {
            CheckStart,
            WaitOutbreak,
            WaitEnd,
            Done,
        }

        static InfectedSmokeRun()
        {
            // Play Mode に入るときにスクリプトが読み直された場合は、ここから続きを見張る。
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        public static void Run()
        {
            try
            {
                var seconds = ParseSeconds(Environment.GetCommandLineArgs());

                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                InfectedSceneBuilder.Build();
                var scene = EditorSceneManager.OpenScene(InfectedSceneBuilder.ScenePathValue, OpenSceneMode.Single);

                var runner = UnityEngine.Object.FindAnyObjectByType<InfectedRunner>();
                if (runner == null)
                {
                    throw new InvalidOperationException(
                        $"'{InfectedSceneBuilder.ScenePathValue}' has no InfectedRunner; rerun {InfectedSceneBuilder.MenuPath}");
                }

                // 調整値のアセットを書き換えると保存しなくてもメモリ上で後の実行に残るので、複製を縮めて配線する。
                runner.Definition = Shorten(runner.Definition, seconds);
                EditorSceneManager.MarkSceneDirty(scene);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(SecondsKey, seconds);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log(
                    $"{LogTag} entering play mode in '{InfectedSceneBuilder.ScenePathValue}' with a {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s round");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        public static float ParseSeconds(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], SecondsArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count
                    || !float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                    || !(seconds >= MinimumSeconds)
                    || float.IsInfinity(seconds))
                {
                    throw new ArgumentException(
                        $"{SecondsArgument} needs a finite number of seconds of at least {MinimumSeconds.ToString("0.##", CultureInfo.InvariantCulture)}",
                        nameof(args));
                }

                return seconds;
            }

            return DefaultSeconds;
        }

        public static InfectedDefinition Shorten(InfectedDefinition source, float seconds)
        {
            InfectedDefinition copy;
            if (source != null)
            {
                copy = UnityEngine.Object.Instantiate(source);
            }
            else
            {
                copy = ScriptableObject.CreateInstance<InfectedDefinition>();
                copy.CopyFrom(InfectedSpec.Default);
            }

            copy.name = (source != null ? source.name : nameof(InfectedDefinition)) + " (smoke)";
            copy.hideFlags = HideFlags.DontSave;
            copy.StartDelaySeconds = StartDelaySeconds;
            copy.RoundSeconds = seconds;
            try
            {
                copy.ToSpec();
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(copy);
                throw;
            }

            return copy;
        }

        public static LineKind Classify(string line)
        {
            if (line == null)
            {
                return LineKind.Other;
            }

            if (line.StartsWith(_outbreakPrefix, StringComparison.Ordinal))
            {
                return LineKind.Outbreak;
            }

            if (line.StartsWith(_turnPrefix, StringComparison.Ordinal))
            {
                return line.EndsWith(_firstTurnSuffix, StringComparison.Ordinal) ? LineKind.FirstTurn : LineKind.Turn;
            }

            if (line.StartsWith(_equipPrefix, StringComparison.Ordinal))
            {
                return LineKind.Equip;
            }

            return line.StartsWith(_endedPrefix, StringComparison.Ordinal) ? LineKind.Ended : LineKind.Other;
        }

        // 引き分けは、感染者が選ばれずに終わった (参加者が足りない) ときにしか出ないので、決着として数えない。
        public static bool IsDecided(string endedLine)
        {
            return string.Equals(endedLine, ModeLog.Ended(Infected.SurvivorSide), StringComparison.Ordinal)
                || string.Equals(endedLine, ModeLog.Ended(Infected.InfectedSide), StringComparison.Ordinal);
        }

        private static void Attach()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void Detach()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= OnUpdate;
        }

        private static void ResetObservations()
        {
            _errors.Clear();
            _step = Step.CheckStart;
            _outbreakLine = null;
            _turns = 0;
            _sawFirstTurn = false;
            _equips = 0;
            _endedLine = null;
            _winner = null;
            _playStartedAt = -1d;
            _finished = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            // Play Mode に入る前 (アセットとシーンの生成) のログは判定に使わない。
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // CampaignSmokeRun と同じく、Editor の起動時インデックスが Play 中に投げる例外はゲームのコードではないので外す。
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
                {
                    return;
                }

                _errors.Add($"{type}: {condition}");
                return;
            }

            switch (Classify(condition))
            {
                case LineKind.Outbreak:
                    _outbreakLine = condition;
                    break;
                case LineKind.FirstTurn:
                    _sawFirstTurn = true;
                    _turns++;
                    break;
                case LineKind.Turn:
                    _turns++;
                    break;
                case LineKind.Equip:
                    _equips++;
                    break;
                case LineKind.Ended:
                    _endedLine = condition;
                    break;
            }
        }

        private static void OnUpdate()
        {
            if (_finished)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (!EditorApplication.isPlaying)
            {
                var requestedAt = SessionState.GetFloat(RequestedAtKey, (float)now);
                if (now - requestedAt > EnterTimeoutSeconds)
                {
                    Finish(FailureExitCode, $"did not enter play mode within {EnterTimeoutSeconds}s");
                }

                return;
            }

            if (_playStartedAt < 0d)
            {
                _playStartedAt = now;
                return;
            }

            if (now - _playStartedAt < WarmUpSeconds)
            {
                return;
            }

            var seconds = SessionState.GetFloat(SecondsKey, DefaultSeconds);
            string problem;
            try
            {
                problem = Advance();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                problem = $"{e.GetType().Name} at step {_step}: {e.Message}";
            }

            var timeout = WarmUpSeconds + seconds + GraceSeconds;
            if (problem == null && _step != Step.Done && now - _playStartedAt > timeout)
            {
                problem = $"step {_step} did not finish within {timeout.ToString("0.##", CultureInfo.InvariantCulture)}s of play";
            }

            if (problem == null && _step != Step.Done && _errors.Count == 0)
            {
                return;
            }

            Report(problem);
        }

        private static string Advance()
        {
            var runner = UnityEngine.Object.FindAnyObjectByType<InfectedRunner>();
            if (runner == null)
            {
                return "the scene has no InfectedRunner";
            }

            switch (_step)
            {
                case Step.CheckStart:
                    return CheckStart(runner);

                case Step.WaitOutbreak:
                    return WaitOutbreak(runner);

                case Step.WaitEnd:
                    return WaitEnd(runner);

                default:
                    return null;
            }
        }

        private static string CheckStart(InfectedRunner runner)
        {
            if (!runner.IsStarted || runner.Infected == null)
            {
                return "the InfectedRunner did not start a round; see the errors above";
            }

            var expected = InfectedSceneBuilder.SurvivorBots + 1;
            if (runner.Members.Count != expected)
            {
                return $"the round has {runner.Members.Count} member(s), expected {expected} (the player and {InfectedSceneBuilder.SurvivorBots} bots); rerun {InfectedSceneBuilder.MenuPath}";
            }

            if (runner.InfectedWeapon == null)
            {
                return $"the round has no infected weapon; rerun {InfectedSceneBuilder.MenuPath}";
            }

            if (!runner.Infected.HasBrokenOut && runner.Infected.SurvivorCount != expected)
            {
                return $"{runner.Infected.SurvivorCount} of {expected} member(s) started with the survivors";
            }

            _step = Step.WaitOutbreak;
            return null;
        }

        private static string WaitOutbreak(InfectedRunner runner)
        {
            var infected = runner.Infected;
            if (infected == null)
            {
                return "the InfectedRunner stopped the round before it ended";
            }

            if (!infected.HasBrokenOut || _outbreakLine == null || !_sawFirstTurn || _equips == 0)
            {
                return runner.IsMatchOver ? $"the round ended before the first infected came back armed ({_endedLine ?? "-"})" : null;
            }

            if (infected.InfectedCount < 1)
            {
                return $"the outbreak picked nobody ({_outbreakLine})";
            }

            _step = Step.WaitEnd;
            return null;
        }

        private static string WaitEnd(InfectedRunner runner)
        {
            if (!runner.IsMatchOver || _endedLine == null)
            {
                return null;
            }

            if (!IsDecided(_endedLine))
            {
                return $"the round ended without a winner ({_endedLine})";
            }

            _winner = _endedLine.Substring(_endedPrefix.Length);
            _step = Step.Done;
            return null;
        }

        private static void Report(string problem)
        {
            var problems = new List<string>();
            if (problem != null)
            {
                problems.Add(problem);
            }

            problems.AddRange(_errors);
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} step={1} outbreak={2} turns={3} equips={4} ended={5} errors={6}",
                LogTag,
                _step,
                _outbreakLine ?? "-",
                _turns,
                _equips,
                _endedLine ?? "-",
                _errors.Count));

            var passed = problems.Count == 0 && _step == Step.Done && _sawFirstTurn && _equips > 0 && _winner != null;
            if (passed)
            {
                Finish(SuccessExitCode, $"passed (winner={_winner})");
            }
            else
            {
                if (problems.Count == 0)
                {
                    problems.Add("not every step was confirmed");
                }

                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
            }
        }

        private static void Finish(int exitCode, string summary)
        {
            _finished = true;
            Detach();
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(SecondsKey);
            SessionState.EraseFloat(RequestedAtKey);

            var line = $"{LogTag} {summary} (exit code {exitCode})";
            if (exitCode == SuccessExitCode)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogError(line);
            }

            if (SmokeSuite.TakeOver(LogTag, exitCode))
            {
                return;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
            else if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
