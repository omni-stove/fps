using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net.Tests
{
    /// <summary>
    /// コミットされた NetTest シーン (Fps.Editor.NetSceneBuilder.Build の出力) の参照が解決できているか。
    /// 失敗したら Build をやり直してシーンとプレハブをコミットする。
    /// </summary>
    public sealed class NetTestSceneTests
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(NetBootstrap.ScenePath), Is.Not.Null,
                NetBootstrap.ScenePath + " is missing; run Fps.Editor.NetSceneBuilder.Build");
            _scene = EditorSceneManager.OpenScene(NetBootstrap.ScenePath, OpenSceneMode.Additive);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void HasOneBootstrapWithNetworkedPlayerPrefab()
        {
            var bootstraps = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .ToArray();

            Assert.That(bootstraps, Has.Length.EqualTo(1));
            var bootstrap = bootstraps[0];
            Assert.That(bootstrap.transform.parent, Is.Null, "NetworkManager needs a root GameObject");
            Assert.That(bootstrap.PlayerPrefab, Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabAsset(bootstrap.PlayerPrefab), Is.True);

            var networkObject = bootstrap.PlayerPrefab.GetComponent<NetworkObject>();
            Assert.That(networkObject, Is.Not.Null);
            Assert.That(bootstrap.PlayerPrefab.GetComponent<NetPlayer>(), Is.Not.Null);
            Assert.That(bootstrap.PlayerPrefab.GetComponent<NetPlayerWeapon>(), Is.Not.Null,
                "NetPlayer has no NetPlayerWeapon; run Fps.Editor.NetSceneBuilder.Build");
            Assert.That(bootstrap.PlayerPrefab.GetComponent<NetworkTransform>(), Is.Null,
                "NetworkPlayerMotor syncs the position; a NetworkTransform would fight it");
        }

        [Test]
        public void HasMatchPrefabWithTeamDeathmatch()
        {
            var bootstrap = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .Single();

            Assert.That(bootstrap.MatchPrefab, Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabAsset(bootstrap.MatchPrefab), Is.True);
            Assert.That(bootstrap.MatchPrefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(bootstrap.MatchPrefab.GetComponent<NetTeamDeathmatch>(), Is.Not.Null);
            Assert.That(bootstrap.MatchPrefab.GetComponent<NetCombatFeed>(), Is.Not.Null,
                "NetMatch has no NetCombatFeed; run Fps.Editor.NetSceneBuilder.Build");
        }

        [Test]
        public void MatchPrefabTakesWeaponsFromTheDefaultLoadoutAsset()
        {
            var match = MatchPrefabComponent();

            Assert.That(match.Loadout, Is.Not.Null, "the match has no loadout; run Fps.Editor.NetSceneBuilder.Build");
            Assert.That(AssetDatabase.GetAssetPath(match.Loadout), Is.EqualTo(Fps.Editor.WeaponAssetBuilder.LoadoutPath));

            // サーバーの起動時と同じ検査に通り、0 番は rifle、1 番は handgun
            Assert.That(NetTeamDeathmatch.TryCreateLoadout(match.Loadout, out var loadout, out var error), Is.True, error);
            Assert.That(loadout.Weapons.Select(w => w.Id), Is.EqualTo(new[] { Fps.Editor.WeaponAssetBuilder.RifleId, Fps.Editor.WeaponAssetBuilder.HandgunId }));
        }

        [Test]
        public void MatchPrefabLetsPlayersChooseFromTheWeaponCatalog()
        {
            var match = MatchPrefabComponent();

            Assert.That(match.Catalog, Is.Not.Null, "the match has no weapon catalog; run Fps.Editor.NetSceneBuilder.Build");
            Assert.That(AssetDatabase.GetAssetPath(match.Catalog), Is.EqualTo(Fps.Editor.WeaponAssetBuilder.CatalogPath));

            // サーバーの起動時と同じ検査に通り、テスト用のメニューの並びは片方だけが通る
            Assert.That(NetTeamDeathmatch.TryCreateLoadout(match.Loadout, match.Catalog, out var loadout, out var error), Is.True, error);
            Assert.That(loadout.TryChoose(1, Fps.Editor.NetLoadoutDebugMenu.ValidWeaponIds, out var reason), Is.True, reason);
            Assert.That(loadout.TryChoose(2, Fps.Editor.NetLoadoutDebugMenu.InvalidWeaponIds, out reason), Is.False);
            Assert.That(reason, Does.Contain("slot 0 does not accept Handgun"));
        }

        [Test]
        public void MatchPrefabTakesRulesFromTheTeamDeathmatchAsset()
        {
            var match = MatchPrefabComponent();

            Assert.That(match.Rules, Is.Not.Null, "the match has no game rules; run Fps.Editor.NetSceneBuilder.Build");
            Assert.That(AssetDatabase.GetAssetPath(match.Rules), Is.EqualTo(Fps.Editor.GameRulesAssetBuilder.TeamDeathmatchPath));

            // サーバーの起動時と同じ検査に通り、既定のルールに置き換わらない
            Assert.That(NetTeamDeathmatch.TryLoadRules(match.Rules, out var rules, out var usedDefault, out var error), Is.True, error);
            Assert.That(usedDefault, Is.False);
            Assert.That(rules.DeathCamera, Is.EqualTo(Fps.Editor.GameRulesAssetBuilder.TeamDeathmatchRules().DeathCamera));

            // 専用サーバーのビルドにも、NetTest シーンから参照をたどって入る
            Assert.That(
                Fps.Editor.ServerBuilder.MissingFromScene(NetBootstrap.ScenePath, new[] { Fps.Editor.GameRulesAssetBuilder.TeamDeathmatchPath }),
                Is.Empty);
        }

        [Test]
        public void DedicatedServerBuildIncludesTheLoadoutAndItsWeapons()
        {
            var paths = Fps.Editor.ServerBuilder.LoadoutAssetPaths(MatchPrefabComponent().Loadout);

            Assert.That(paths, Does.Contain(Fps.Editor.WeaponAssetBuilder.LoadoutPath));
            Assert.That(paths, Does.Contain(Fps.Editor.WeaponAssetBuilder.RiflePath));
            Assert.That(paths, Does.Contain(Fps.Editor.WeaponAssetBuilder.HandgunPath));

            // 専用サーバーに入れるのは NetTest シーンだけなので、そこから参照をたどって届けばビルドに入る
            Assert.That(Fps.Editor.ServerBuilder.MissingFromScene(NetBootstrap.ScenePath, paths), Is.Empty);
        }

        [Test]
        public void SpawnPointsCanStartTeamDeathmatch()
        {
            var points = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetSpawnPoint>(true))
                .Select(marker => marker.ToSpawnPoint())
                .ToArray();

            Assert.That(points.Any(p => p.Team == Team.Alpha), Is.True);
            Assert.That(points.Any(p => p.Team == Team.Bravo), Is.True);

            // 実行時と同じ組み立てで例外が出ないこと (どちらのチームも使える地点がある)
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(10, 300f, 3f), 100, points, new SpawnSelector(), _ => System.Numerics.Vector3.Zero);
            Assert.That(session.Join(0), Is.EqualTo(Team.Alpha));
            Assert.That(session.Join(1), Is.EqualTo(Team.Bravo));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void MatchPrefabRestartsTenSecondsAfterEnd()
        {
            var match = MatchPrefabComponent();

            Assert.That(match.RestartDelaySeconds, Is.EqualTo(NetTeamDeathmatch.DefaultRestartDelaySeconds));
            Assert.That(NetTeamDeathmatch.DefaultRestartDelaySeconds, Is.EqualTo(10f));
        }

        [Test]
        public void EndedMatchReturnsToPlayingAfterRestartDelayAndRespawnsEveryone()
        {
            var match = MatchPrefabComponent();
            var points = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetSpawnPoint>(true))
                .Select(marker => marker.ToSpawnPoint())
                .ToArray();
            var positions = new Dictionary<int, System.Numerics.Vector3>();
            var spawned = new Dictionary<int, SpawnPoint>();

            // NetTeamDeathmatch.StartSession と同じ値で組み立てる
            Assert.That(NetTeamDeathmatch.TryLoadRules(match.Rules, out var rules, out _, out var rulesError), Is.True, rulesError);
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(match.ScoreLimit, match.TimeLimitSeconds, match.RespawnSeconds, rules),
                match.MaxHealth,
                points,
                new SpawnSelector(match.MinEnemySpawnDistance),
                id => positions[id],
                match.RestartDelaySeconds);
            session.Spawned += (id, point) =>
            {
                positions[id] = point.Position;
                spawned[id] = point;
            };

            var playerIds = new[] { 0, 1, 2, 3 };
            foreach (var id in playerIds)
            {
                session.Join(id);
            }

            // 点数・キル数・デス数が付き、傷ついた人と出現地点から動いた人がいる状態で時間切れにする
            session.ApplyDamage(0, 1, match.MaxHealth);
            session.ApplyDamage(1, 2, 1);
            foreach (var id in playerIds)
            {
                positions[id] = new System.Numerics.Vector3(1000f, 0f, 1000f);
            }

            session.Tick(match.TimeLimitSeconds);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(session.RestartRemainingSeconds, Is.EqualTo(match.RestartDelaySeconds));

            var teams = playerIds.ToDictionary(id => id, session.GetTeam);
            spawned.Clear();

            session.Tick(match.RestartDelaySeconds / 2f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(spawned, Is.Empty);

            session.Tick(match.RestartDelaySeconds / 2f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.RemainingSeconds, Is.EqualTo(match.TimeLimitSeconds));
            Assert.That(session.GetScore(Team.Alpha), Is.Zero);
            Assert.That(session.GetScore(Team.Bravo), Is.Zero);
            Assert.That(spawned.Keys, Is.EquivalentTo(playerIds));
            foreach (var id in playerIds)
            {
                Assert.That(session.GetTeam(id), Is.EqualTo(teams[id]), "team of " + id);
                Assert.That(session.GetHealth(id), Is.EqualTo(match.MaxHealth), "health of " + id);
                Assert.That(session.GetKills(id), Is.Zero, "kills of " + id);
                Assert.That(session.GetDeaths(id), Is.Zero, "deaths of " + id);
                Assert.That(spawned[id].IsUsableBy(teams[id]), Is.True, "spawn point of " + id);
                Assert.That(positions[id], Is.EqualTo(spawned[id].Position), "position of " + id);
            }
        }

        private NetTeamDeathmatch MatchPrefabComponent()
        {
            var bootstrap = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .Single();
            Assert.That(bootstrap.MatchPrefab, Is.Not.Null);
            var match = bootstrap.MatchPrefab.GetComponent<NetTeamDeathmatch>();
            Assert.That(match, Is.Not.Null);
            return match;
        }

        [Test]
        public void PlayerPrefabMovesWithCharacterController()
        {
            var bootstrap = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .Single();
            var prefab = bootstrap.PlayerPrefab;

            Assert.That(prefab.GetComponent<NetworkPlayerMotor>(), Is.Not.Null,
                "NetPlayer has no NetworkPlayerMotor; run Fps.Editor.NetSceneBuilder.Build");
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);

            // CharacterController と別のコライダーがあると自分自身にぶつかる
            Assert.That(prefab.GetComponents<Collider>(), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
        }

        [Test]
        public void PlayerPrefabCanCrouchWithBodyAndEye()
        {
            var prefab = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .Single()
                .PlayerPrefab;

            var body = prefab.transform.Find(NetStance.BodyName);
            Assert.That(body, Is.Not.Null, "NetPlayer has no '" + NetStance.BodyName + "'; run Fps.Editor.NetSceneBuilder.Build");
            Assert.That(body.GetComponent<Renderer>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Renderer>(), Is.Null, "the mesh must be on the body child so crouching squashes only the look");

            // プレハブの寸法でしゃがみの寸法が作れる (NetworkPlayerMotor の Awake と同じ)
            var stance = NetStance.FromHierarchy(prefab.GetComponent<CharacterController>(), Fps.Runtime.PlayerController.DefaultCrouchHeight);
            Assert.That(stance.Eye, Is.SameAs(prefab.transform.Find(NetPlayer.ViewName)));
            Assert.That(stance.Body, Is.SameAs(body));
            Assert.That(stance.Geometry.Standing.EyeY, Is.EqualTo(NetPlayer.EyeHeight).Within(1e-4f));
        }

        [Test]
        public void PlayerPrefabHasDisabledViewWithEyeAtEyeHeight()
        {
            var prefab = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .Single()
                .PlayerPrefab;

            var view = prefab.transform.Find(NetPlayer.ViewName);
            Assert.That(view, Is.Not.Null, "NetPlayer has no eye; run Fps.Editor.NetSceneBuilder.Build");
            Assert.That(view.parent, Is.EqualTo(prefab.transform), "yaw turns the body and pitch turns this child");
            Assert.That(view.localPosition, Is.EqualTo(new Vector3(0f, NetPlayer.EyeHeight, 0f)),
                "the eye is the shot origin, which the server checks with NetPlayer.EyeHeight");

            var cameraObject = view.Find(NetPlayer.ViewCameraName);
            Assert.That(cameraObject, Is.Not.Null, "the camera sits under the eye so a third-person offset keeps the shot at the eye");
            var camera = cameraObject.GetComponent<Camera>();
            var listener = cameraObject.GetComponent<AudioListener>();
            var look = prefab.GetComponent<Fps.Runtime.FirstPersonLook>();
            var rig = prefab.GetComponent<Fps.Runtime.ViewRig>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(listener, Is.Not.Null);
            Assert.That(look, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.Mode, Is.EqualTo(Fps.Editor.NetSceneBuilder.PlayerViewMode));
            Assert.That(Fps.Runtime.FirstPersonLook.FindChildCamera(prefab.transform), Is.EqualTo(camera));
            Assert.That(Fps.Runtime.ViewRig.FindEye(prefab.transform), Is.EqualTo(view));

            // 他人のプレイヤーで映ったり音を拾ったりしないよう、NetPlayer が自分のプレイヤーでだけ有効にする
            Assert.That(camera.enabled, Is.False);
            Assert.That(listener.enabled, Is.False);
            Assert.That(look.enabled, Is.False);
            Assert.That(rig.enabled, Is.False);

            var ads = prefab.GetComponent<Fps.Runtime.AimDownSights>();
            Assert.That(ads, Is.Not.Null, "NetPlayer has no AimDownSights; run Fps.Editor.NetSceneBuilder.Build");
            Assert.That(ads.enabled, Is.False, "only the own player zooms its camera");

            Assert.That(prefab.GetComponent<NetDeathCamera>(), Is.Not.Null,
                "NetPlayer has no death camera; run Fps.Editor.NetSceneBuilder.Build");
        }

        [Test]
        public void HasSpectatorCameraForWhenNoPlayerIsLocal()
        {
            var spectators = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetSpectatorCamera>(true))
                .ToArray();

            Assert.That(spectators, Has.Length.EqualTo(1));
            Assert.That(spectators[0].GetComponent<Camera>(), Is.Not.Null);
            Assert.That(spectators[0].GetComponent<Camera>().enabled, Is.True);
        }

        [Test]
        public void HasNoNetworkManagerSavedInScene()
        {
            // 構成はコードで行うので、シーンに保存された NetworkManager があれば二重になる
            var managers = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetworkManager>(true));

            Assert.That(managers, Is.Empty);
        }

        [Test]
        public void IsInBuildSettings()
        {
            var entry = EditorBuildSettings.scenes.FirstOrDefault(s => s.path == NetBootstrap.ScenePath);

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.enabled, Is.True);
        }

        [Test]
        public void BuildSceneListNamesTheSceneTheRuntimeLoads()
        {
            Assert.That(Fps.Editor.BuildSceneList.NetTestScene, Is.EqualTo(NetBootstrap.ScenePath));
            Assert.That(Fps.Editor.BuildSceneList.Scenes, Has.Member(NetBootstrap.ScenePath));
            Assert.That(Fps.Editor.ServerBuilder.ServerScenePaths(), Is.EqualTo(new[] { NetBootstrap.ScenePath }));
        }

        [Test]
        public void PlayerPrefabHasThePresentationAndGrenadeComponents()
        {
            var prefab = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .Single()
                .PlayerPrefab;

            const string rerun = "run Fps.Editor.NetSceneBuilder.Build";
            Assert.That(prefab.GetComponent<Fps.Runtime.WeaponPresentation>(), Is.Not.Null, rerun);
            Assert.That(prefab.GetComponent<Fps.Runtime.CharacterAudio>(), Is.Not.Null, rerun);
            Assert.That(prefab.GetComponent<Fps.Runtime.CharacterAnimator>(), Is.Not.Null, rerun);
            Assert.That(prefab.GetComponent<Fps.Runtime.GrenadeThrower>(), Is.Not.Null, rerun);
        }
    }
}
