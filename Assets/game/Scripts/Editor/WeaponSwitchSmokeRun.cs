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
    /// 武器のアセットと Bootstrap シーンを生成し直して Play Mode に入り、プレイヤーの <see cref="WeaponController"/> を
    /// コードから操作して次を確かめ、Editor を終える。
    /// <list type="bullet">
    /// <item>プレイヤーが既定のロードアウト (rifle, handgun) を持ち、rifle を持って始まる。</item>
    /// <item>rifle (Auto) は押し続けている間撃ち続ける。リロード中に持ち替えるとリロードは取り消される。</item>
    /// <item>Next で handgun に持ち替えて撃てる。handgun (Semi) は押し続けても 2 発目が出ず、押し直すと撃てる。</item>
    /// <item>Next / Previous は端で折り返す。持ち替えるたびに HUD の武器名が変わる。</item>
    /// <item>Play 中にエラー・例外・Assert のログが出ない。</item>
    /// </list>
    /// すべて満たせば終了コード 0、そうでなければ 1 で終わる。batchmode には入力が無いので、Next / Previous / Attack の代わりに
    /// <see cref="WeaponController.SelectNext"/>・<see cref="WeaponController.PullTrigger"/> などを呼ぶ。
    /// Play Mode に入るので <c>-quit</c> は付けずに、Editor を閉じた状態で次のように呼ぶ:
    /// <code>unity -batchmode -projectPath . -executeMethod Fps.Editor.WeaponSwitchSmokeRun.Run -logFile -</code>
    /// </summary>
    [InitializeOnLoad]
    public static class WeaponSwitchSmokeRun
    {
        public const string LogTag = "[WeaponSwitchSmokeRun]";

        /// <summary>Play Mode に入ってから確かめ始めるまでに待つ秒数。GameBootstrap の Awake と HUD の Start を済ませるため。</summary>
        public const float WarmUpSeconds = 1f;

        /// <summary>Play Mode に入れないまま、これだけ待ったら失敗にする (秒)。</summary>
        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Fps.WeaponSwitchSmokeRun.Active";
        private const string RequestedAtKey = "Fps.WeaponSwitchSmokeRun.RequestedAt";

        private static readonly List<string> _errors = new List<string>();
        private static int _hitLogs;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        static WeaponSwitchSmokeRun()
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
                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                BootstrapSceneBuilder.Build();
                EditorSceneManager.OpenScene(BootstrapSceneBuilder.DefaultScenePath, OpenSceneMode.Single);

                _errors.Clear();
                _hitLogs = 0;
                _playStartedAt = -1d;
                _finished = false;
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{BootstrapSceneBuilder.DefaultScenePath}'");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
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

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
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

            if (condition != null && condition.StartsWith("[Combat] hit zone=", StringComparison.Ordinal))
            {
                _hitLogs++;
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

            var problems = new List<string>();
            try
            {
                Exercise(problems);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                problems.Add($"{e.GetType().Name}: {e.Message}");
            }

            problems.AddRange(_errors);
            Debug.Log($"{LogTag} hit logs={_hitLogs.ToString(CultureInfo.InvariantCulture)} errors={_errors.Count.ToString(CultureInfo.InvariantCulture)}");
            if (problems.Count == 0)
            {
                Finish(SuccessExitCode, "passed");
            }
            else
            {
                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
            }
        }

        /// <summary>シーンのプレイヤーの武器を操作し、期待と違うことを problems に足す。</summary>
        private static void Exercise(List<string> problems)
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null || bootstrap.World == null)
            {
                problems.Add("the scene has no GameBootstrap that built the player");
                return;
            }

            var weapon = bootstrap.World.Weapon;
            var hud = bootstrap.World.Hud;
            if (weapon == null || !weapon.IsInitialized)
            {
                problems.Add("the player's WeaponController is not initialized");
                return;
            }

            // 自動の Update と入れ違いにならないよう、確かめる間は止める (時間は Tick で進める)。
            weapon.enabled = false;

            var ids = new List<string>();
            foreach (var item in weapon.Loadout.Items)
            {
                ids.Add(item.Spec.Id);
            }

            Expect(problems, ids.Count == 2 && ids[0] == WeaponAssetBuilder.RifleId && ids[1] == WeaponAssetBuilder.HandgunId,
                $"the loadout is rifle, handgun (got {string.Join(", ", ids)})");
            if (ids.Count != 2)
            {
                return;
            }

            Expect(problems, weapon.CurrentIndex == 0, "the player starts with the rifle");
            ExpectHudName(problems, hud, weapon);

            // rifle (Auto): 押し続けている間、連射間隔ごとに撃つ。
            var rifle = weapon.Weapon;
            var rifleAmmo = rifle.AmmoInMagazine;
            Expect(problems, weapon.PullTrigger(true, true), "the rifle fires when the trigger is pressed");
            weapon.Tick(rifle.Spec.FireInterval);
            Expect(problems, weapon.PullTrigger(true, false), "the rifle keeps firing while the trigger is held");
            Expect(problems, rifle.AmmoInMagazine == rifleAmmo - 2, $"the rifle used 2 rounds (left {rifle.AmmoInMagazine})");

            Expect(problems, weapon.StartReload(), "the rifle starts reloading");
            Expect(problems, weapon.SelectNext(), "Next switches to the handgun");
            Expect(problems, !rifle.IsReloading, "switching away cancels the rifle's reload");
            Expect(problems, weapon.Weapon.Spec.Id == WeaponAssetBuilder.HandgunId, "the handgun is held after Next");
            ExpectHudName(problems, hud, weapon);

            // handgun (Semi): 押したフレームだけ撃ち、押し続けても 2 発目は出ない。
            var handgun = weapon.Weapon;
            var handgunAmmo = handgun.AmmoInMagazine;
            Expect(problems, weapon.PullTrigger(true, true), "the handgun fires when the trigger is pressed");
            weapon.Tick(handgun.Spec.FireInterval);
            Expect(problems, !weapon.PullTrigger(true, false), "the handgun does not fire again while the trigger is held");
            Expect(problems, weapon.PullTrigger(true, true), "the handgun fires again when the trigger is pressed again");
            Expect(problems, handgun.AmmoInMagazine == handgunAmmo - 2, $"the handgun used 2 rounds (left {handgun.AmmoInMagazine})");

            Expect(problems, weapon.SelectNext() && weapon.CurrentIndex == 0, "Next after the last weapon wraps to the rifle");
            Expect(problems, weapon.Weapon == rifle && rifle.AmmoInMagazine == rifleAmmo - 2, "the rifle keeps its ammo across switches");
            ExpectHudName(problems, hud, weapon);
            Expect(problems, weapon.SelectPrevious() && weapon.CurrentIndex == 1, "Previous before the first weapon wraps to the handgun");
            ExpectHudName(problems, hud, weapon);

            Debug.Log(
                $"{LogTag} rifle ammo={rifle.AmmoInMagazine}/{rifle.ReserveAmmo} handgun ammo={handgun.AmmoInMagazine}/{handgun.ReserveAmmo} hud weapon={(hud != null ? hud.Model.WeaponNameText : "(no hud)")}");
        }

        private static void ExpectHudName(List<string> problems, Hud hud, WeaponController weapon)
        {
            if (hud == null)
            {
                problems.Add("the scene has no HUD");
                return;
            }

            var expected = weapon.Weapon.Spec.DisplayName;
            Expect(problems, hud.Model.WeaponNameText == expected, $"the HUD shows '{expected}' (got '{hud.Model.WeaponNameText}')");
        }

        private static void Expect(List<string> problems, bool condition, string what)
        {
            if (!condition)
            {
                problems.Add(what);
            }
        }

        private static void Finish(int exitCode, string summary)
        {
            _finished = true;
            Detach();
            SessionState.EraseBool(ActiveKey);
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
