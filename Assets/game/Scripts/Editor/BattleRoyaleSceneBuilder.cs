using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// バトルロイヤルの小さなテストシーン (<see cref="BuildSceneList.BattleRoyaleScene"/>) を組む。Runner は <see cref="BattleRoyaleRunner"/>、
    /// ルールは <see cref="GameRulesAssetBuilder.BattleRoyalePath"/> (倒れたら戻らない・ダウンして分隊の味方が起こせる)。
    /// 地形と遮蔽物は土台の既定のまま、<see cref="SquadCount"/> 分隊が <see cref="SquadSize"/> 人ずつ (プレイヤーの分隊 0 は bot 1 体) で
    /// 床の四方 (手前・奥・左・右) から始め、全員武器なしで、各分隊の近くと中央に置いた拾える物 (<see cref="PickupPlacements"/>、一度きり) から拾って戦う。
    /// 床の中央を中心に <see cref="ZoneInitialRadius"/> の円から <see cref="ZonePhases"/> の予定で縮むエリア (<see cref="ShrinkingZone"/>) を置き、Runner が始める。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.BattleRoyaleSceneBuilder.Build</code>
    /// </summary>
    public sealed class BattleRoyaleSceneBuilder : ModeSceneBuilder
    {
        public const string ScenePathValue = BuildSceneList.BattleRoyaleScene;
        public const string RulesPathValue = GameRulesAssetBuilder.BattleRoyalePath;
        public const string MenuPath = "Fps/Build Battle Royale Scene";

        public const int SquadCount = 4;
        public const int SquadSize = 2;

        public const string ZoneObjectName = "Zone";
        public const string PickupsObjectName = "Pickups";

        /// <summary>武器の拾える物を、持っていたときに足す予備弾。</summary>
        public const int WeaponPickupAmmoIfOwned = 30;

        public const int AmmoPickupAmount = 30;
        public const int HealthPickupAmount = 50;

        /// <summary>エリアの最初の円の中心 (床の中央)。</summary>
        public static readonly Vector3 ZoneCenter = DefaultFloorCenter;

        /// <summary>最初の円の半径 (m)。床 (x ±20m・z -10〜50m) の四隅まで入るよう、中央から隅までの距離 (約 36m) より大きくする。</summary>
        public const float ZoneInitialRadius = 37f;

        /// <summary>
        /// エリアの予定。小さな床で数分で決着するよう短くし、外のダメージは段階ごとに強める。最後の円は半径 4m。
        /// </summary>
        public static readonly IReadOnlyList<ShrinkingZone.PhaseSettings> ZonePhases = new[]
        {
            new ShrinkingZone.PhaseSettings(30f, 20f, 22f, 2f),
            new ShrinkingZone.PhaseSettings(20f, 15f, 12f, 5f),
            new ShrinkingZone.PhaseSettings(15f, 10f, 4f, 10f),
        };

        public BattleRoyaleSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => SquadLayout();

        [MenuItem(MenuPath)]
        public static void Build()
        {
            new BattleRoyaleSceneBuilder().BuildSceneFromMenu();
        }

        /// <summary>
        /// 分隊ごとの出現点と bot の数。分隊 0 (プレイヤー) は手前 (z = 0) で +Z、分隊 1 は奥 (z = 40) で -Z、分隊 2 は左 (x = -16) で +X、
        /// 分隊 3 は右 (x = 16) で -X を向く。どの分隊も <see cref="SquadSize"/> 人で、プレイヤーの分隊は bot が 1 体少ない。
        /// </summary>
        public static IReadOnlyList<ModeSideLayout> SquadLayout()
        {
            return new[]
            {
                new ModeSideLayout(new Side(0), Spawns(Vector3.forward, PlayerFeet, new Vector3(-3f, 0f, 0f)), SquadSize - 1),
                new ModeSideLayout(new Side(1), Spawns(Vector3.back, new Vector3(0f, 0f, DefaultFarSpawnZ), new Vector3(3f, 0f, DefaultFarSpawnZ)), SquadSize),
                new ModeSideLayout(new Side(2), Spawns(Vector3.right, new Vector3(-16f, 0f, 20f), new Vector3(-16f, 0f, 23f)), SquadSize),
                new ModeSideLayout(new Side(3), Spawns(Vector3.left, new Vector3(16f, 0f, 20f), new Vector3(16f, 0f, 17f)), SquadSize),
            };
        }

        /// <summary>
        /// 拾える物の置き場所と中身。各分隊の出現点の近くに rifle と handgun を 1 つずつ、分隊の間に予備弾、中央の壁の手前と奥に回復を置く。
        /// どれも出現点から拾える範囲 (<see cref="Pickup.DefaultRadius"/>) の外で、遮蔽物と重ならない。
        /// </summary>
        public static IReadOnlyList<(string Name, Vector3 Position, PickupContents Contents)> PickupPlacements(
            WeaponDefinition rifle, WeaponDefinition handgun)
        {
            if (rifle == null)
            {
                throw new ArgumentNullException(nameof(rifle));
            }

            if (handgun == null)
            {
                throw new ArgumentNullException(nameof(handgun));
            }

            return new (string, Vector3, PickupContents)[]
            {
                ("PickupSpawn-Rifle-0", new Vector3(2f, 0f, 4f), PickupContents.ForWeapon(rifle, WeaponPickupAmmoIfOwned)),
                ("PickupSpawn-Handgun-0", new Vector3(-5f, 0f, 4f), PickupContents.ForWeapon(handgun, WeaponPickupAmmoIfOwned)),
                ("PickupSpawn-Rifle-1", new Vector3(-2f, 0f, 36f), PickupContents.ForWeapon(rifle, WeaponPickupAmmoIfOwned)),
                ("PickupSpawn-Handgun-1", new Vector3(5f, 0f, 36f), PickupContents.ForWeapon(handgun, WeaponPickupAmmoIfOwned)),
                ("PickupSpawn-Rifle-2", new Vector3(-12f, 0f, 22f), PickupContents.ForWeapon(rifle, WeaponPickupAmmoIfOwned)),
                ("PickupSpawn-Handgun-2", new Vector3(-12f, 0f, 17f), PickupContents.ForWeapon(handgun, WeaponPickupAmmoIfOwned)),
                ("PickupSpawn-Rifle-3", new Vector3(12f, 0f, 18f), PickupContents.ForWeapon(rifle, WeaponPickupAmmoIfOwned)),
                ("PickupSpawn-Handgun-3", new Vector3(12f, 0f, 23f), PickupContents.ForWeapon(handgun, WeaponPickupAmmoIfOwned)),
                ("PickupSpawn-Ammo-SouthWest", new Vector3(-6f, 0f, 9f), PickupContents.ForReserveAmmo(AmmoPickupAmount)),
                ("PickupSpawn-Ammo-SouthEast", new Vector3(6f, 0f, 9f), PickupContents.ForReserveAmmo(AmmoPickupAmount)),
                ("PickupSpawn-Ammo-NorthWest", new Vector3(-6f, 0f, 31f), PickupContents.ForReserveAmmo(AmmoPickupAmount)),
                ("PickupSpawn-Ammo-NorthEast", new Vector3(6f, 0f, 31f), PickupContents.ForReserveAmmo(AmmoPickupAmount)),
                ("PickupSpawn-Health-South", new Vector3(0f, 0f, 16f), PickupContents.ForHealth(HealthPickupAmount)),
                ("PickupSpawn-Health-North", new Vector3(0f, 0f, 24f), PickupContents.ForHealth(HealthPickupAmount)),
            };
        }

        /// <summary>bot は自分の出現点と、そこから床の中央 (エリアの中心) へ半分寄った所を往復する。中央は壁なので、そこには置かない。</summary>
        protected override IReadOnlyList<Vector3> BotPatrolRoute(IReadOnlyList<ModeSideLayout> sides, int sideIndex, int spawnIndex)
        {
            var own = sides[sideIndex].Spawns[spawnIndex].Feet;
            var toward = Vector3.Lerp(own, new Vector3(ZoneCenter.x, own.y, ZoneCenter.z), 0.5f);
            return new[] { own, toward };
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            var runner = host.AddComponent<BattleRoyaleRunner>();
            runner.SquadCount = SquadCount;
            runner.SquadSize = SquadSize;
            runner.TimeLimitSeconds = BattleRoyaleRunner.DefaultTimeLimitSeconds;
            runner.StartsUnarmed = true;
            runner.ReviveRange = BattleRoyaleRunner.DefaultReviveRange;
            runner.BotPlanIntervalSeconds = BattleRoyaleRunner.DefaultBotPlanIntervalSeconds;
            runner.BotSearchRadius = BattleRoyaleBotPlanner.DefaultSearchRadius;
            return runner;
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            var rifle = WeaponAssetBuilder.FindInCatalog(context.Catalog, PickupsObjectName, WeaponAssetBuilder.RifleId);
            var handgun = WeaponAssetBuilder.FindInCatalog(context.Catalog, PickupsObjectName, WeaponAssetBuilder.HandgunId);

            // 中身を先に作って検査し、不正なら何も置かない。
            var placements = PickupPlacements(rifle, handgun);
            SceneBuildHelpers.RequireOnNavMesh(PositionsOf(placements), NavMeshCheckRadius, "pickup");

            var root = new GameObject(PickupsObjectName);
            SceneManager.MoveGameObjectToScene(root, context.Scene);
            foreach (var placement in placements)
            {
                var pointObject = new GameObject(placement.Name);
                pointObject.transform.SetParent(root.transform, false);
                pointObject.transform.position = placement.Position;
                var point = pointObject.AddComponent<PickupSpawnPoint>();
                point.Contents = placement.Contents;
                point.CollectMode = PickupCollectMode.Touch;

                // バトルロイヤルの物は取り合いなので、拾われたらもう出さない。
                point.RespawnSeconds = 0f;
            }

            var zoneObject = new GameObject(ZoneObjectName);
            SceneManager.MoveGameObjectToScene(zoneObject, context.Scene);
            zoneObject.transform.position = ZoneCenter;
            var zone = zoneObject.AddComponent<ShrinkingZone>();
            zone.InitialRadius = ZoneInitialRadius;
            zone.SetPhases(ZonePhases);
            zone.UseRandomSeed = true;

            // Runner が参加者だけを相手にして始めるので、自分では始めない。
            zone.BeginsOnStart = false;

            ((BattleRoyaleRunner)context.Runner).Zone = zone;
            EditorSceneManager.MarkSceneDirty(context.Scene);
        }

        private static IReadOnlyList<ModeSpawnPlacement> Spawns(Vector3 facing, params Vector3[] feet)
        {
            var spawns = new ModeSpawnPlacement[feet.Length];
            for (var i = 0; i < feet.Length; i++)
            {
                spawns[i] = new ModeSpawnPlacement(feet[i], facing);
            }

            return spawns;
        }

        private static List<Vector3> PositionsOf(IReadOnlyList<(string Name, Vector3 Position, PickupContents Contents)> placements)
        {
            var positions = new List<Vector3>(placements.Count);
            foreach (var placement in placements)
            {
                positions.Add(placement.Position);
            }

            return positions;
        }
    }
}
