using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 旗と台の形は FlagObject.ModelPrefab・FlagBase.MarkerPrefab を空にして置き、Play 中に仮の形を作らせる。ゲームはシーンの Inspector で差し替える。
    public sealed class CaptureTheFlagSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.CaptureTheFlagScene;
        public static string RulesPathValue => GameRulesAssetBuilder.CaptureTheFlagPath;

        public static string DefinitionPath => ModeDefinitionAssets.PathOf(GameRulesAssetBuilder.CaptureTheFlagName);

        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const string BasesRootName = "FlagBases";
        public const string FlagsRootName = "Flags";

        public const float BaseDepth = 5f;

        // CaptureTheFlag.FlagSides の順 (Alpha、Bravo) に並べる。どちらも床の中央の列 (x = 0) で自陣の奥に置き、両陣営から同じ距離にする。
        public static readonly IReadOnlyList<Vector3> BaseCenters = new[]
        {
            new Vector3(0f, 0f, DefaultNearSpawnZ - BaseDepth),
            new Vector3(0f, 0f, DefaultFarSpawnZ + BaseDepth),
        };

        public CaptureTheFlagSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        public static void Build()
        {
            new CaptureTheFlagSceneBuilder().BuildSceneFromMenu();
        }

        public static string BaseObjectName(Side side)
        {
            return "FlagBase-" + side.Id;
        }

        public static string FlagObjectName(Side side)
        {
            return "Flag-" + side.Id;
        }

        public static CaptureTheFlagDefinition LoadOrCreateDefinition(string path)
        {
            var definition = ModeDefinitionAssets.LoadOrCreate<CaptureTheFlagDefinition>(path, _ => { });
            definition.ToSpec();
            return definition;
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath);

            var runner = host.AddComponent<CaptureTheFlagRunner>();
            runner.Definition = definition;
            return runner;
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            if (!(context.Runner is CaptureTheFlagRunner runner))
            {
                throw new InvalidOperationException($"{GetType().Name}.AddRunner must add a CaptureTheFlagRunner");
            }

            SceneBuildHelpers.RequireOnNavMesh(BaseCenters, NavMeshCheckRadius, "flag base");
            SceneBuildHelpers.RequireReachable(PlayerFeet, BaseCenters, NavMeshCheckRadius, "flag base");

            var captureRadius = runner.Definition != null ? runner.Definition.CaptureRadius : runner.CaptureRadius;

            var basesRoot = new GameObject(BasesRootName);
            SceneManager.MoveGameObjectToScene(basesRoot, context.Scene);
            var flagsRoot = new GameObject(FlagsRootName);
            SceneManager.MoveGameObjectToScene(flagsRoot, context.Scene);

            var sides = CaptureTheFlag.FlagSides;
            var bases = new FlagBase[sides.Count];
            var flags = new FlagObject[sides.Count];
            for (var i = 0; i < sides.Count; i++)
            {
                var side = sides[i];

                var baseObject = new GameObject(BaseObjectName(side));
                baseObject.transform.SetParent(basesRoot.transform, false);
                baseObject.transform.position = BaseCenters[i];
                var flagBase = baseObject.AddComponent<FlagBase>();
                flagBase.Side = side;
                flagBase.Radius = captureRadius;
                bases[i] = flagBase;

                // 運ばれると運ぶ人に付いて動くので、台の子にはしない。
                var flagObject = new GameObject(FlagObjectName(side));
                flagObject.transform.SetParent(flagsRoot.transform, false);
                flagObject.transform.position = BaseCenters[i];
                var flag = flagObject.AddComponent<FlagObject>();
                flag.Side = side;
                flags[i] = flag;
            }

            runner.SetBases(bases);
            runner.SetFlagObjects(flags);
        }
    }
}
