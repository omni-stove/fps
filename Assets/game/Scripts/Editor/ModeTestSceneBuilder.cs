using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// <see cref="ModeSceneBuilder"/> の動作確認用シーン ModeTest を組む。最小の Runner (<see cref="TeamDeathmatchRunner"/>、2 陣営) と
    /// チームデスマッチのルール (<see cref="GameRulesAssetBuilder.TeamDeathmatchPath"/>) を使い、配置は土台の既定のまま
    /// (プレイヤーの陣営 0 に bot 2 体、陣営 1 に bot 3 体)。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ModeTestSceneBuilder.Build</code>
    /// </summary>
    public sealed class ModeTestSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.ModeTestScene;
        public static string RulesPathValue => GameRulesAssetBuilder.TeamDeathmatchPath;
        public const string MenuPath = "Fps/Build Mode Test Scene";

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
            var runner = host.AddComponent<TeamDeathmatchRunner>();
            runner.SideCount = 2;
            return runner;
        }
    }
}
