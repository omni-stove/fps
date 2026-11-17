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
    // batchmode には入力が無いので、Next / Previous / Attack の代わりに WeaponController.SelectNext・WeaponController.PullTrigger などを呼ぶ。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class WeaponSwitchSmokeRun
    {
        public const string LogTag = "[WeaponSwitchSmokeRun]";

        // GameBootstrap の Awake と HUD の Start を済ませてから確かめ始める。
        public const float WarmUpSeconds = 1f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.WeaponSwitchSmokeRun.Active";
        private const string RequestedAtKey = "Shooter.WeaponSwitchSmokeRun.RequestedAt";

        private static readonly List<string> _errors = new List<string>();
        private static int _hitLogs;
        private static int _viewmodelSpawnLogs;
        private static int _viewmodelSwapLogs;
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

        public static void Run()
        {
            try
            {
                WeaponAssetBuilder.Build();
                new ViewmodelAssetStep().Build();
                GameRulesAssetBuilder.Build();
                BootstrapSceneBuilder.Build();
                EditorSceneManager.OpenScene(BootstrapSceneBuilder.DefaultScenePath, OpenSceneMode.Single);

                _errors.Clear();
                _hitLogs = 0;
                _viewmodelSpawnLogs = 0;
                _viewmodelSwapLogs = 0;
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
            else if (condition != null && condition.StartsWith(ViewmodelPresenter.SpawnedLogPrefix, StringComparison.Ordinal))
            {
                _viewmodelSpawnLogs++;
            }
            else if (condition != null && condition.StartsWith(ViewmodelPresenter.SwappedLogPrefix, StringComparison.Ordinal))
            {
                _viewmodelSwapLogs++;
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

        private static void Exercise(List<string> problems)
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null || bootstrap.World == null)
            {
                problems.Add("the scene has no GameBootstrap that built the player");
                return;
            }

            var weapon = bootstrap.World.Weapon;
            // 武器名は既定の HUD の表示から読む (既定の HUD を止めたゲームなら「HUD が無い」として扱う)
            var hud = DefaultHudFactory.Current;
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
                $"{LogTag} rifle ammo={rifle.AmmoInMagazine}/{rifle.ReserveAmmo} handgun ammo={handgun.AmmoInMagazine}/{handgun.ReserveAmmo} hud weapon={(hud != null ? hud.Model.WeaponSpec?.Id : "(no hud)")}");

            ExerciseViewmodel(problems, bootstrap.World, weapon);
        }

        // 確かめる間は LateUpdate が来ないので、持ち替えと視点の切り替えのたびに Refresh を直接呼ぶ。
        private static void ExerciseViewmodel(List<string> problems, BootstrapWorld world, WeaponController weapon)
        {
            var presenter = world.Player.GetComponent<ViewmodelPresenter>();
            if (presenter == null)
            {
                problems.Add("the player has no ViewmodelPresenter");
                return;
            }

            var rig = world.Rig;
            Expect(problems, rig.Mode == ViewMode.FirstPerson, "the Bootstrap scene starts in first person");
            Expect(problems, _viewmodelSpawnLogs >= 1, "a viewmodel was spawned while warming up in first person");

            presenter.Refresh();
            Expect(problems, presenter.CurrentWeaponId == weapon.Weapon.Spec.Id, $"the viewmodel shows the held weapon (got '{presenter.CurrentWeaponId}')");
            Expect(problems, presenter.Sockets.Muzzle != null, "WeaponSockets finds the muzzle socket in the viewmodel");

            var swaps = _viewmodelSwapLogs;
            weapon.SelectNext();
            presenter.Refresh();
            Expect(problems, _viewmodelSwapLogs == swaps + 1, $"switching weapons logs one viewmodel swap (got {_viewmodelSwapLogs - swaps})");
            Expect(problems, presenter.CurrentWeaponId == weapon.Weapon.Spec.Id, "the viewmodel follows the switch");

            rig.Mode = ViewMode.ThirdPerson;
            presenter.Refresh();
            Expect(problems, presenter.Instance == null, "third person shows no viewmodel");
            var spawnsInThirdPerson = _viewmodelSpawnLogs;
            var swapsInThirdPerson = _viewmodelSwapLogs;
            weapon.SelectNext();
            presenter.Refresh();
            Expect(problems, presenter.Instance == null, "switching in third person shows no viewmodel");
            Expect(problems, _viewmodelSpawnLogs == spawnsInThirdPerson && _viewmodelSwapLogs == swapsInThirdPerson,
                "third person logs no viewmodel spawn or swap");

            rig.Mode = ViewMode.FirstPerson;
            presenter.Refresh();
            Expect(problems, presenter.CurrentWeaponId == weapon.Weapon.Spec.Id, "returning to first person shows the viewmodel again");
            Expect(problems, _viewmodelSpawnLogs == spawnsInThirdPerson + 1, "returning to first person logs a spawn");

            Debug.Log(
                $"{LogTag} viewmodel spawns={_viewmodelSpawnLogs.ToString(CultureInfo.InvariantCulture)} swaps={_viewmodelSwapLogs.ToString(CultureInfo.InvariantCulture)}");
        }

        private static void ExpectHudName(List<string> problems, Hud hud, WeaponController weapon)
        {
            if (hud == null)
            {
                problems.Add("the scene has no HUD");
                return;
            }

            var expected = weapon.Weapon.Spec;
            Expect(problems, ReferenceEquals(hud.ShownModel.WeaponSpec, expected), $"the HUD shows '{expected.Id}' (got '{hud.ShownModel.WeaponSpec?.Id}')");
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
