using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Core;
using Fps.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// ネット対戦のアセットと NetArena01 を生成し直して Play Mode に入り、シーンの <see cref="NetBootstrap"/> が立てるホストで
    /// Bot <see cref="Bots"/> 人の TDM を回して、<see cref="NetTeamDeathmatch"/> のサーバーの 1 フレームぶんの処理にかかった時間
    /// (<see cref="NetTeamDeathmatch.ServerTickMeasured"/>) を <see cref="MeasureSeconds"/> 秒計る。
    /// 平均・最大・p95 と予算 (<see cref="TickBudget"/> の既定値) を <see cref="ReportRelativePath"/> に書き、
    /// 予算を超えたか、計れなかったか、Play 中にエラー・例外・Assert のログが出たら終了コード 1、そうでなければ 0 で Editor を終える。
    /// Play Mode に入るので <c>-quit</c> は付けずに、Editor を閉じた状態で次のように呼ぶ:
    /// <code>unity -batchmode -projectPath . -executeMethod Fps.Editor.PerfSmokeRun.Run -logFile -</code>
    /// </summary>
    /// <remarks>
    /// <para>
    /// ホストのプレイヤー (人 1 人) を除いて Bot がちょうど <see cref="Bots"/> 人になるよう、試合が Spawn されたところで
    /// <see cref="NetTeamDeathmatch.LaunchBots"/> に人と Bot の合計を入れる。<see cref="NetTeamDeathmatch.CurrentChanged"/> は
    /// OnNetworkSpawn で試合を始める (LaunchBots を読む) 前に呼ばれるので、起動引数の <c>-bots</c> を使わずに済む。
    /// </para>
    /// <para>
    /// Bot がそろってから <see cref="WarmUpSeconds"/> 秒待って計り始める。Bot の Spawn が重なる最初のフレームは、
    /// 試合中の tick ではないので数えない。計っている間は、専用サーバーのプレイヤーと同じく <see cref="TargetFrameRate"/> fps に抑える
    /// (Editor のままだと描画の無い batchmode で回るだけ回り、1 フレームあたりの仕事が実際のサーバーより小さく出るため)。
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    public static class PerfSmokeRun
    {
        public const string LogTag = SmokeSuite.PerfSmokeRunLogTag;

        /// <summary>計る試合の Bot の数 (ホストの人は数えない)。</summary>
        public const int Bots = 16;

        /// <summary>ホストのプレイヤーの数。Bot はこれと合わせた人数まで足される。</summary>
        public const int HostPlayers = 1;

        /// <summary>計る秒数 (Editor の実時間)。</summary>
        public const float MeasureSeconds = 120f;

        /// <summary>Bot がそろってから計り始めるまでに待つ秒数。</summary>
        public const float WarmUpSeconds = 3f;

        /// <summary>Play Mode に入れないまま、これだけ待ったら失敗にする (秒)。</summary>
        public const float EnterTimeoutSeconds = 120f;

        /// <summary>Play Mode に入ってから、これだけ経っても Bot がそろわなければ失敗にする (秒)。</summary>
        public const float BotFillTimeoutSeconds = 60f;

        /// <summary>計っている間のフレームレート。<see cref="NetBootstrap"/> が専用サーバーのプレイヤーに設定する値と同じ。</summary>
        public const int TargetFrameRate = 60;

        /// <summary>結果を書くファイル (プロジェクトのルートから)。nightly はこれを成果物として残す。</summary>
        public const string ReportRelativePath = "Logs/perf.json";

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Fps.PerfSmokeRun.Active";
        private const string RequestedAtKey = "Fps.PerfSmokeRun.RequestedAt";
        private const double NotYet = -1d;

        private static readonly List<string> _errors = new List<string>();
        private static NetTeamDeathmatch _match;
        private static TickBudget _budget;
        private static bool _measuring;
        private static double _playStartedAt = NotYet;
        private static double _botsReadyAt = NotYet;
        private static double _measureStartedAt = NotYet;
        private static int _previousTargetFrameRate;
        private static bool _frameRateOverridden;
        private static bool _finished;

        static PerfSmokeRun()
        {
            // Play Mode に入るときにスクリプトが読み直された場合は、ここから続きを見張る。
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        /// <summary>結果を書くファイルの絶対パス。</summary>
        public static string ReportPath => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ReportRelativePath);

        /// <summary>batchmode から <c>-executeMethod</c> で呼ぶ。終わると Editor を終了コード付きで閉じる。</summary>
        public static void Run()
        {
            try
            {
                ResetState();
                DeleteStaleReport();

                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                NetSceneBuilder.Build();
                ArenaSceneBuilder.Build();
                EditorSceneManager.OpenScene(ArenaSceneBuilder.ScenePath, OpenSceneMode.Single);

                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{ArenaSceneBuilder.ScenePath}' to measure {Bots} bots for {MeasureSeconds}s");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(new List<string> { $"could not start: {e.Message}" }, 0d);
            }
        }

        /// <summary>試合に入れる人と Bot の合計 (<see cref="NetTeamDeathmatch.LaunchBots"/> に渡す値)。</summary>
        public static int LaunchBotsFor(int bots, int hostPlayers)
        {
            if (bots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bots), bots, "bots must not be negative");
            }

            if (hostPlayers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hostPlayers), hostPlayers, "hostPlayers must not be negative");
            }

            return bots + hostPlayers;
        }

        private static void ResetState()
        {
            _errors.Clear();
            _match = null;
            _budget = null;
            _measuring = false;
            _playStartedAt = NotYet;
            _botsReadyAt = NotYet;
            _measureStartedAt = NotYet;
            _frameRateOverridden = false;
            _finished = false;
        }

        // 前の回の perf.json が残っていると、今回が書く前に落ちたときに nightly が古い結果を成果物にしてしまう
        private static void DeleteStaleReport()
        {
            var path = ReportPath;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void Attach()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            NetTeamDeathmatch.CurrentChanged -= OnMatchChanged;
            NetTeamDeathmatch.CurrentChanged += OnMatchChanged;
        }

        private static void Detach()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= OnUpdate;
            NetTeamDeathmatch.CurrentChanged -= OnMatchChanged;
            if (_match != null)
            {
                _match.ServerTickMeasured -= OnTick;
            }
        }

        private static void OnMatchChanged(NetTeamDeathmatch match)
        {
            if (_match != null)
            {
                _match.ServerTickMeasured -= OnTick;
            }

            _match = match;
            if (match == null || _finished || !match.IsServer)
            {
                return;
            }

            match.LaunchBots = LaunchBotsFor(Bots, HostPlayers);
            match.ServerTickMeasured += OnTick;
            Debug.Log($"{LogTag} the match spawned; filling to {match.LaunchBots} players ({HostPlayers} host + {Bots} bots)");
        }

        private static void OnTick(double milliseconds)
        {
            if (_measuring && !_finished)
            {
                _budget.Record(milliseconds);
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }

            // 他の SmokeRun と同じく、Editor の起動時インデックスが Play 中に投げる例外はゲームのコードではないので外す。
            if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
            {
                return;
            }

            _errors.Add($"{type}: {condition}");
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
                // 計り始めた後に Play Mode が終わったなら、外から止められたので結果は出せない
                if (_playStartedAt != NotYet)
                {
                    Finish(new List<string> { "play mode ended before the measurement finished" }, MeasuredSeconds(now));
                    return;
                }

                var requestedAt = SessionState.GetFloat(RequestedAtKey, (float)now);
                if (now - requestedAt > EnterTimeoutSeconds)
                {
                    Finish(new List<string> { $"did not enter play mode within {EnterTimeoutSeconds}s" }, 0d);
                }

                return;
            }

            if (_playStartedAt == NotYet)
            {
                _playStartedAt = now;
                _previousTargetFrameRate = Application.targetFrameRate;
                _frameRateOverridden = true;
                Application.targetFrameRate = TargetFrameRate;
                return;
            }

            if (_measureStartedAt == NotYet)
            {
                WaitForBots(now);
                return;
            }

            if (_match == null || !_match.IsRunningOnServer)
            {
                Finish(new List<string> { "the match stopped running on the server while measuring" }, MeasuredSeconds(now));
                return;
            }

            if (now - _measureStartedAt >= MeasureSeconds)
            {
                Finish(new List<string>(), MeasuredSeconds(now));
            }
        }

        private static void WaitForBots(double now)
        {
            var match = _match;
            var running = match != null && match.IsRunningOnServer;
            if (_botsReadyAt == NotYet)
            {
                if (running && match.BotCount == Bots)
                {
                    _botsReadyAt = now;
                    Debug.Log($"{LogTag} {Bots} bots joined; warming up for {WarmUpSeconds}s");
                    return;
                }

                if (now - _playStartedAt > BotFillTimeoutSeconds)
                {
                    var state = match == null ? "no match was spawned" : running ? $"{match.BotCount} bot(s) joined" : "the match is not running on the server";
                    Finish(new List<string> { $"{Bots} bots did not join within {BotFillTimeoutSeconds}s ({state})" }, 0d);
                }

                return;
            }

            if (now - _botsReadyAt < WarmUpSeconds)
            {
                return;
            }

            _budget = new TickBudget();
            _measuring = true;
            _measureStartedAt = now;
            Debug.Log($"{LogTag} measuring the server tick of {nameof(NetTeamDeathmatch)} for {MeasureSeconds}s");
        }

        private static double MeasuredSeconds(double now)
        {
            return _measureStartedAt == NotYet ? 0d : Math.Max(0d, now - _measureStartedAt);
        }

        private static void Finish(List<string> problems, double measuredSeconds)
        {
            _measuring = false;
            _finished = true;
            Detach();
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(RequestedAtKey);
            if (_frameRateOverridden)
            {
                Application.targetFrameRate = _previousTargetFrameRate;
                _frameRateOverridden = false;
            }

            problems.AddRange(_errors);
            var budget = _budget ?? new TickBudget();
            var bots = _match != null && _match.IsRunningOnServer ? _match.BotCount : 0;

            var violations = new List<string>(budget.Violations());
            violations.AddRange(problems);
            var exitCode = violations.Count == 0 ? SuccessExitCode : FailureExitCode;

            var writeError = WriteReport(PerfReport.ToJson(ArenaSceneBuilder.MapName, bots, measuredSeconds, budget, problems));
            if (writeError != null)
            {
                violations.Add(writeError);
                exitCode = FailureExitCode;
            }

            Debug.Log($"{LogTag} {budget} bots={bots.ToString(CultureInfo.InvariantCulture)} seconds={TickBudget.Format(measuredSeconds)} report={ReportPath}");
            var line = exitCode == SuccessExitCode
                ? $"{LogTag} passed (exit code {exitCode})"
                : $"{LogTag} failed:\n  {string.Join("\n  ", violations)} (exit code {exitCode})";
            if (exitCode == SuccessExitCode)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogError(line);
            }

            _match = null;
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

        /// <summary>json を <see cref="ReportPath"/> に書く。書けなければ理由を返す。</summary>
        private static string WriteReport(string json)
        {
            var path = ReportPath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(path, json);
                return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                Debug.LogException(e);
                return $"could not write {path}: {e.Message}";
            }
        }
    }
}
