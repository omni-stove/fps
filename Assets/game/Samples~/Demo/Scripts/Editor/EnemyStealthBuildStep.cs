using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public static class EnemyStealthBuildStep
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            SceneBuildHelpers.RegisterEnemyStep(Apply);
        }

        // 相手のルールが Off ならすぐ交戦する (今までどおり) ので、どのモードの敵にも付けておく。
        public static void Apply(GameObject enemy)
        {
            if (enemy != null && enemy.GetComponent<EnemyStealth>() == null)
            {
                enemy.AddComponent<EnemyStealth>();
            }
        }
    }
}
