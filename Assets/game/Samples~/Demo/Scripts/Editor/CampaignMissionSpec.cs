using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
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

            Id = id;
            NameKey = nameKey;
            ScenePath = scenePath ?? throw new ArgumentNullException(nameof(scenePath));
            DefinitionPath = definitionPath ?? throw new ArgumentNullException(nameof(definitionPath));
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

        public string Id { get; }

        public string NameKey { get; }

        public string ScenePath { get; }
        public string DefinitionPath { get; }

        // Unity の慣習どおり、焼いた NavMesh はシーン名のフォルダに置く。
        public string NavMeshDataPath { get; }

        public Vector3 FloorCenter { get; }

        public Vector3 FloorScale { get; }

        public IReadOnlyList<LevelBox> Walls { get; }

        public IReadOnlyList<LevelBox> Covers { get; }

        public IReadOnlyList<CoverPointPlacement> CoverPoints { get; }

        public IReadOnlyList<ObjectiveDefinition> Objectives { get; }

        public IReadOnlyList<CampaignElement> Elements { get; }

        public IReadOnlyList<CampaignCheckpoint> Checkpoints { get; }

        public static Vector3 PlayerStart => GameBootstrap.PlayerSpawn - (Vector3.up * (GameBootstrap.PlayerHeight / 2f));

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

                if (element is CampaignPickupsElement pickups)
                {
                    foreach (var pickup in pickups.Pickups)
                    {
                        points.Add(pickup.Feet);
                    }
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

    public enum CampaignAreaKind
    {
        Reach,

        Interact,
    }

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

        public string KillObjectiveId { get; }

        public string RevealObjectiveId { get; }
    }

    public sealed class CampaignCoverPointsElement : CampaignElement
    {
        public CampaignCoverPointsElement(string objectName)
            : base(objectName)
        {
        }
    }

    public sealed class CampaignPickupsElement : CampaignElement
    {
        public CampaignPickupsElement(string objectName, IReadOnlyList<CampaignPickupPlacement> pickups)
            : base(objectName)
        {
            if (pickups == null)
            {
                throw new ArgumentNullException(nameof(pickups));
            }

            if (pickups.Count == 0)
            {
                throw new ArgumentException($"pickup group '{objectName}' has no pickups", nameof(pickups));
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pickup in pickups)
            {
                if (pickup == null || !names.Add(pickup.ObjectName))
                {
                    throw new ArgumentException($"pickup group '{objectName}' has a null or duplicate pickup", nameof(pickups));
                }
            }

            Pickups = new List<CampaignPickupPlacement>(pickups).AsReadOnly();
        }

        public IReadOnlyList<CampaignPickupPlacement> Pickups { get; }
    }

    public enum CampaignPickupKind
    {
        Weapon,
        Ammo,

        AmmoCrate,
    }

    public sealed class CampaignPickupPlacement
    {
        private CampaignPickupPlacement(CampaignPickupKind kind, string objectName, Vector3 feet, string weaponId, int amount, float respawnSeconds)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                throw new ArgumentException("a pickup needs an object name", nameof(objectName));
            }

            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, $"pickup '{objectName}' amount must not be negative");
            }

            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, $"pickup '{objectName}' respawn seconds must be non-negative and finite");
            }

            Kind = kind;
            ObjectName = objectName;
            Feet = feet;
            WeaponId = weaponId;
            Amount = amount;
            RespawnSeconds = respawnSeconds;
        }

        public CampaignPickupKind Kind { get; }

        public string ObjectName { get; }

        public Vector3 Feet { get; }

        public string WeaponId { get; }

        public int Amount { get; }

        public float RespawnSeconds { get; }

        public static CampaignPickupPlacement Weapon(string objectName, Vector3 feet, string weaponId, int ammoIfOwned, float respawnSeconds)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                throw new ArgumentException($"weapon pickup '{objectName}' needs a weapon id", nameof(weaponId));
            }

            return new CampaignPickupPlacement(CampaignPickupKind.Weapon, objectName, feet, weaponId, ammoIfOwned, respawnSeconds);
        }

        public static CampaignPickupPlacement Ammo(string objectName, Vector3 feet, int amount, float respawnSeconds)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, $"ammo pickup '{objectName}' needs a positive amount");
            }

            return new CampaignPickupPlacement(CampaignPickupKind.Ammo, objectName, feet, null, amount, respawnSeconds);
        }

        public static CampaignPickupPlacement AmmoCrate(string objectName, Vector3 feet)
        {
            return new CampaignPickupPlacement(CampaignPickupKind.AmmoCrate, objectName, feet, null, 0, 0f);
        }
    }

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

        // セーブデータに残るので、一度出したら変えない。
        public string Id { get; }

        public Vector3 Feet { get; }
    }
}
