using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    /// <summary>シーン生成スクリプトのテストが、空のシーンを今のシーンに足して使うための入口。</summary>
    public static class TestScenes
    {
        /// <summary>Untitled シーンの代わりに開いておく、保存済みの空のシーン。Assets の外なのでアセットにはならない。</summary>
        public const string BaseScenePath = "Temp/FpsTestBase.unity";

        /// <summary>
        /// 空のシーンを Additive で作って返す。使い終えたら <c>EditorSceneManager.CloseScene(scene, true)</c> で閉じる。
        /// テストの SetUp では、Single でシーンを開き直すことがあるので、ScriptableObject を作るより先に呼ぶ。
        /// </summary>
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

        /// <summary>
        /// 保存済みのシーンでプレイヤーに配線されているべき武器 (rifle, handgun のアセット)。
        /// どちらかのアセットが無ければ null を含むので、比べると失敗する (先に WeaponAssetBuilder.Build を走らせる)。
        /// </summary>
        public static Fps.Runtime.WeaponDefinition[] ExpectedPlayerWeapons()
        {
            return new[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<Fps.Runtime.WeaponDefinition>(Fps.Editor.WeaponAssetBuilder.RiflePath),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Fps.Runtime.WeaponDefinition>(Fps.Editor.WeaponAssetBuilder.HandgunPath),
            };
        }

        private static void OpenBaseScene()
        {
            if (File.Exists(BaseScenePath))
            {
                EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, BaseScenePath);
        }
    }
}
