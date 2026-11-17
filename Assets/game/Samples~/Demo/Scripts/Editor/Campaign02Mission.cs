using System.Collections.Generic;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
    public static class Campaign02Mission
    {
        public const string MissionId = "Campaign02";
        public static string MissionNameKey => MissionDefinition.NameKeyOf(MissionId);
        public static string ScenePath => BuildSceneList.Campaign02Scene;
        public static string DefinitionPath => ShooterKitRoot.OutputPath("Missions/" + MissionId + ".asset");

        // Unity の慣習どおり、焼いた NavMesh はシーン名のフォルダに置く。
        public static string NavMeshDataPath => ShooterKitRoot.OutputPath("Scenes/" + MissionId + "/" + CampaignSceneBuilder.NavMeshDataFileName);

        public const string EnterBuildingId = "enter_building";
        public const string UseTerminalId = "use_terminal";
        public const string KillReinforcementsId = "kill_reinforcements";
        public const string ReachExtractionId = "reach_extraction";

        // チェックポイントの Id はセーブデータに残るので変えない。
        public const string StartCheckpointId = "start";

        public const string BuildingCheckpointId = "building";
        public const string TerminalCheckpointId = "terminal";

        public const string BuildingEntranceObjectName = "BuildingEntrance";
        public const string GuardsObjectName = "Guards";
        public const string TerminalObjectName = "Terminal";
        public const string ReinforcementsObjectName = "Reinforcements";
        public const string ExtractionObjectName = "Extraction";
        public const string StartCheckpointObjectName = "Checkpoint_Start";
        public const string BuildingCheckpointObjectName = "Checkpoint_Building";
        public const string TerminalCheckpointObjectName = "Checkpoint_Terminal";

        public static IReadOnlyList<CampaignPickupPlacement> Pickups()
        {
            return new[]
            {
                CampaignPickupPlacement.Weapon(
                    "Pickup-Launcher", new Vector3(-8f, 0f, 5f), WeaponAssetBuilder.LauncherId, CampaignSceneBuilder.WeaponPickupAmmoIfOwned, 0f),
                CampaignPickupPlacement.Ammo(
                    "Pickup-Ammo-Start", new Vector3(8f, 0f, 5f), CampaignSceneBuilder.AmmoPickupAmount, CampaignSceneBuilder.AmmoPickupRespawnSeconds),
                CampaignPickupPlacement.Ammo(
                    "Pickup-Ammo-Lobby", new Vector3(-14f, 0f, 55f), CampaignSceneBuilder.AmmoPickupAmount, CampaignSceneBuilder.AmmoPickupRespawnSeconds),
                CampaignPickupPlacement.AmmoCrate("AmmoCrate-Terminal", new Vector3(18.5f, 0f, 70f)),
            };
        }

        public static readonly Vector3 FloorCenter = new Vector3(0f, 0f, 40f);

        public static readonly Vector3 FloorScale = new Vector3(4f, 1f, 10f);

        public const float HalfWidth = 20f;
        public const float SouthZ = -10f;
        public const float NorthZ = 90f;

        public const float FrontWallZ = 45f;

        public const float EntranceHalfWidth = 2.5f;

        public const float PartitionZ = 58f;

        public const float PartitionDoorInnerX = 6f;
        public const float PartitionDoorOuterX = 10f;

        public const float BackWallZ = 75f;

        public const float BackDoorHalfWidth = 2f;

        public static readonly IReadOnlyList<LevelBox> Walls = new[]
        {
            new LevelBox("Wall_South", new Vector3(0f, CampaignSceneBuilder.WallHeight / 2f, SouthZ), new Vector3(HalfWidth * 2f, CampaignSceneBuilder.WallHeight, CampaignSceneBuilder.WallThickness)),
            new LevelBox("Wall_North", new Vector3(0f, CampaignSceneBuilder.WallHeight / 2f, NorthZ), new Vector3(HalfWidth * 2f, CampaignSceneBuilder.WallHeight, CampaignSceneBuilder.WallThickness)),
            new LevelBox("Wall_West", new Vector3(-HalfWidth, CampaignSceneBuilder.WallHeight / 2f, (SouthZ + NorthZ) / 2f), new Vector3(CampaignSceneBuilder.WallThickness, CampaignSceneBuilder.WallHeight, NorthZ - SouthZ)),
            new LevelBox("Wall_East", new Vector3(HalfWidth, CampaignSceneBuilder.WallHeight / 2f, (SouthZ + NorthZ) / 2f), new Vector3(CampaignSceneBuilder.WallThickness, CampaignSceneBuilder.WallHeight, NorthZ - SouthZ)),
            WallAlongX("Wall_Front_West", -HalfWidth, -EntranceHalfWidth, FrontWallZ),
            WallAlongX("Wall_Front_East", EntranceHalfWidth, HalfWidth, FrontWallZ),
            WallAlongX("Wall_Partition_West", -HalfWidth, -PartitionDoorOuterX, PartitionZ),
            WallAlongX("Wall_Partition_Center", -PartitionDoorInnerX, PartitionDoorInnerX, PartitionZ),
            WallAlongX("Wall_Partition_East", PartitionDoorOuterX, HalfWidth, PartitionZ),
            WallAlongX("Wall_Back_West", -HalfWidth, -BackDoorHalfWidth, BackWallZ),
            WallAlongX("Wall_Back_East", BackDoorHalfWidth, HalfWidth, BackWallZ),
            new LevelBox("Terminal_Console", new Vector3(-10f, 0.6f, 72.4f), new Vector3(2f, 1.2f, 0.8f)),
        };

        public static readonly IReadOnlyList<LevelBox> LowCovers = new[]
        {
            new LevelBox("Cover_Crate_1", new Vector3(-6f, 0.5f, 26f), new Vector3(2f, 1f, 1f)),
            new LevelBox("Cover_Crate_2", new Vector3(6f, 0.5f, 26f), new Vector3(2f, 1f, 1f)),
            new LevelBox("Cover_Crate_3", new Vector3(-9f, 0.5f, 38f), new Vector3(2f, 1f, 1f)),
            new LevelBox("Cover_Crate_4", new Vector3(9f, 0.5f, 38f), new Vector3(2f, 1f, 1f)),
            new LevelBox("Cover_Counter_1", new Vector3(-6f, 0.5f, 54f), new Vector3(4f, 1f, 1f)),
            new LevelBox("Cover_Counter_2", new Vector3(6f, 0.5f, 54f), new Vector3(4f, 1f, 1f)),
        };

        public static readonly IReadOnlyList<LevelBox> HighCovers = new[]
        {
            new LevelBox("Cover_Pillar_1", new Vector3(-3f, 1f, 66f), new Vector3(2f, 2f, 2f)),
            new LevelBox("Cover_Pillar_2", new Vector3(6f, 1f, 66f), new Vector3(2f, 2f, 2f)),
        };

        public static IReadOnlyList<LevelBox> Covers()
        {
            var all = new List<LevelBox>(LowCovers);
            all.AddRange(HighCovers);
            return all;
        }

        public static IReadOnlyList<CoverPointPlacement> CoverPoints()
        {
            var points = new List<CoverPointPlacement>(CampaignSceneBuilder.LowCoverPoints(LowCovers));
            points.AddRange(CampaignSceneBuilder.HighCoverPoints(HighCovers));
            return points;
        }

        // 入口を抜けると必ず踏むよう、入口の内側を幅いっぱいに覆う。
        public static readonly Vector3 BuildingEntrancePosition = new Vector3(0f, 0f, 49f);

        public static readonly Vector3 BuildingEntrancePadScale = new Vector3((HalfWidth * 2f) - 1f, 0.1f, 6f);

        public static readonly Vector3 TerminalPosition = new Vector3(-10f, 0f, 70f);

        public static readonly Vector3 TerminalPadScale = new Vector3(3f, 0.1f, 3f);

        public static readonly Vector3 ExtractionPosition = new Vector3(12f, 0f, 84f);

        public static readonly Vector3 ExtractionPadScale = new Vector3(6f, 0.1f, 5f);

        public static readonly Vector3 BuildingCheckpointPosition = new Vector3(0f, 0f, 49f);

        public static readonly Vector3 TerminalCheckpointPosition = new Vector3(-10f, 0f, 65f);

        public static readonly IReadOnlyList<EnemyPlacement> Guards = new[]
        {
            EnemyPlacement.Patrol(
                "Guard-Patrol-1", "guard-patrol-1", WeaponAssetBuilder.RifleId, new Vector3(-14f, 0f, 32f), new Vector3(14f, 0f, 32f)),
            EnemyPlacement.Ambush(
                "Guard-Ambush-1", "guard-ambush-1", WeaponAssetBuilder.HandgunId, new Vector3(-12f, 0f, 42f), new Vector3(0f, 0f, 28f)),
            EnemyPlacement.Ambush(
                "Guard-Ambush-2", "guard-ambush-2", WeaponAssetBuilder.HandgunId, new Vector3(12f, 0f, 42f), new Vector3(0f, 0f, 28f)),
            EnemyPlacement.Ambush(
                "Guard-Lobby", "guard-lobby", WeaponAssetBuilder.HandgunId, new Vector3(0f, 0f, 56f), new Vector3(0f, 0f, 46f)),
        };

        // 増援が現れる裏庭は、端末からは壁に遮られて見えない所にする。
        public static readonly IReadOnlyList<EnemyPlacement> Reinforcements = new[]
        {
            EnemyPlacement.Patrol(
                "Reinforcement-1", "reinforcement-1", WeaponAssetBuilder.RifleId, new Vector3(-14f, 0f, 86f), new Vector3(-6f, 0f, 68f)),
            EnemyPlacement.Patrol(
                "Reinforcement-2", "reinforcement-2", WeaponAssetBuilder.RifleId, new Vector3(-8f, 0f, 87f), new Vector3(2f, 0f, 63f)),
            EnemyPlacement.Patrol(
                "Reinforcement-3", "reinforcement-3", WeaponAssetBuilder.HandgunId, new Vector3(-2f, 0f, 86f), new Vector3(-12f, 0f, 62f)),
        };

        public static int ReinforcementCount => Reinforcements.Count;

        public static IReadOnlyList<ObjectiveDefinition> Objectives()
        {
            return new[]
            {
                new ObjectiveDefinition(EnterBuildingId, MissionDefinition.ObjectiveKeyOf(MissionId, EnterBuildingId)),
                new ObjectiveDefinition(UseTerminalId, MissionDefinition.ObjectiveKeyOf(MissionId, UseTerminalId), 1, false, EnterBuildingId),
                new ObjectiveDefinition(
                    KillReinforcementsId,
                    MissionDefinition.ObjectiveKeyOf(MissionId, KillReinforcementsId),
                    ReinforcementCount,
                    false,
                    UseTerminalId),
                new ObjectiveDefinition(ReachExtractionId, MissionDefinition.ObjectiveKeyOf(MissionId, ReachExtractionId), 1, false, KillReinforcementsId),
            };
        }

        public static CampaignMissionSpec Spec()
        {
            return new CampaignMissionSpec(
                MissionId,
                MissionNameKey,
                ScenePath,
                DefinitionPath,
                NavMeshDataPath,
                FloorCenter,
                FloorScale,
                Walls,
                Covers(),
                CoverPoints(),
                Objectives(),
                new CampaignElement[]
                {
                    CampaignAreaElement.Reach(BuildingEntranceObjectName, EnterBuildingId, BuildingEntrancePosition, BuildingEntrancePadScale),
                    new CampaignEnemyGroupElement(GuardsObjectName, Guards),
                    CampaignAreaElement.Interact(TerminalObjectName, UseTerminalId, TerminalPosition, TerminalPadScale),
                    new CampaignEnemyGroupElement(ReinforcementsObjectName, Reinforcements, KillReinforcementsId, KillReinforcementsId),
                    new CampaignCoverPointsElement(CampaignSceneBuilder.CoverPointsObjectName),
                    CampaignAreaElement.Reach(ExtractionObjectName, ReachExtractionId, ExtractionPosition, ExtractionPadScale),
                    new CampaignPickupsElement(CampaignSceneBuilder.PickupsObjectName, Pickups()),
                },
                new[]
                {
                    new CampaignCheckpoint(StartCheckpointObjectName, StartCheckpointId, CampaignMissionSpec.PlayerStart),
                    new CampaignCheckpoint(BuildingCheckpointObjectName, BuildingCheckpointId, BuildingCheckpointPosition),
                    new CampaignCheckpoint(TerminalCheckpointObjectName, TerminalCheckpointId, TerminalCheckpointPosition),
                });
        }

        private static LevelBox WallAlongX(string name, float fromX, float toX, float z)
        {
            return new LevelBox(
                name,
                new Vector3((fromX + toX) / 2f, CampaignSceneBuilder.WallHeight / 2f, z),
                new Vector3(toX - fromX, CampaignSceneBuilder.WallHeight, CampaignSceneBuilder.WallThickness));
        }
    }
}
