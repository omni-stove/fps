using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// キャンペーンのミッション 1 つ分の配置 (地形・敵・遮蔽点・目標・チェックポイント) と保存先。
    /// <see cref="CampaignSceneBuilder"/> はこれを受け取ってシーンと定義を作る。
    /// 作るときに、目標を進める物がそろっているか・名前や Id が重ならないかを確かめ、だめなら <see cref="ArgumentException"/> を投げる。
    /// </summary>
    public sealed class CampaignMissionSpec
    {
        public CampaignMissionSpec(
            string id,
            string nameKey,
            string scenePath,
            string definitionPath,
            string navMeshDataPath,
            Vector3 floorCenter,
            Vector3 floorScale,
            IReadOnlyList<LevelBox> walls,
            IReadOnlyList<LevelBox> covers,
            IReadOnlyList<CoverPointPlacement> coverPoints,
            IReadOnlyList<ObjectiveDefinition> objectives,
            IReadOnlyList<CampaignElement> elements,
            IReadOnlyList<CampaignCheckpoint> checkpoints)
        {
            if (!CampaignLaunch.IsValidMissionId(id))
            {
                throw new ArgumentException($"mission id '{id}' is not a valid mission id", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException($"mission '{id}' needs a name key", nameof(nameKey));
            }

            if (definitionPath == null)
            {
                throw new ArgumentNullException(nameof(definitionPath));
            }

            // CampaignRunner は定義のアセット名をミッション Id (セーブのスロット) にするので、ずれると別のミッションとして保存される。
            if (!string.Equals(Path.GetFileNameWithoutExtension(definitionPath), id, StringComparison.Ordinal))
            {
                throw new ArgumentException($"mission '{id}' definition '{definitionPath}' must be named '{id}.asset'", nameof(definitionPath));
            }

            Id = id;
            NameKey = nameKey;
            ScenePath = scenePath ?? throw new ArgumentNullException(nameof(scenePath));
            DefinitionPath = definitionPath;
            NavMeshDataPath = navMeshDataPath ?? throw new ArgumentNullException(nameof(navMeshDataPath));
            FloorCenter = floorCenter;
            FloorScale = floorScale;
            Walls = Copy(walls, nameof(walls));
            Covers = Copy(covers, nameof(covers));
            CoverPoints = Copy(coverPoints, nameof(coverPoints));
            Objectives = Copy(objectives, nameof(objectives));
            Elements = Copy(elements, nameof(elements));
            Checkpoints = Copy(checkpoints, nameof(checkpoints));

            Validate();
        }

        /// <summary>ミッション Id。定義のアセット名・セーブのスロット・カタログの Id に使う。</summary>
        public string Id { get; }

        /// <summary>ミッション名を引く言語の表のキー。定義とカタログに入れる。</summary>
        public string NameKey { get; }

        public string ScenePath { get; }
        public string DefinitionPath { get; }

        /// <summary>焼いた NavMesh。Unity の慣習どおりシーン名のフォルダに置く。</summary>
        public string NavMeshDataPath { get; }

        /// <summary>床 (10m 四方の Plane) の中心と拡大率。</summary>
        public Vector3 FloorCenter { get; }

        public Vector3 FloorScale { get; }

        /// <summary>壁。地形として NavMesh に焼く。</summary>
        public IReadOnlyList<LevelBox> Walls { get; }

        /// <summary>遮蔽物。地形として NavMesh に焼く。</summary>
        public IReadOnlyList<LevelBox> Covers { get; }

        /// <summary>敵が使う遮蔽点。空でなければ <see cref="CampaignCoverPointsElement"/> をちょうど 1 つ置く。</summary>
        public IReadOnlyList<CoverPointPlacement> CoverPoints { get; }

        /// <summary>目標。定義順に並ぶ。</summary>
        public IReadOnlyList<ObjectiveDefinition> Objectives { get; }

        /// <summary>シーンの直下に置く物。この順に置く。</summary>
        public IReadOnlyList<CampaignElement> Elements { get; }

        /// <summary>チェックポイント。<see cref="CampaignRunner"/> の後ろに、この順に置く。</summary>
        public IReadOnlyList<CampaignCheckpoint> Checkpoints { get; }

        /// <summary>GameBootstrap がプレイヤーを出す足元。ここから全員・全地点へ歩いて行けなければならない。</summary>
        public static Vector3 PlayerStart => GameBootstrap.PlayerSpawn - (Vector3.up * (GameBootstrap.PlayerHeight / 2f));

        /// <summary>敵の全員。<see cref="Elements"/> の順に、各組の中の順で並べる。</summary>
        public IReadOnlyList<EnemyPlacement> AllEnemies()
        {
            var all = new List<EnemyPlacement>();
            foreach (var element in Elements)
            {
                if (element is CampaignEnemyGroupElement group)
                {
                    all.AddRange(group.Enemies);
                }
            }

            return all;
        }

        /// <summary>
        /// NavMesh の上で <see cref="PlayerStart"/> から行かなければならない点 (足元)。
        /// チェックポイント・目標の範囲・敵の位置と巡回地点・遮蔽点の立つ位置と覗く位置。
        /// </summary>
        public IReadOnlyList<Vector3> RequiredPoints()
        {
            var points = new List<Vector3>();
            foreach (var checkpoint in Checkpoints)
            {
                points.Add(checkpoint.Feet);
            }

            foreach (var element in Elements)
            {
                if (element is CampaignAreaElement area)
                {
                    points.Add(area.Position);
                }
            }

            foreach (var enemy in AllEnemies())
            {
                points.Add(enemy.Position);
                points.AddRange(enemy.PatrolRoute);
            }

            points.AddRange(SceneBuildHelpers.CoverStandPoints(CoverPoints));
            return points;
        }

        private void Validate()
        {
            var objectiveIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var objective in Objectives)
            {
                if (objective == null || !objectiveIds.Add(objective.Id ?? string.Empty))
                {
                    throw new ArgumentException($"mission '{Id}' has a null or duplicate objective", nameof(Objectives));
                }
            }

            var rootNames = new HashSet<string>(StringComparer.Ordinal)
            {
                CampaignSceneBuilder.LevelObjectName,
                CampaignSceneBuilder.BootstrapObjectName,
                CampaignSceneBuilder.RunnerObjectName,
                CampaignSceneBuilder.CampaignRunnerObjectName,
            };

            var driven = new HashSet<string>(StringComparer.Ordinal);
            var enemyIds = new HashSet<string>(StringComparer.Ordinal);
            var coverPointElements = 0;
            foreach (var element in Elements)
            {
                if (element == null)
                {
                    throw new ArgumentException($"mission '{Id}' has a null element", nameof(Elements));
                }

                if (!rootNames.Add(element.ObjectName))
                {
                    throw new ArgumentException($"mission '{Id}' uses the object name '{element.ObjectName}' twice", nameof(Elements));
                }

                switch (element)
                {
                    case CampaignAreaElement area:
                        RequireObjective(objectiveIds, area.ObjectiveId, area.ObjectName);
                        RequireDrivenOnce(driven, area.ObjectiveId, area.ObjectName);
                        break;

                    case CampaignEnemyGroupElement group:
                        foreach (var enemy in group.Enemies)
                        {
                            if (!enemyIds.Add(enemy.Id))
                            {
                                throw new ArgumentException($"mission '{Id}' uses the enemy id '{enemy.Id}' twice", nameof(Elements));
                            }
                        }

                        if (group.KillObjectiveId != null)
                        {
                            RequireObjective(objectiveIds, group.KillObjectiveId, group.ObjectName);
                            RequireDrivenOnce(driven, group.KillObjectiveId, group.ObjectName);
                            var required = FindObjective(group.KillObjectiveId).RequiredCount;
                            if (required > group.Enemies.Count)
                            {
                                throw new ArgumentException(
                                    $"mission '{Id}' objective '{group.KillObjectiveId}' needs {required} kills but '{group.ObjectName}' has only {group.Enemies.Count} enemies",
                                    nameof(Elements));
                            }
                        }

                        if (group.RevealObjectiveId != null)
                        {
                            RequireObjective(objectiveIds, group.RevealObjectiveId, group.ObjectName);
                        }

                        break;

                    case CampaignCoverPointsElement _:
                        coverPointElements++;
                        break;
                }
            }

            if (coverPointElements != (CoverPoints.Count > 0 ? 1 : 0))
            {
                throw new ArgumentException(
                    $"mission '{Id}' has {CoverPoints.Count} cover points and {coverPointElements} cover point elements; place exactly one when there are cover points",
                    nameof(Elements));
            }

            foreach (var objective in Objectives)
            {
                if (!driven.Contains(objective.Id))
                {
                    throw new ArgumentException($"mission '{Id}' has nothing in the scene that progresses objective '{objective.Id}'", nameof(Elements));
                }
            }

            var checkpointIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var checkpoint in Checkpoints)
            {
                if (checkpoint == null)
                {
                    throw new ArgumentException($"mission '{Id}' has a null checkpoint", nameof(Checkpoints));
                }

                if (!checkpointIds.Add(checkpoint.Id))
                {
                    throw new ArgumentException($"mission '{Id}' uses the checkpoint id '{checkpoint.Id}' twice", nameof(Checkpoints));
                }

                if (!rootNames.Add(checkpoint.ObjectName))
                {
                    throw new ArgumentException($"mission '{Id}' uses the object name '{checkpoint.ObjectName}' twice", nameof(Checkpoints));
                }
            }
        }

        private ObjectiveDefinition FindObjective(string id)
        {
            foreach (var objective in Objectives)
            {
                if (string.Equals(objective.Id, id, StringComparison.Ordinal))
                {
                    return objective;
                }
            }

            throw new KeyNotFoundException($"mission '{Id}' has no objective '{id}'");
        }

        private void RequireObjective(HashSet<string> objectiveIds, string objectiveId, string objectName)
        {
            if (!objectiveIds.Contains(objectiveId))
            {
                throw new ArgumentException($"'{objectName}' of mission '{Id}' refers to unknown objective '{objectiveId}'", nameof(Elements));
            }
        }

        private void RequireDrivenOnce(HashSet<string> driven, string objectiveId, string objectName)
        {
            // 2 つの物が同じ目標を進めると、片方だけで完了して数が合わなくなる。
            if (!driven.Add(objectiveId))
            {
                throw new ArgumentException($"'{objectName}' of mission '{Id}' progresses objective '{objectiveId}', which something else already progresses", nameof(Elements));
            }
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return new List<T>(source).AsReadOnly();
        }
    }

    /// <summary><see cref="CampaignMissionSpec.Elements"/> の 1 つ。シーンの直下に <see cref="ObjectName"/> の GameObject を置く。</summary>
    public abstract class CampaignElement
    {
        protected CampaignElement(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                throw new ArgumentException("a campaign element needs an object name", nameof(objectName));
            }

            ObjectName = objectName;
        }

        public string ObjectName { get; }
    }

    /// <summary>目標の範囲の進め方。</summary>
    public enum CampaignAreaKind
    {
        /// <summary>入ったら進める (<see cref="ReachAreaTrigger"/>)。</summary>
        Reach,

        /// <summary>中で Interact したら進める (<see cref="InteractObjective"/>)。</summary>
        Interact,
    }

    /// <summary>床の印とトリガーで目標を進める範囲。</summary>
    public sealed class CampaignAreaElement : CampaignElement
    {
        public CampaignAreaElement(string objectName, CampaignAreaKind kind, string objectiveId, Vector3 position, Vector3 padScale)
            : base(objectName)
        {
            if (!Enum.IsDefined(typeof(CampaignAreaKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown area kind");
            }

            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                throw new ArgumentException($"area '{objectName}' needs an objective id", nameof(objectiveId));
            }

            Kind = kind;
            ObjectiveId = objectiveId;
            Position = position;
            PadScale = padScale;
        }

        public CampaignAreaKind Kind { get; }
        public string ObjectiveId { get; }

        /// <summary>床の印の底面の中心 (足元)。</summary>
        public Vector3 Position { get; }

        public Vector3 PadScale { get; }

        public static CampaignAreaElement Reach(string objectName, string objectiveId, Vector3 position, Vector3 padScale)
        {
            return new CampaignAreaElement(objectName, CampaignAreaKind.Reach, objectiveId, position, padScale);
        }

        public static CampaignAreaElement Interact(string objectName, string objectiveId, Vector3 position, Vector3 padScale)
        {
            return new CampaignAreaElement(objectName, CampaignAreaKind.Interact, objectiveId, position, padScale);
        }
    }

    /// <summary>
    /// 敵の組。<see cref="CampaignElement.ObjectName"/> の GameObject の子に敵を置く。
    /// <see cref="KillObjectiveId"/> があれば、組の全員を標的にする <see cref="KillTargetObjective"/> を付ける。
    /// <see cref="RevealObjectiveId"/> があれば、敵を非アクティブで置き、その目標が Active になったら <see cref="ObjectiveReveal"/> で出す。
    /// </summary>
    public sealed class CampaignEnemyGroupElement : CampaignElement
    {
        public CampaignEnemyGroupElement(
            string objectName,
            IReadOnlyList<EnemyPlacement> enemies,
            string killObjectiveId = null,
            string revealObjectiveId = null)
            : base(objectName)
        {
            if (enemies == null)
            {
                throw new ArgumentNullException(nameof(enemies));
            }

            if (enemies.Count == 0)
            {
                throw new ArgumentException($"enemy group '{objectName}' has no enemies", nameof(enemies));
            }

            foreach (var enemy in enemies)
            {
                if (enemy == null)
                {
                    throw new ArgumentException($"enemy group '{objectName}' has a null enemy", nameof(enemies));
                }
            }

            if (killObjectiveId != null && string.IsNullOrWhiteSpace(killObjectiveId))
            {
                throw new ArgumentException($"enemy group '{objectName}' has a blank kill objective id", nameof(killObjectiveId));
            }

            if (revealObjectiveId != null && string.IsNullOrWhiteSpace(revealObjectiveId))
            {
                throw new ArgumentException($"enemy group '{objectName}' has a blank reveal objective id", nameof(revealObjectiveId));
            }

            Enemies = new List<EnemyPlacement>(enemies).AsReadOnly();
            KillObjectiveId = killObjectiveId;
            RevealObjectiveId = revealObjectiveId;
        }

        public IReadOnlyList<EnemyPlacement> Enemies { get; }

        /// <summary>組の全員の撃破で進める目標。null なら目標に関わらない。</summary>
        public string KillObjectiveId { get; }

        /// <summary>Active になったら敵を出す目標。null なら最初から置いておく。</summary>
        public string RevealObjectiveId { get; }
    }

    /// <summary><see cref="CampaignMissionSpec.CoverPoints"/> を置く場所。敵の全員がここの registry を共有する。</summary>
    public sealed class CampaignCoverPointsElement : CampaignElement
    {
        public CampaignCoverPointsElement(string objectName)
            : base(objectName)
        {
        }
    }

    /// <summary>チェックポイント 1 つ。</summary>
    public sealed class CampaignCheckpoint
    {
        public CampaignCheckpoint(string objectName, string id, Vector3 feet)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                throw new ArgumentException("a checkpoint needs an object name", nameof(objectName));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"checkpoint '{objectName}' needs an id", nameof(id));
            }

            ObjectName = objectName;
            Id = id;
            Feet = feet;
        }

        public string ObjectName { get; }

        /// <summary>セーブデータに残るので、一度出したら変えない。</summary>
        public string Id { get; }

        /// <summary>床の印の底面の中心 (足元)。</summary>
        public Vector3 Feet { get; }
    }
}
