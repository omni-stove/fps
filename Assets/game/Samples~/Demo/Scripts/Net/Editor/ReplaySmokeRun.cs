using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 本物のサーバーを立てずに通しで見るため、承認と切断だけの記録 (ReplaySmokeFile) をリプレイの一覧のフォルダーに置き、
    // メインメニューの一覧と同じ口 (GameSignals.Replays の Play) で再生する。マップを読み、記録の終わりでメニューに戻れば通る。
    // 置いた記録は終わったときに消す。
    // Shooter.Samples.Editor (SmokeSuite がある) はこのアセンブリを参照できないので、スイートには入れず単独で走らせる。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class ReplaySmokeRun
    {
        public const string LogTag = "[ReplaySmokeRun]";

        public const string MapName = ArenaSceneBuilder.MapName;

        // マップを読み終えてから、記録の終わり (切断) が届くまでの長さ。
        public const float ReplaySeconds = 3f;

        public const string ReplayIdPrefix = "smoke-";

        // メインメニューの起動 (NetV2Client の起動と一覧の登録) を済ませてから見始める。
        public const float WarmUpSeconds = 1f;

        public const float GraceSeconds = 60f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.ReplaySmokeRun.Active";
        private const string RequestedAtKey = "Shooter.ReplaySmokeRun.RequestedAt";
        private const string ReplayIdKey = "Shooter.ReplaySmokeRun.ReplayId";
        private const string ReplayPathKey = "Shooter.ReplaySmokeRun.ReplayPath";

        private static readonly string _playingPrefix = ReplayLog.Tag + " playing ";
        private static readonly string _endedPrefix = ReplayLog.Tag + " playback reached the end";
        private static readonly string _acceptedPrefix = MatchClient.LogTag + " client connected id=";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static bool _sawPlaying;
        private static bool _sawAccepted;
        private static bool _sawEnded;
        private static bool _refreshed;
        private static string _mapScene;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        public enum LineKind
        {
            Other,
            Playing,
            Accepted,
            Ended,
        }

        private enum Step
        {
            Play,
            WaitMap,
            WaitMenu,
            Done,
        }

        static ReplaySmokeRun()
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
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuSceneBuilder.ScenePath) == null)
                {
                    MenuSceneBuilder.Build();
                }
                else
                {
                    BuildSceneList.RegisterStartScene(MenuSceneBuilder.ScenePath);
                }

                ServerDataMaps.EnsureScenes();
                BuildSceneList.Register(ArenaSceneBuilder.ScenePath);
                AssetDatabase.SaveAssets();

                var collision = MapCollisionFiles.PathOf(MapName);
                if (!File.Exists(collision))
                {
                    throw new FileNotFoundException(
                        $"the map data '{collision}' is missing; run {MapCollisionExporter.MenuPath} and commit {MapCollisionFiles.ServerDataFolder}", collision);
                }

                if (!BuildVersion.TryNormalize(Application.version, out var version, out var versionError))
                {
                    throw new InvalidOperationException("cannot read the build version (Application.version): " + versionError);
                }

                var recordedAt = DateTime.UtcNow;
                var id = ReplayIdPrefix + recordedAt.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
                var path = Path.Combine(ReplayLibrary.DefaultDirectory, id + ReplayFile.Extension);
                Directory.CreateDirectory(ReplayLibrary.DefaultDirectory);
                File.WriteAllBytes(path, ReplaySmokeFile.Build(version, MapName, ReplaySeconds, recordedAt));

                EditorSceneManager.OpenScene(MenuSceneBuilder.ScenePath, OpenSceneMode.Single);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                SessionState.SetString(ReplayIdKey, id);
                SessionState.SetString(ReplayPathKey, path);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{MenuSceneBuilder.ScenePath}' to play '{path}' on {MapName}");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        public static LineKind Classify(string line)
        {
            if (line == null)
            {
                return LineKind.Other;
            }

            if (line.StartsWith(_playingPrefix, StringComparison.Ordinal))
            {
                return LineKind.Playing;
            }

            if (line.StartsWith(_acceptedPrefix, StringComparison.Ordinal))
            {
                return LineKind.Accepted;
            }

            return line.StartsWith(_endedPrefix, StringComparison.Ordinal) ? LineKind.Ended : LineKind.Other;
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
            _step = Step.Play;
            _sawPlaying = false;
            _sawAccepted = false;
            _sawEnded = false;
            _refreshed = false;
            _mapScene = null;
            _playStartedAt = -1d;
            _finished = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            // Play Mode に入る前 (シーンの生成) のログは判定に使わない。
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
                case LineKind.Playing:
                    _sawPlaying = true;
                    break;
                case LineKind.Accepted:
                    _sawAccepted = true;
                    break;
                case LineKind.Ended:
                    _sawEnded = true;
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

            var timeout = WarmUpSeconds + ReplaySeconds + GraceSeconds;
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
            switch (_step)
            {
                case Step.Play:
                    return Play();

                case Step.WaitMap:
                    return WaitMap();

                case Step.WaitMenu:
                    return WaitMenu();

                default:
                    return null;
            }
        }

        private static string Play()
        {
            if (!GameSignals.Replays.HasValue)
            {
                return null;
            }

            var library = GameSignals.Replays.Value;
            var id = SessionState.GetString(ReplayIdKey, string.Empty);
            if (!Lists(library, id))
            {
                // ドメインの再読み込みを切った Play では、記録を置く前に読んだ一覧が残っていることがある。
                if (!_refreshed && library is ReplayLibrary own)
                {
                    _refreshed = true;
                    own.Refresh();
                    return null;
                }

                return $"the replay list does not show '{id}'";
            }

            if (!library.Play(id))
            {
                return $"the replay list refused to play '{id}'; see the warnings above";
            }

            _step = Step.WaitMap;
            return null;
        }

        private static string WaitMap()
        {
            var active = SceneManager.GetActiveScene().name;
            if (string.Equals(active, MenuRoute.MainMenuSceneName, StringComparison.Ordinal) && _sawEnded)
            {
                return $"the replay went back to the menu without loading {MapName}";
            }

            if (!string.Equals(active, MapName, StringComparison.Ordinal) || !_sawPlaying || !_sawAccepted)
            {
                return null;
            }

            _mapScene = active;
            _step = Step.WaitMenu;
            return null;
        }

        private static string WaitMenu()
        {
            if (!_sawEnded || !string.Equals(SceneManager.GetActiveScene().name, MenuRoute.MainMenuSceneName, StringComparison.Ordinal))
            {
                return null;
            }

            _step = Step.Done;
            return null;
        }

        private static bool Lists(IReplayLibrary library, string id)
        {
            foreach (var entry in library.Entries)
            {
                if (string.Equals(entry.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
                "{0} step={1} playing={2} accepted={3} map={4} ended={5} scene={6} errors={7}",
                LogTag,
                _step,
                _sawPlaying,
                _sawAccepted,
                _mapScene ?? "-",
                _sawEnded,
                SceneManager.GetActiveScene().name,
                _errors.Count));

            var passed = problems.Count == 0 && _step == Step.Done && _sawPlaying && _sawAccepted && _sawEnded && _mapScene != null;
            if (passed)
            {
                Finish(SuccessExitCode, "passed");
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
            DeleteReplay(SessionState.GetString(ReplayPathKey, string.Empty));
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(RequestedAtKey);
            SessionState.EraseString(ReplayIdKey);
            SessionState.EraseString(ReplayPathKey);

            var line = $"{LogTag} {summary} (exit code {exitCode})";
            if (exitCode == SuccessExitCode)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogError(line);
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

        // 消せなくても判定は変えない。残ったファイルは一覧に並ぶだけで、ほかのリプレイの再生を妨げない。
        private static void DeleteReplay(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"{LogTag} could not remove '{path}': {e.Message}");
            }
        }
    }
}
