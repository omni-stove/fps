using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// Campaign01 を生成し直し、セーブを消してから Play Mode で数秒走らせ、ログで次を確かめて Editor を終える。
    /// <list type="bullet">
    /// <item>ミッションが始まる (<c>[Mission] objective=... state=Active</c> が出る)。</item>
    /// <item>キャンペーンがセーブ無しの状態から始まる (<c>[Campaign] no save; starting ...</c> が出る)。</item>
    /// <item>Play 中にエラー・例外・Assert のログが出ない。</item>
    /// </list>
    /// すべて満たせば終了コード 0、そうでなければ 1 で終わる。走らせる秒数は <see cref="SecondsArgument"/> で変えられる。
    /// Play Mode に入るので <c>-quit</c> は付けずに、Editor を閉じた状態で次のように呼ぶ:
    /// <code>unity -batchmode -projectPath . -executeMethod Fps.Editor.CampaignSmokeRun.Run -logFile -</code>
    /// </summary>
    /// <remarks>
    /// セーブ無しから始めるため、この Editor の <c>Application.persistentDataPath</c> にあるスロット
    /// <see cref="CampaignRunner.SaveSlot"/> のセーブを走らせる前に消す。走っている間に開始地点のチェックポイントでセーブされるので、
    /// 終えるときにもう一度消す。Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    /// </remarks>
    [InitializeOnLoad]
    public static class CampaignSmokeRun
    {
        public const string LogTag = "[CampaignSmokeRun]";

        /// <summary>Play Mode で走らせる秒数を渡す引数。例: <c>-campaignSmokeSeconds 8</c>。</summary>
        public const string SecondsArgument = "-campaignSmokeSeconds";

        public const float DefaultSeconds = 5f;

        /// <summary>Play Mode に入れないまま、これだけ待ったら失敗にする (秒)。</summary>
        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Fps.CampaignSmokeRun.Active";
        private const string SecondsKey = "Fps.CampaignSmokeRun.Seconds";
        private const string RequestedAtKey = "Fps.CampaignSmokeRun.RequestedAt";

        private static readonly List<string> _errors = new List<string>();
        private static bool _missionStarted;
        private static bool _startedWithoutSave;
        private static bool _savedAtStart;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        static CampaignSmokeRun()
        {
            // Play Mode に入るときにスクリプトが読み直された場合は、ここから続きを見張る。
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        /// <summary>batchmode から <c>-executeMethod</c> で呼ぶ。終わると Editor を終了コード付きで閉じる。</summary>
        public static void Run()
        {
            try
            {
                var seconds = ParseSeconds(Environment.GetCommandLineArgs());

                CampaignSceneBuilder.Build();
                DeleteSave("before the run");
                EditorSceneManager.OpenScene(CampaignSceneBuilder.ScenePath, OpenSceneMode.Single);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(SecondsKey, seconds);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log(
                    $"{LogTag} entering play mode in '{CampaignSceneBuilder.ScenePath}' for {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        /// <summary>
        /// args から <see cref="SecondsArgument"/> の値を読む。無ければ <see cref="DefaultSeconds"/>。
        /// 値が無い・正の有限な数でなければ <see cref="ArgumentException"/> を投げる。
        /// </summary>
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
                    || !(seconds > 0f)
                    || float.IsInfinity(seconds))
                {
                    throw new ArgumentException($"{SecondsArgument} needs a positive number of seconds", nameof(args));
                }

                return seconds;
            }

            return DefaultSeconds;
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
            _missionStarted = false;
            _startedWithoutSave = false;
            _savedAtStart = false;
            _playStartedAt = -1d;
            _finished = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            // Play Mode に入る前 (シーンの生成など) のログは判定に使わない。
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors.Add($"{type}: {condition}");
                return;
            }

            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(MissionReporter.Tag + " objective=", StringComparison.Ordinal)
                && condition.EndsWith(" state=" + ObjectiveState.Active, StringComparison.Ordinal))
            {
                _missionStarted = true;
            }
            else if (condition.StartsWith(CampaignRunner.LogTag + " no save; starting ", StringComparison.Ordinal))
            {
                _startedWithoutSave = true;
            }
            else if (condition.StartsWith(CampaignRunner.LogTag + " saved mission=", StringComparison.Ordinal)
                && condition.Contains(" checkpoint=" + CampaignSceneBuilder.StartCheckpointId + " "))
            {
                _savedAtStart = true;
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

            var seconds = SessionState.GetFloat(SecondsKey, DefaultSeconds);
            if (now - _playStartedAt < seconds)
            {
                return;
            }

            var problems = new List<string>();
            if (!_missionStarted)
            {
                problems.Add($"no '{MissionReporter.Tag} objective=... state=Active' line; the mission did not start");
            }

            if (!_startedWithoutSave)
            {
                problems.Add($"no '{CampaignRunner.LogTag} no save; starting ...' line; the campaign did not start without a save");
            }

            foreach (var error in _errors)
            {
                problems.Add(error);
            }

            Debug.Log(
                $"{LogTag} mission started={_missionStarted} started without save={_startedWithoutSave} saved at checkpoint={CampaignSceneBuilder.StartCheckpointId}={_savedAtStart} errors={_errors.Count}");
            if (problems.Count == 0)
            {
                Finish(SuccessExitCode, "passed");
            }
            else
            {
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

            try
            {
                DeleteSave("after the run");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

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

        private static void DeleteSave(string when)
        {
            var storage = FileSaveStorage.CreateDefault();
            storage.Delete(CampaignRunner.SaveSlot);
            Debug.Log($"{LogTag} deleted save slot={CampaignRunner.SaveSlot} {when} path={storage.PathFor(CampaignRunner.SaveSlot)}");
        }
    }
}
