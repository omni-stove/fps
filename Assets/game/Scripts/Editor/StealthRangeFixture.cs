using System;
using Fps.Core;
using Fps.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 組み込みの手順でプレイヤーが立つ所 (出現位置・乗り越えの壁・開けた場所) から敵の視認距離より離して置く。近いと、ほかの手順の途中で見つかって撃たれる。
    public sealed class StealthRangeFixture : ITpsRangeFixture
    {
        public const string AreaName = "StealthArea";
        public const string EnemyName = "StealthEnemy";
        public const string EnemyId = "stealth-enemy";
        public const string WallName = "StealthWall";

        public const float EnemyHeight = 2f;
        public const float EnemyRadius = 0.5f;
        public const float EnemySpeed = 2.5f;
        public const int EnemyMaxHealth = 100;

        public static readonly Vector3 AreaCenter = new Vector3(-18.5f, 0f, -16f);

        // 高さは床の上下を含めて焼けるように取る。
        public static readonly Vector3 AreaSize = new Vector3(12f, 8f, 16f);

        public static readonly Vector3 EnemyFeet = new Vector3(-18.5f, 0f, -22f);

        // 敵の視認距離 (EnemyAgent.DefaultSightRange) より手前だが、遠くからたまり始めるのを確かめられる距離。
        public static readonly Vector3 ApproachStart = new Vector3(-18.5f, 0f, -9f);

        public static readonly Vector3 WallCenter = new Vector3(-22f, 1.5f, -13f);
        public static readonly Vector3 WallSize = new Vector3(4f, 3f, 0.4f);

        public int Order => 100;

        public string Name => AreaName;

        public Bounds Footprint => new Bounds(AreaCenter + (Vector3.up * 2f), new Vector3(AreaSize.x, 4f, AreaSize.z));

        // 敵の武器は射撃場のプレイヤーの最初の武器を使う。置く物には武器が渡されないので、先に置かれた GameBootstrap から引く。
        public GameObject Place(Scene scene, GameRules rules)
        {
            var weapon = FindBootstrapWeapon(scene);
            var root = new GameObject(AreaName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.transform.SetPositionAndRotation(AreaCenter, Quaternion.identity);
                SceneBuildHelpers.CreateStaticBox(scene, root.transform, WallName, WallCenter, WallSize);

                var enemy = SceneBuildHelpers.CreateEnemy(
                    scene,
                    root.transform,
                    EnemyName,
                    EnemyId,
                    EnemyFeet,
                    Quaternion.LookRotation(Vector3.forward, Vector3.up),
                    Array.Empty<Vector3>(),
                    new EnemyBody(EnemyHeight, EnemyRadius, EnemySpeed, EnemyMaxHealth),
                    weapon);
                enemy.gameObject.SetActive(false);

                var area = root.AddComponent<RuntimeNavMeshArea>();
                area.Size = AreaSize;
                area.AgentTypeId = SceneBuildHelpers.HumanoidAgentTypeId;
                area.ActivateAfterBuild = new[] { enemy.gameObject };

                EditorSceneManager.MarkSceneDirty(scene);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static WeaponDefinition FindBootstrapWeapon(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var bootstrap = root.GetComponent<GameBootstrap>();
                if (bootstrap != null && bootstrap.WeaponDefinitions != null)
                {
                    foreach (var weapon in bootstrap.WeaponDefinitions)
                    {
                        if (weapon != null)
                        {
                            return weapon;
                        }
                    }
                }
            }

            throw new InvalidOperationException($"{TpsRangeSceneBuilder.LogTag} {nameof(StealthRangeFixture)} needs a GameBootstrap with a weapon placed before it");
        }
    }
}
