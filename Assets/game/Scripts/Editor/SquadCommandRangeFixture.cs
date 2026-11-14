using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場に、分隊への指示 (<see cref="SquadCommandController"/>) を試す場所を置く。右の中ほど (開けた場所と小屋の間) に足場を敷き、
    /// その上に味方の bot (<see cref="BotName"/>、陣営 <see cref="BotSide"/>) を 1 人立たせる。
    /// 射撃場の床は GameBootstrap が実行時に作り NavMesh が無いので、殴ってくる敵 (<see cref="MeleeEnemyRangeFixture"/>) と同じく足場を Play 中に焼いてから bot を有効にする。
    /// smoke (<see cref="SquadCommandSmokeStep"/>) は <see cref="StandPoint"/> から <see cref="HoldPoint"/> を指して守らせ、
    /// <see cref="TargetPoint"/> に置いた標的ダミーを指して狙わせ、最後に付いてこさせる。
    /// 位置はどれも床の高さで、子の空の GameObject (<see cref="StandName"/> など) にも置くので、シーンを開いて場所を確かめられる。
    /// </summary>
    /// <remarks>
    /// 立つ位置は狙撃手の足場 (<see cref="SniperRangeFixture.Perches"/>) のどれからも <see cref="SniperRangeFixture.SightRange"/> より離し、手順の間に撃たれないようにする。
    /// bot は陣営を持つので、陣営の無い射撃場の敵や標的は自分からは狙わない (指示した敵だけを撃つ)。
    /// </remarks>
    public sealed class SquadCommandRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "SquadCommandYard";
        public const string BotName = "SquadBot";
        public const string BotId = "SquadBot";
        public const string NavFloorName = "NavFloor";
        public const string StandName = "Stand";
        public const string HoldName = "Hold";
        public const string TargetName = "Target";
        public const int FixtureOrder = 540;

        /// <summary>足場の x 方向の広さ (m)。</summary>
        public const float NavFloorWidth = 8f;

        /// <summary>足場の z 方向の広さ (m)。</summary>
        public const float NavFloorDepth = 5f;

        public const float NavFloorThickness = 0.1f;

        // 床の Plane (y=0) と面を重ねると、bot の足がどちらの面に乗るかが定まらないので、わずかに上げる。
        public const float NavFloorTop = 0.02f;

        /// <summary>bot の陣営。smoke はプレイヤーにも同じ陣営を持たせて味方にする。</summary>
        public static readonly Side BotSide = Side.Alpha;

        /// <summary>足場の中心 (床の高さ)。</summary>
        public static readonly Vector3 Center = new Vector3(13f, 0f, 5.5f);

        /// <summary>プレイヤーが立つ位置 (床の高さ)。足場の左手前の端。</summary>
        public static readonly Vector3 StandPoint = new Vector3(9.5f, 0f, 3.8f);

        /// <summary>bot が初めに立つ位置 (床の高さ)。</summary>
        public static readonly Vector3 BotPoint = new Vector3(12f, 0f, 6.5f);

        /// <summary>守らせる場所 (床の高さ)。立つ位置から右前へ 5.5m ほどで、bot の初めの位置と照準の線が重ならない。</summary>
        public static readonly Vector3 HoldPoint = new Vector3(15.5f, 0f, 5f);

        /// <summary>狙わせる標的ダミーを置く位置 (床の高さ)。守らせる場所から bot の攻撃の距離 (<see cref="EnemySpec.DefaultAttackRange"/>) の内。</summary>
        public static readonly Vector3 TargetPoint = new Vector3(16f, 0f, 7.5f);

        public static readonly EnemyBody BotBody = new EnemyBody(2f, 0.5f, 3.5f, Damageable.DefaultMaxHealth);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(
            Center + new Vector3(0f, BotBody.Height / 2f, 0f), new Vector3(NavFloorWidth, BotBody.Height, NavFloorDepth));

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            try
            {
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = Center;

                var navFloor = new GameObject(NavFloorName);
                navFloor.transform.SetParent(root.transform, false);
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "Slab";
                slab.transform.SetParent(navFloor.transform, false);
                slab.transform.localPosition = new Vector3(0f, NavFloorTop - (NavFloorThickness / 2f), 0f);
                slab.transform.localScale = new Vector3(NavFloorWidth, NavFloorThickness, NavFloorDepth);

                AddPoint(root.transform, StandName, StandPoint);
                AddPoint(root.transform, HoldName, HoldPoint);
                AddPoint(root.transform, TargetName, TargetPoint);

                var activated = new List<GameObject>();
                var weapon = MeleeEnemyRangeFixture.FindEnemyWeapon(scene);
                if (weapon != null)
                {
                    var bot = SceneBuildHelpers.CreateEnemy(
                        scene,
                        root.transform,
                        BotName,
                        BotId,
                        BotPoint + new Vector3(0f, NavFloorTop, 0f),
                        SceneBuildHelpers.FacingTowards(BotPoint, StandPoint),
                        new List<Vector3>(),
                        BotBody,
                        weapon);
                    bot.gameObject.AddComponent<SideMember>().Side = BotSide;
                    bot.gameObject.SetActive(false);
                    activated.Add(bot.gameObject);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(SquadCommandRangeFixture)}] the range has no weapon for the squad bot; placing the floor only");
                }

                root.AddComponent<NavMeshBakeActivator>().Configure(navFloor.transform, activated, SceneBuildHelpers.HumanoidAgentTypeId);
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        private static void AddPoint(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent, false);
            point.transform.SetPositionAndRotation(position, Quaternion.identity);
        }
    }
}
