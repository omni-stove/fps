using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    /// <summary>
    /// 旗取り (<see cref="CaptureTheFlagRunner"/>) のシーン CaptureTheFlag を組む。ルールは <see cref="GameRulesAssetBuilder.CaptureTheFlagPath"/>
    /// (個別リスポーン・ダウンなし)。配置は土台の既定 (プレイヤーの陣営 0 = Alpha に bot 2 体、陣営 1 = Bravo に bot 3 体で 3 対 3) に、
    /// 両陣営の出現点の後ろに旗の台 (<see cref="FlagBase"/>、<see cref="BaseCenters"/>) と旗の見た目 (<see cref="FlagObject"/>) を足す。
    /// 旗と台の形は <see cref="FlagObject.ModelPrefab"/>・<see cref="FlagBase.MarkerPrefab"/> を空にして置くので、Play 中に仮の形が作られる
    /// (ゲームはシーンの Inspector で差し替える)。得点の上限や旗が戻る秒数は <see cref="DefinitionPath"/> のアセットを Runner に配線して読ませる
    /// (無ければ既定値で作り、あれば値を変えない)。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Shooter.Editor.CaptureTheFlagSceneBuilder.Build</code>
    /// </summary>
    public sealed class CaptureTheFlagSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.CaptureTheFlagScene;
        public static string RulesPathValue => GameRulesAssetBuilder.CaptureTheFlagPath;

        /// <summary>Runner に配線する調整値のアセット (<see cref="CaptureTheFlagDefinition"/>)。</summary>
        public static string DefinitionPath => ModeDefinitionAssets.PathOf(GameRulesAssetBuilder.CaptureTheFlagName);

        public const string MenuPath = "Shooter/Build Capture The Flag Scene";

        public const string BasesRootName = "FlagBases";
        public const string FlagsRootName = "Flags";

        /// <summary>出現点の列から台までの、自陣の奥への距離 (m)。</summary>
        public const float BaseDepth = 5f;

        /// <summary>
        /// 陣営ごとの台の足元。<see cref="CaptureTheFlag.FlagSides"/> の順 (Alpha、Bravo)。どちらも床の中央の列 (x = 0) で、
        /// 自陣の出現点の列から <see cref="BaseDepth"/> だけ奥 (Alpha は z = -5、Bravo は z = 45) にあり、両陣営から同じ距離。
        /// </summary>
        public static readonly IReadOnlyList<Vector3> BaseCenters = new[]
        {
            new Vector3(0f, 0f, DefaultNearSpawnZ - BaseDepth),
            new Vector3(0f, 0f, DefaultFarSpawnZ + BaseDepth),
        };

        public CaptureTheFlagSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            new CaptureTheFlagSceneBuilder().BuildSceneFromMenu();
        }

        /// <summary>side の台の GameObject の名前。</summary>
        /// <example><c>BaseObjectName(Side.Bravo)</c> は <c>FlagBase-1</c></example>
        public static string BaseObjectName(Side side)
        {
            return "FlagBase-" + side.Id;
        }

        /// <summary>side の旗の GameObject の名前。</summary>
        /// <example><c>FlagObjectName(Side.Alpha)</c> は <c>Flag-0</c></example>
        public static string FlagObjectName(Side side)
        {
            return "Flag-" + side.Id;
        }

        /// <summary>
        /// path の調整値のアセットを返す。無ければ <see cref="CaptureTheFlagDefinition"/> の既定値で作る。既にあれば値は変えず、通ることだけ確かめる。
        /// </summary>
        /// <exception cref="InvalidOperationException">既にあるアセットの値が不正か、path に別の型のアセットがある。</exception>
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

            // 焼き終えた NavMesh で、旗を取りに・運びに台へ歩いて行けることを確かめる (行けなければ保存しない)。
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
