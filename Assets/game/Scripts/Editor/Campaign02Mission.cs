using System.Collections.Generic;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// キャンペーンの 2 本目のミッション Campaign02 の配置。<see cref="CampaignSceneBuilder"/> が <see cref="Spec"/> からシーンと定義を作る。
    /// 目標は定義順に、前の目標を前提にする:
    /// 建物に入る (<see cref="EnterBuildingId"/>) → 端末を操作する (<see cref="UseTerminalId"/>、<see cref="InteractObjective"/>) →
    /// 増援を倒す (<see cref="KillReinforcementsId"/>) → 脱出地点に着く (<see cref="ReachExtractionId"/>)。
    /// </summary>
    /// <remarks>
    /// 配置 (上から見て、プレイヤーは原点で +Z を向く。床は x = ±20、z = -10〜90。壁の高さはすべて 3m):
    /// <list type="bullet">
    /// <item>z &lt; 20 は開始区画。GameBootstrap が実行時に z = 10 へ標的ダミーを並べる。</item>
    /// <item>z = 20〜45 は建物の前庭。低い木箱 (高さ 1m) が 4 つあり、見張りの敵 3 体 (巡回 1・待ち伏せ 2) がいる。
    /// 見張りは撃破の目標に入れないので、倒さずに走り抜けてもよい。</item>
    /// <item>z = 45 が建物の正面の壁で、中央 (x = ±2.5) にだけ入口がある。入口の内側の z = 46〜52 を幅いっぱいに覆うのが
    /// 「建物に入る」の範囲で、その中にチェックポイント <c>building</c> がある。ロビーには低いカウンターが 2 つと見張り 1 体。</item>
    /// <item>z = 58 の仕切りの x = -10〜-6 と 6〜10 が端末室への出入口。端末室の西の奥 (-10, 0, 70) に端末があり、
    /// その手前 (-10, 0, 65) にチェックポイント <c>terminal</c> がある。端末室には高い柱が 2 本ある。</item>
    /// <item>z = 75 が建物の裏の壁で、中央 (x = ±2) に裏口がある。その奥の裏庭に、端末を操作すると増援 3 体が現れ、
    /// 裏口から端末室へ攻めてくる。増援は端末からは壁に遮られて現れる所が見えない。</item>
    /// <item>脱出地点は裏庭の東 (12, 0, 84)。</item>
    /// </list>
    /// </remarks>
    public static class Campaign02Mission
    {
        public const string MissionId = "Campaign02";
        public const string MissionDisplayName = "Campaign 02";
        public static string ScenePath => BuildSceneList.Campaign02Scene;
        public static string DefinitionPath => FpsKitRoot.OutputPath("Missions/" + MissionId + ".asset");

        /// <summary>焼いた NavMesh。Unity の慣習どおりシーン名のフォルダに置く。</summary>
        public static string NavMeshDataPath => FpsKitRoot.OutputPath("Scenes/" + MissionId + "/" + CampaignSceneBuilder.NavMeshDataFileName);

        public const string EnterBuildingId = "enter_building";
        public const string UseTerminalId = "use_terminal";
        public const string KillReinforcementsId = "kill_reinforcements";
        public const string ReachExtractionId = "reach_extraction";

        /// <summary>チェックポイントの Id。セーブデータに残るので変えない。</summary>
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

        // ---- 地形 ----

        /// <summary>床の中心。Plane は 10m 四方なので、<see cref="FloorScale"/> で x = ±20、z = -10〜90 になる。</summary>
        public static readonly Vector3 FloorCenter = new Vector3(0f, 0f, 40f);

        public static readonly Vector3 FloorScale = new Vector3(4f, 1f, 10f);

        public const float HalfWidth = 20f;
        public const float SouthZ = -10f;
        public const float NorthZ = 90f;

        /// <summary>建物の正面の壁の z と、入口の半幅。</summary>
        public const float FrontWallZ = 45f;

        public const float EntranceHalfWidth = 2.5f;

        /// <summary>ロビーと端末室を分ける仕切りの z と、2 つの出入口の x の範囲 (内側の端・外側の端)。</summary>
        public const float PartitionZ = 58f;

        public const float PartitionDoorInnerX = 6f;
        public const float PartitionDoorOuterX = 10f;

        /// <summary>建物の裏の壁の z と、裏口の半幅。</summary>
        public const float BackWallZ = 75f;

        public const float BackDoorHalfWidth = 2f;

        /// <summary>壁と据え付けの端末。高さは端末のほかはすべて <see cref="CampaignSceneBuilder.WallHeight"/>。</summary>
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

        /// <summary>しゃがんで隠れる低い遮蔽物 (高さ 1m)。前庭の木箱とロビーのカウンター。</summary>
        public static readonly IReadOnlyList<LevelBox> LowCovers = new[]
        {
            new LevelBox("Cover_Crate_1", new Vector3(-6f, 0.5f, 26f), new Vector3(2f, 1f, 1f)),
            new LevelBox("Cover_Crate_2", new Vector3(6f, 0.5f, 26f), new Vector3(2f, 1f, 1f)),
            new LevelBox("Cover_Crate_3", new Vector3(-9f, 0.5f, 38f), new Vector3(2f, 1f, 1f)),
            new LevelBox("Cover_Crate_4", new Vector3(9f, 0.5f, 38f), new Vector3(2f, 1f, 1f)),
            new LevelBox("Cover_Counter_1", new Vector3(-6f, 0.5f, 54f), new Vector3(4f, 1f, 1f)),
            new LevelBox("Cover_Counter_2", new Vector3(6f, 0.5f, 54f), new Vector3(4f, 1f, 1f)),
        };

        /// <summary>立ったまま隠れる高い遮蔽物 (高さ 2m)。端末室の柱。</summary>
        public static readonly IReadOnlyList<LevelBox> HighCovers = new[]
        {
            new LevelBox("Cover_Pillar_1", new Vector3(-3f, 1f, 66f), new Vector3(2f, 2f, 2f)),
            new LevelBox("Cover_Pillar_2", new Vector3(6f, 1f, 66f), new Vector3(2f, 2f, 2f)),
        };

        /// <summary>低い遮蔽物、高い遮蔽物の順に並べた全部。</summary>
        public static IReadOnlyList<LevelBox> Covers()
        {
            var all = new List<LevelBox>(LowCovers);
            all.AddRange(HighCovers);
            return all;
        }

        /// <summary>遮蔽物の横の遮蔽点。低い遮蔽物の点、高い遮蔽物の点の順。</summary>
        public static IReadOnlyList<CoverPointPlacement> CoverPoints()
        {
            var points = new List<CoverPointPlacement>(CampaignSceneBuilder.LowCoverPoints(LowCovers));
            points.AddRange(CampaignSceneBuilder.HighCoverPoints(HighCovers));
            return points;
        }

        // ---- 目標の範囲 ----

        /// <summary>入口の内側を幅いっぱいに覆う範囲。入口を抜けると必ず踏む。</summary>
        public static readonly Vector3 BuildingEntrancePosition = new Vector3(0f, 0f, 49f);

        public static readonly Vector3 BuildingEntrancePadScale = new Vector3((HalfWidth * 2f) - 1f, 0.1f, 6f);

        /// <summary>端末の前の、Interact を受け付ける範囲。</summary>
        public static readonly Vector3 TerminalPosition = new Vector3(-10f, 0f, 70f);

        public static readonly Vector3 TerminalPadScale = new Vector3(3f, 0.1f, 3f);

        public static readonly Vector3 ExtractionPosition = new Vector3(12f, 0f, 84f);

        public static readonly Vector3 ExtractionPadScale = new Vector3(6f, 0.1f, 5f);

        // ---- チェックポイント ----

        public static readonly Vector3 BuildingCheckpointPosition = new Vector3(0f, 0f, 49f);

        public static readonly Vector3 TerminalCheckpointPosition = new Vector3(-10f, 0f, 65f);

        // ---- 敵 ----

        /// <summary>前庭とロビーの見張り。撃破の目標には入れない。</summary>
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

        /// <summary>端末を操作すると裏庭に現れ、裏口から端末室へ向かって往復する増援。</summary>
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

        /// <summary>Campaign02 の目標。定義順に、前の目標を前提にする。</summary>
        public static IReadOnlyList<ObjectiveDefinition> Objectives()
        {
            return new[]
            {
                new ObjectiveDefinition(EnterBuildingId, "敵を倒しながら建物に入る"),
                new ObjectiveDefinition(UseTerminalId, "端末を操作する", 1, false, EnterBuildingId),
                new ObjectiveDefinition(KillReinforcementsId, $"増援を {ReinforcementCount} 体倒す", ReinforcementCount, false, UseTerminalId),
                new ObjectiveDefinition(ReachExtractionId, "脱出地点に着く", 1, false, KillReinforcementsId),
            };
        }

        /// <summary>Campaign02 の配置。</summary>
        public static CampaignMissionSpec Spec()
        {
            return new CampaignMissionSpec(
                MissionId,
                MissionDisplayName,
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
                },
                new[]
                {
                    new CampaignCheckpoint(StartCheckpointObjectName, StartCheckpointId, CampaignMissionSpec.PlayerStart),
                    new CampaignCheckpoint(BuildingCheckpointObjectName, BuildingCheckpointId, BuildingCheckpointPosition),
                    new CampaignCheckpoint(TerminalCheckpointObjectName, TerminalCheckpointId, TerminalCheckpointPosition),
                });
        }

        /// <summary>z の位置で x = fromX〜toX に伸びる壁。</summary>
        private static LevelBox WallAlongX(string name, float fromX, float toX, float z)
        {
            return new LevelBox(
                name,
                new Vector3((fromX + toX) / 2f, CampaignSceneBuilder.WallHeight / 2f, z),
                new Vector3(toX - fromX, CampaignSceneBuilder.WallHeight, CampaignSceneBuilder.WallThickness));
        }
    }
}
