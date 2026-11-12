using System;
using System.Collections.Generic;
using System.IO;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// テスト用ミッション (エリアに到達 → 標的を 3 体倒す → 脱出地点に到達) の定義アセットとシーンを生成する。
    /// プレイヤー・床・ライトは実行時に <see cref="GameBootstrap"/> が作るので、シーンにはミッション用の物だけを置き、
    /// 参照 (MissionRunner の定義、トリガーの目標 Id) はここでコードから設定する。
    /// 保存したシーンは Build Settings の末尾に加える (先頭の起動シーンは変えない)。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -executeMethod Fps.Editor.MissionTestSceneBuilder.Build</code>
    /// </summary>
    public static class MissionTestSceneBuilder
    {
        public const string ScenePath = "Assets/game/Scenes/MissionTest.unity";
        public const string DefinitionPath = "Assets/game/Missions/TestMission.asset";
        public const string MenuPath = "Fps/Build Mission Test Scene";
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

        /// <summary>プレイヤー (原点で +Z を向く) から見て正面 10m。</summary>
        public static readonly Vector3 ReachAreaPosition = new Vector3(0f, 0f, 10f);

        /// <summary>到達エリアのさらに 10m 先に横一列。</summary>
        public static readonly Vector3 TargetsPosition = new Vector3(0f, 0f, 20f);

        public const float TargetSpacing = 4f;

        /// <summary>標的の奥の右手。</summary>
        public static readonly Vector3 ExtractionPosition = new Vector3(12f, 0f, 20f);

        /// <summary>床に置く目印の大きさ。トリガーの高さは <see cref="AreaTriggerHeight"/>。</summary>
        public static readonly Vector3 AreaPadScale = new Vector3(4f, 0.1f, 4f);

        public const float AreaTriggerHeight = 2.5f;

        /// <summary>テスト用ミッションの目標。</summary>
        public static IReadOnlyList<ObjectiveDefinition> TestObjectives()
        {
            return new[]
            {
                new ObjectiveDefinition(ReachAreaId, "前方のエリアに到達する"),
                new ObjectiveDefinition(KillTargetsId, "標的を 3 体倒す", TargetCount, false, ReachAreaId),
                new ObjectiveDefinition(ExtractId, "脱出地点に到達する", 1, false, KillTargetsId),
            };
        }

        /// <summary>
        /// 定義アセットとシーンを既定の場所に生成し、シーンを Build Settings に加える。
        /// 失敗したら例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
        /// </summary>
        [MenuItem(MenuPath)]
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

        /// <summary>definitionPath に定義を、scenePath にシーンを生成・保存する。</summary>
        public static void BuildAt(string scenePath, string definitionPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            definitionPath = ValidateAssetPath(definitionPath);

            var definition = CreateOrUpdateDefinition(definitionPath);

            EnsureFolder(Path.GetDirectoryName(scenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PopulateScene(scene, definition);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the mission test scene to '{scenePath}'");
            }

            EditorBuildSettings.scenes = WithSceneAppended(EditorBuildSettings.scenes, scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{definitionPath}' and '{scenePath}' and registered the scene in Build Settings");
        }

        /// <summary>
        /// path の MissionDefinition をテスト用の目標で上書きする。無ければ作る。既存のアセットは GUID を保つので、
        /// それを参照しているシーンは壊れない。保存前に <see cref="Fps.Core.Mission"/> に変換できることを確かめる。
        /// </summary>
        public static MissionDefinition CreateOrUpdateDefinition(string path)
        {
            path = ValidateAssetPath(path);

            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is MissionDefinition))
            {
                throw new InvalidOperationException(
                    $"'{path}' is a {existing.GetType().Name}, not a {nameof(MissionDefinition)}; move it away first");
            }

            var definition = (MissionDefinition)existing;
            var created = definition == null;
            if (created)
            {
                definition = ScriptableObject.CreateInstance<MissionDefinition>();
            }

            Configure(definition);

            if (created)
            {
                EnsureFolder(Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(definition, path);
            }
            else
            {
                EditorUtility.SetDirty(definition);
            }

            AssetDatabase.SaveAssets();
            return definition;
        }

        /// <summary>definition の中身をテスト用ミッションにし、Mission に変換できることを確かめる。</summary>
        public static void Configure(MissionDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.DisplayName = "Test Mission";
            definition.SetObjectives(TestObjectives());

            // 不正ならここで MissionDefinitionException になり、壊れたアセットを保存しない。
            definition.CreateMission();
        }

        /// <summary>
        /// 空のシーンにテスト用ミッションの物を置き、参照をすべて設定する。Edit Mode では Awake/Start が走らないので、
        /// ミッションは Play したときに始まる。
        /// </summary>
        public static MissionTestLayout PopulateScene(Scene scene, MissionDefinition definition)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a valid, loaded scene", nameof(scene));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (scene.rootCount != 0)
            {
                throw new InvalidOperationException(
                    $"scene '{scene.name}' already has {scene.rootCount} root object(s); build the mission test scene into an empty scene");
            }

            var created = new List<GameObject>();
            try
            {
                var bootstrap = Track(created, new GameObject(BootstrapObjectName, typeof(GameBootstrap)));

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

        /// <summary>
        /// Build Settings のシーン一覧に path を有効な状態で加える。既にあれば位置を変えずに有効にし、
        /// 他のシーンは順番と有効/無効をそのまま残す。
        /// </summary>
        public static EditorBuildSettingsScene[] WithSceneAppended(IReadOnlyList<EditorBuildSettingsScene> current, string path)
        {
            path = BootstrapSceneBuilder.ValidateScenePath(path);

            var scenes = new List<EditorBuildSettingsScene>();
            var found = false;
            if (current != null)
            {
                foreach (var scene in current)
                {
                    if (scene == null || string.IsNullOrEmpty(scene.path))
                    {
                        continue;
                    }

                    if (string.Equals(scene.path.Replace('\\', '/'), path, StringComparison.Ordinal))
                    {
                        if (found)
                        {
                            continue;
                        }

                        found = true;
                        scenes.Add(new EditorBuildSettingsScene(path, true));
                        continue;
                    }

                    scenes.Add(scene);
                }
            }

            if (!found)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            return scenes.ToArray();
        }

        /// <summary>Assets/ 以下の .asset であることを確かめ、区切りを / に揃えて返す。</summary>
        public static string ValidateAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("asset path must not be empty", nameof(path));
            }

            var normalized = path.Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException($"asset path '{path}' must be under Assets/", nameof(path));
            }

            if (!normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"asset path '{path}' must end with .asset", nameof(path));
            }

            foreach (var segment in normalized.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new ArgumentException($"asset path '{path}' must not contain empty, '.' or '..' segments", nameof(path));
                }
            }

            return normalized;
        }

        /// <summary>
        /// 床に置く薄い板の目印。BoxCollider はトリガーにし、板の上に <see cref="AreaTriggerHeight"/> の高さまで伸ばす。
        /// CharacterController が確実にトリガーを通知するよう、動かない kinematic の Rigidbody を付ける。
        /// </summary>
        private static GameObject CreateAreaPad(string name, Vector3 position)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = name;
            pad.transform.SetPositionAndRotation(position + new Vector3(0f, AreaPadScale.y / 2f, 0f), Quaternion.identity);
            pad.transform.localScale = AreaPadScale;

            var collider = pad.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            var height = AreaTriggerHeight / AreaPadScale.y;
            collider.size = new Vector3(1f, height, 1f);
            collider.center = new Vector3(0f, (height - 1f) / 2f, 0f);

            var body = pad.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return pad;
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

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            var guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            if (string.IsNullOrEmpty(guid))
            {
                throw new IOException($"Failed to create the folder '{path}'");
            }
        }
    }

    /// <summary><see cref="MissionTestSceneBuilder.PopulateScene"/> が置いたもの。</summary>
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
