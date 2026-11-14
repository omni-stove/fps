using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// <see cref="ModeSceneBuilder"/> の動作確認用シーン ModeTest を組む。最小の Runner (<see cref="TeamDeathmatchRunner"/>、2 陣営) と
    /// チームデスマッチのルール (<see cref="GameRulesAssetBuilder.TeamDeathmatchPath"/>) を使い、配置は土台の既定のまま
    /// (プレイヤーの陣営 0 に bot 2 体、陣営 1 に bot 3 体)。得点・時間・リスポーンは <see cref="DefinitionPath"/> のアセットを Runner に配線して読ませる
    /// (無ければ既定値で作り、あれば値を変えない)。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ModeTestSceneBuilder.Build</code>
    /// </summary>
    public sealed class ModeTestSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.ModeTestScene;
        public static string RulesPathValue => GameRulesAssetBuilder.TeamDeathmatchPath;
        public const string MenuPath = "Fps/Build Mode Test Scene";

        /// <summary>Runner に配線するチームデスマッチの調整値のアセット (<see cref="TeamDeathmatchDefinition"/>)。</summary>
        public static string DefinitionPath => ModeDefinitionAssets.PathOf("TeamDeathmatch");

        public ModeTestSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            new ModeTestSceneBuilder().BuildSceneFromMenu();
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath);

            var runner = host.AddComponent<TeamDeathmatchRunner>();
            runner.SideCount = 2;
            runner.Definition = definition;
            return runner;
        }

        /// <summary>path の調整値のアセットを返す。無ければ <see cref="TeamDeathmatchRunner"/> の既定値で作る。既にあれば値は変えない。</summary>
        public static TeamDeathmatchDefinition LoadOrCreateDefinition(string path)
        {
            return ModeDefinitionAssets.LoadOrCreate<TeamDeathmatchDefinition>(
                path,
                d => d.CopyFrom(new Fps.Core.TeamDeathmatchSpec(
                    TeamDeathmatchRunner.DefaultScoreLimit,
                    TeamDeathmatchRunner.DefaultTimeLimitSeconds,
                    TeamDeathmatchRunner.DefaultRespawnSeconds)));
        }
    }
}
