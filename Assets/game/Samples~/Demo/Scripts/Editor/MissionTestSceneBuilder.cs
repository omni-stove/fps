using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    // プレイヤー・床・ライトは実行時に GameBootstrap が作るので、シーンにはミッション用の物だけを置く。
    public static class MissionTestSceneBuilder
    {
        public static string ScenePath => BuildSceneList.MissionTestScene;
        public const string MissionId = "TestMission";

        public static string DefinitionPath => ShooterKitRoot.OutputPath("Missions/" + MissionId + ".asset");

        public static string MissionNameKey => MissionDefinition.NameKeyOf(MissionId);
        public const string MenuPath = SceneBuildSteps.MenuPath;
        public const string LogTag = "[MissionTestSceneBuilder]";

        public const string ReachAreaId = "reach_area";
        public const string KillTargetsId = "kill_targets";
        public const string ExtractId = "extract";
        public const int TargetCount = 3;

        public const string BootstrapObjectName = "GameBootstrap";
        public const string RunnerObjectName = "MissionRunner";
        public const string ReachAreaObjectName = "ReachArea";
        public const string TargetsObjectName = "Targets";
        public const string TargetObjectNamePrefix = "Target_";
        public const string ExtractionObjectName = "ExtractionPoint";

        public static readonly Vector3 ReachAreaPosition = new Vector3(0f, 0f, 10f);

        public static readonly Vector3 TargetsPosition = new Vector3(0f, 0f, 20f);

        public const float TargetSpacing = 4f;

        public static readonly Vector3 ExtractionPosition = new Vector3(12f, 0f, 20f);

        public static readonly Vector3 AreaPadScale = new Vector3(4f, 0.1f, 4f);

        public const float AreaTriggerHeight = 2.5f;

        public static IReadOnlyList<ObjectiveDefinition> TestObjectives()
        {
            return new[]
            {
                new ObjectiveDefinition(ReachAreaId, MissionDefinition.ObjectiveKeyOf(MissionId, ReachAreaId)),
                new ObjectiveDefinition(KillTargetsId, MissionDefinition.ObjectiveKeyOf(MissionId, KillTargetsId), TargetCount, false, ReachAreaId),
                new ObjectiveDefinition(ExtractId, MissionDefinition.ObjectiveKeyOf(MissionId, ExtractId), 1, false, KillTargetsId),
            };
        }

        public static void Build()
        {
            // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log($"{LogTag} cancelled");
                return;
            }

            BuildAt(ScenePath, DefinitionPath);
        }

        public static void BuildAt(string scenePath, string definitionPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            definitionPath = ValidateAssetPath(definitionPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            // 武器のアセットが無ければ何も保存せずに止める (先に WeaponAssetBuilder.Build を走らせる)。
            var weapons = WeaponAssetBuilder.LoadPlayerLoadout();
            var definition = CreateOrUpdateDefinition(definitionPath);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            PopulateScene(scene, definition, weapons);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the mission test scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{definitionPath}' and '{scenePath}' and registered the scene in Build Settings");
        }

        // 既存のアセットは GUID を保って上書きするので、それを参照しているシーンは壊れない。
        public static MissionDefinition CreateOrUpdateDefinition(string path)
        {
            return MissionDefinitionAssets.CreateOrUpdate(path, Configure);
        }

        public static void Configure(MissionDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.Id = MissionId;
            definition.DisplayNameKey = MissionNameKey;
            definition.SetObjectives(TestObjectives());

            // 不正ならここで MissionDefinitionException になり、壊れたアセットを保存しない。
            definition.CreateMission();
        }

        public static MissionTestLayout PopulateScene(Scene scene, MissionDefinition definition, IReadOnlyList<WeaponDefinition> weapons)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a valid, loaded scene", nameof(scene));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            SceneBuildHelpers.RequirePlayerWeapons(weapons, nameof(weapons));

            if (scene.rootCount != 0)
            {
                throw new InvalidOperationException(
                    $"scene '{scene.name}' already has {scene.rootCount} root object(s); build the mission test scene into an empty scene");
            }

            var created = new List<GameObject>();
            try
            {
                var bootstrap = Track(created, new GameObject(BootstrapObjectName, typeof(GameBootstrap)));
                bootstrap.GetComponent<GameBootstrap>().WeaponDefinitions = weapons;

                var runnerObject = Track(created, new GameObject(RunnerObjectName));
                var runner = runnerObject.AddComponent<MissionRunner>();
                runner.Definition = definition;

                var reach = Track(created, CreateAreaPad(ReachAreaObjectName, ReachAreaPosition));
                var reachTrigger = reach.AddComponent<ReachAreaTrigger>();
                reachTrigger.ObjectiveId = ReachAreaId;
                reachTrigger.Runner = runner;

                var targetsRoot = Track(created, new GameObject(TargetsObjectName));
                targetsRoot.transform.position = TargetsPosition;
                var targets = new Damageable[TargetCount];
                for (var i = 0; i < TargetCount; i++)
                {
                    targets[i] = CreateTarget(targetsRoot.transform, i);
                }

                var kill = targetsRoot.AddComponent<KillTargetObjective>();
                kill.ObjectiveId = KillTargetsId;
                kill.Runner = runner;
                kill.SetTargets(targets);

                var extraction = Track(created, CreateAreaPad(ExtractionObjectName, ExtractionPosition));
                var extractionTrigger = extraction.AddComponent<ReachAreaTrigger>();
                extractionTrigger.ObjectiveId = ExtractId;
                extractionTrigger.Runner = runner;

                foreach (var root in created)
                {
                    SceneManager.MoveGameObjectToScene(root, scene);
                }

                // 床は GameBootstrap が実行時に作るので、床の物でなく、実行時の床 (原点に置く GameBootstrap.FloorScale の Plane) の縁から囲む。
                created.Add(SceneBuildHelpers.CreatePerimeterWalls(scene, null, Vector3.zero, SceneBuildHelpers.PlaneHalfExtents(GameBootstrap.FloorScale)));

                EditorSceneManager.MarkSceneDirty(scene);
                return new MissionTestLayout(bootstrap, runner, reachTrigger, kill, targets, extractionTrigger);
            }
            catch
            {
                foreach (var root in created)
                {
                    Object.DestroyImmediate(root);
                }

                throw;
            }
        }

        public static EditorBuildSettingsScene[] WithSceneAppended(IReadOnlyList<EditorBuildSettingsScene> current, string path)
        {
            return BuildSceneList.WithScene(current, path);
        }

        public static string ValidateAssetPath(string path)
        {
            return SceneBuildHelpers.ValidateAssetPath(path);
        }

        private static GameObject CreateAreaPad(string name, Vector3 position)
        {
            return SceneBuildHelpers.CreateAreaPad(name, position, AreaPadScale, AreaTriggerHeight);
        }

        private static Damageable CreateTarget(Transform parent, int index)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = TargetObjectNamePrefix + (index + 1);
            target.transform.SetParent(parent, false);
            var offset = (index - (TargetCount - 1) / 2f) * TargetSpacing;
            target.transform.localPosition = new Vector3(offset, 1f, 0f);
            target.transform.localRotation = Quaternion.identity;
            return target.AddComponent<Damageable>();
        }

        private static GameObject Track(List<GameObject> created, GameObject gameObject)
        {
            created.Add(gameObject);
            return gameObject;
        }
    }

    public sealed class MissionTestLayout
    {
        public GameObject Bootstrap { get; }
        public MissionRunner Runner { get; }
        public ReachAreaTrigger ReachArea { get; }
        public KillTargetObjective KillTargets { get; }
        public IReadOnlyList<Damageable> Targets { get; }
        public ReachAreaTrigger Extraction { get; }

        public MissionTestLayout(
            GameObject bootstrap,
            MissionRunner runner,
            ReachAreaTrigger reachArea,
            KillTargetObjective killTargets,
            IReadOnlyList<Damageable> targets,
            ReachAreaTrigger extraction)
        {
            Bootstrap = bootstrap;
            Runner = runner;
            ReachArea = reachArea;
            KillTargets = killTargets;
            Targets = targets;
            Extraction = extraction;
        }
    }
}
