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

        public static readonly Vector3 AreaCenter = new Vector3(-7f, 0f, 17.25f);

        // 高さは床の上下を含めて焼けるように取る。
        public static readonly Vector3 AreaSize = new Vector3(5f, 8f, 13.5f);

        public static readonly Vector3 EnemyFeet = new Vector3(-7f, 0f, 22.5f);

        // 床の奥の端に置くので、端を背にして出現位置の側を向かせる。
        public static readonly Quaternion EnemyFacing = Quaternion.LookRotation(Vector3.back, Vector3.up);

        // 敵の視認距離 (EnemySpec.DefaultSightRange) より手前だが、遠くからたまり始めるのを確かめられる距離。
        public static readonly Vector3 ApproachStart = new Vector3(-7f, 0f, 11.5f);

        // 標的ダミーの列や近くの置く物 (怯みの的・ボス) に視認距離の内側で接するので、壁と屋根で囲んで見えなくする。
        // 北の端だけは床の端とのあいだを開けておき、手で遊ぶときにそこから入る。
        public static readonly Vector3 WallCenter = new Vector3(-7f, WallHeight / 2f, 10.75f);
        public static readonly Vector3 WallSize = new Vector3(4.9f, WallHeight, WallThickness);

        public static readonly Vector3 LeftWallCenter = new Vector3(-9.25f, WallHeight / 2f, 17.25f);
        public static readonly Vector3 RightWallCenter = new Vector3(-4.75f, WallHeight / 2f, 17.25f);
        public static readonly Vector3 SideWallSize = new Vector3(WallThickness, WallHeight, 13.4f);

        // 角度は水平にしか見ないので、高い塔の上は壁だけでは隠れない。
        public static readonly Vector3 RoofCenter = new Vector3(-7f, WallHeight + 0.1f, 17.25f);
        public static readonly Vector3 RoofSize = new Vector3(4.9f, 0.2f, 13.4f);

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
                    EnemyFacing,
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
