using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 武器のアセットと EnemyAiTest を生成し直し、Play Mode で走らせて、ログで次を確かめて Editor を終える。
    /// <list type="bullet">
    /// <item>プレイヤーの武器が撃てる (Play 直後に 1 発撃ち、<c>[Combat] fire ammo=...</c> が出る)。</item>
    /// <item>巡回してきた敵がプレイヤーを撃つ (<c>[AI] id=enemy-1 fire ammo=...</c> が出る)。</item>
    /// <item>
    /// 敵が撃ち始めたら、プレイヤーの位置から撃たれたことにして敵に <see cref="HitDamage"/> のダメージを与え、
    /// 敵が遮蔽点に移って隠れる (<c>[AI] id=enemy-1 state=TakeCover</c> の後に <c>[AI] id=enemy-1 cover phase=Hiding</c> が出る)。
    /// </item>
    /// <item>Play 中にエラー・例外・Assert のログが出ない。</item>
    /// </list>
    /// すべて満たせば終了コード 0、そうでなければ 1 で終わる。走らせる秒数は <see cref="SecondsArgument"/> で変えられる。
    /// Play Mode に入るので <c>-quit</c> は付けずに、Editor を閉じた状態で次のように呼ぶ:
    /// <code>unity -batchmode -projectPath . -executeMethod Fps.Editor.EnemyFireSmokeRun.Run -logFile -</code>
    /// </summary>
    /// <remarks>
    /// Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    /// </remarks>
    [InitializeOnLoad]
    public static class EnemyFireSmokeRun
    {
        public const string LogTag = "[EnemyFireSmokeRun]";

        /// <summary>Play Mode で走らせる秒数を渡す引数。例: <c>-enemyFireSmokeSeconds 60</c>。</summary>
        public const string SecondsArgument = "-enemyFireSmokeSeconds";

        /// <summary>敵が巡回路を回って壁の端から出てきて撃ち始め、撃たれて遮蔽点に着くまでに足りる秒数。</summary>
        public const float DefaultSeconds = 60f;

        /// <summary>撃ち始めた敵に与えるダメージ。倒さずに TakeCover に移らせる量。</summary>
        public const int HitDamage = 20;

        /// <summary>Play Mode に入れないまま、これだけ待ったら失敗にする (秒)。</summary>
        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Fps.EnemyFireSmokeRun.Active";
        private const string SecondsKey = "Fps.EnemyFireSmokeRun.Seconds";
        private const string RequestedAtKey = "Fps.EnemyFireSmokeRun.RequestedAt";

        private const string PlayerFirePrefix = "[Combat] fire ammo=";

        private static readonly string EnemyFirePrefix = $"{EnemyAgent.LogTag} id={EnemyTestSceneBuilder.EnemyId} fire ammo=";

        private static readonly string TakeCoverPrefix = $"{EnemyAgent.LogTag} id={EnemyTestSceneBuilder.EnemyId} state={Fps.Core.EnemyState.TakeCover} ";

        private static readonly string HidingPrefix =
            $"{EnemyAgent.LogTag} id={EnemyTestSceneBuilder.EnemyId} cover phase={Fps.Core.EnemyCoverPhase.Hiding} point=";

        private static readonly List<string> _errors = new List<string>();
        private static bool _playerFired;
        private static bool _enemyFired;
        private static bool _triggerPulled;
        private static bool _enemyHit;
        private static bool _tookCover;
        private static bool _reachedCover;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        static EnemyFireSmokeRun()
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

                WeaponAssetBuilder.Build();
                EnemyTestSceneBuilder.Build();
                EditorSceneManager.OpenScene(EnemyTestSceneBuilder.ScenePath, OpenSceneMode.Single);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(SecondsKey, seconds);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log(
                    $"{LogTag} entering play mode in '{EnemyTestSceneBuilder.ScenePath}' for {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s");
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
            _playerFired = false;
            _enemyFired = false;
            _triggerPulled = false;
            _enemyHit = false;
            _tookCover = false;
            _reachedCover = false;
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
                // Unity 6000.6.0f1 の batchmode では、Editor の起動時インデックス (SearchInit.IndexationOnStartup) が
                // Play 中に例外を投げることがある。ゲームのコードではないので、これだけは判定から外す。
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
                {
                    return;
                }

                _errors.Add($"{type}: {condition}");
                return;
            }

            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(PlayerFirePrefix, StringComparison.Ordinal))
            {
                _playerFired = true;
            }
            else if (condition.StartsWith(EnemyFirePrefix, StringComparison.Ordinal))
            {
                _enemyFired = true;
            }
            else if (_enemyHit && condition.StartsWith(TakeCoverPrefix, StringComparison.Ordinal))
            {
                _tookCover = true;
            }
            else if (_tookCover && condition.StartsWith(HidingPrefix, StringComparison.Ordinal))
            {
                _reachedCover = true;
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

            // batchmode では入力が無いので、GameBootstrap が組み立てた武器の引き金をコードで 1 回だけ引く。
            if (!_triggerPulled)
            {
                _triggerPulled = true;
                var weapon = UnityEngine.Object.FindAnyObjectByType<WeaponController>();
                if (weapon == null)
                {
                    _errors.Add("no WeaponController in the scene; GameBootstrap did not build the player's weapon");
                }
                else if (!weapon.Fire())
                {
                    _errors.Add("the player's weapon did not fire");
                }
            }

            // 撃ち始めた敵を、プレイヤーの位置から撃たれたことにする。プレイヤーに狙わせるより確実に当たり、判定がぶれない。
            if (_enemyFired && !_enemyHit)
            {
                _enemyHit = true;
                HitEnemy();
            }

            var seconds = SessionState.GetFloat(SecondsKey, DefaultSeconds);
            if (!(_enemyFired && _playerFired && _reachedCover) && now - _playStartedAt < seconds)
            {
                return;
            }

            var problems = new List<string>();
            if (!_playerFired)
            {
                problems.Add($"no '{PlayerFirePrefix}...' line; the player's weapon did not fire");
            }

            if (!_enemyFired)
            {
                problems.Add($"no '{EnemyFirePrefix}...' line within {seconds}s; the enemy did not fire");
            }
            else if (!_tookCover)
            {
                problems.Add($"no '{TakeCoverPrefix}...' line within {seconds}s; the enemy did not take cover after being shot");
            }
            else if (!_reachedCover)
            {
                problems.Add($"no '{HidingPrefix}...' line within {seconds}s; the enemy did not reach a cover point");
            }

            foreach (var error in _errors)
            {
                problems.Add(error);
            }

            Debug.Log(
                $"{LogTag} player fired={_playerFired} enemy fired={_enemyFired} took cover={_tookCover} reached cover={_reachedCover} errors={_errors.Count}");
            if (problems.Count == 0)
            {
                Finish(SuccessExitCode, "passed");
            }
            else
            {
                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
            }
        }

        private static void HitEnemy()
        {
            EnemyAgent enemy = null;
            foreach (var agent in UnityEngine.Object.FindObjectsByType<EnemyAgent>())
            {
                if (agent.Id == EnemyTestSceneBuilder.EnemyId)
                {
                    enemy = agent;
                    break;
                }
            }

            var player = GameObject.FindGameObjectWithTag(GameBootstrap.PlayerTag);
            var damageable = enemy != null ? enemy.GetComponent<Damageable>() : null;
            if (damageable == null || player == null)
            {
                _errors.Add($"could not shoot the enemy: enemy '{EnemyTestSceneBuilder.EnemyId}' found={damageable != null} player found={player != null}");
                return;
            }

            if (enemy.CoverRegistry == null)
            {
                _errors.Add($"enemy '{EnemyTestSceneBuilder.EnemyId}' has no CoverRegistry; rerun EnemyTestSceneBuilder.Build");
            }

            Debug.Log($"{LogTag} shooting {EnemyTestSceneBuilder.EnemyId} for {HitDamage} from {player.transform.position}");
            damageable.TakeDamage(HitDamage, player.transform.position);
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
