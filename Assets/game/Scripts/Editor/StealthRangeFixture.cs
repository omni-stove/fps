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

        public const string LeftWallName = "StealthWall_Left";
        public const string RightWallName = "StealthWall_Right";
        public const string RoofName = "StealthRoof";

        public const float WallHeight = 3.5f;
        public const float WallThickness = 0.4f;

        public static readonly Vector3 AreaCenter = new Vector3(-7f, 0f, -16f);

        // 高さは床の上下を含めて焼けるように取る。
        public static readonly Vector3 AreaSize = new Vector3(12f, 8f, 16f);

        public static readonly Vector3 EnemyFeet = new Vector3(-7f, 0f, -22f);

        // 敵の視認距離 (EnemyAgent.DefaultSightRange) より手前だが、遠くからたまり始めるのを確かめられる距離。
        public static readonly Vector3 ApproachStart = new Vector3(-7f, 0f, -9.5f);

        // 出現位置や近くの置く物 (近接の的・パラシュートの塔・崖のぼりの段) に視認距離の内側で接するので、壁と屋根で囲んで見えなくする。
        // 南の端だけは床の端とのあいだを開けておき、手で遊ぶときにそこから入る。
        public static readonly Vector3 WallCenter = new Vector3(-7f, WallHeight / 2f, -8.25f);
        public static readonly Vector3 WallSize = new Vector3(11.9f, WallHeight, WallThickness);

        public static readonly Vector3 LeftWallCenter = new Vector3(-12.75f, WallHeight / 2f, -16f);
        public static readonly Vector3 RightWallCenter = new Vector3(-1.25f, WallHeight / 2f, -16f);
        public static readonly Vector3 SideWallSize = new Vector3(WallThickness, WallHeight, 15.9f);

        // 角度は水平にしか見ないので、高い塔の上は壁だけでは隠れない。
        public static readonly Vector3 RoofCenter = new Vector3(-7f, WallHeight + 0.1f, -16f);
        public static readonly Vector3 RoofSize = new Vector3(11.9f, 0.2f, 15.9f);

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
                SceneBuildHelpers.CreateStaticBox(scene, root.transform, LeftWallName, LeftWallCenter, SideWallSize);
                SceneBuildHelpers.CreateStaticBox(scene, root.transform, RightWallName, RightWallCenter, SideWallSize);
                SceneBuildHelpers.CreateStaticBox(scene, root.transform, RoofName, RoofCenter, RoofSize);

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
