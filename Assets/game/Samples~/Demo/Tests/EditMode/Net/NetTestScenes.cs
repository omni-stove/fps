using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Shooter.Net.Tests
{
    // Shooter.Samples.Tests の TestScenes を参照すると Net のテストが Demo のテスト全体に依存するので、同じ手順をここに持つ。
    public static class NetTestScenes
    {
        // Assets の外に置くので、保存してもアセットにはならない。
        public const string BaseScenePath = "Temp/ShooterTestBase.unity";

        public static Scene NewAdditive()
        {
            // 保存されていない Untitled シーンがあると Additive の NewScene は例外を投げ、batchmode のテストは Untitled から始まる。
            // Untitled を保存済みの空のシーンに置き換えれば、以後のテストはそのまま Additive で足せる。
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                {
                    OpenBaseScene();
                    break;
                }
            }

            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        // Temp は AssetDatabase に取り込まれず OpenScene で開き直せないので、毎回作って保存し直す。
        private static void OpenBaseScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, BaseScenePath);
        }
    }
}
