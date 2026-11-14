using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="BattleRoyaleSceneBuilder"/> の広いマップの置き場所・ルール・分隊の配置・地形・拾える物・エリアの予定と、
    /// <see cref="BattleRoyaleSmokeRun"/> の引数と予定の縮め方を確かめる。シーンを組んで NavMesh を焼く処理は、開いているシーンを置き換える
    /// (NewScene Single) ので <c>-executeMethod Fps.Editor.BattleRoyaleSceneBuilder.Build</c> と <see cref="BattleRoyaleSmokeRun"/> で確かめる。
    /// </summary>
    public sealed class BattleRoyaleSceneBuilderTests
    {
        private const float Epsilon = 1e-3f;

        [Test]
        public void TheSceneUsesTheBattleRoyaleRules()
        {
            var builder = new BattleRoyaleSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.BattleRoyaleScene));
            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.BattleRoyaleName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.BattleRoyalePath));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/BattleRoyale/NavMesh-Floor.asset"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.BattleRoyaleScene));

            var rules = GameRulesAssetBuilder.BattleRoyaleRules();
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(rules.Actions.Downed, Is.EqualTo(Downed.Revive));
        }

        [Test]
        public void SixSquadsOfThreeAndThePlayerHasTwoBotSquadmates()
        {
            Assert.That(BattleRoyaleSceneBuilder.SquadCount, Is.EqualTo(6));
            Assert.That(BattleRoyaleSceneBuilder.SquadSize, Is.EqualTo(3));

            var sides = BattleRoyaleSceneBuilder.SquadLayout();
            Assert.That(sides.Count, Is.EqualTo(BattleRoyaleSceneBuilder.SquadCount));
            Assert.That(ModeSceneBuilder.ValidateLayout(sides, new Side(0)), Is.SameAs(sides));
            foreach (var side in sides)
            {
                var isPlayers = side.Side == new Side(0);
                Assert.That(side.BotCount, Is.EqualTo(isPlayers ? 2 : 3), $"squad {side.Side.Id}");
                Assert.That(side.BotCount + (isPlayers ? 1 : 0), Is.EqualTo(BattleRoyaleSceneBuilder.SquadSize), $"squad {side.Side.Id}");
                Assert.That(side.Spawns.Count, Is.EqualTo(BattleRoyaleSceneBuilder.SquadSize), $"squad {side.Side.Id}");
                Assert.That(side.Side.Id, Is.LessThan(BattleRoyaleSceneBuilder.SquadCount));
            }

            Assert.That(sides[0].Spawns[0].Feet, Is.EqualTo(ModeSceneBuilder.PlayerFeet));
        }

        [Test]
        public void SquadsStartFarApartAndFaceTheMiddle()
        {
            var sides = BattleRoyaleSceneBuilder.SquadLayout();
            for (var a = 0; a < sides.Count; a++)
            {
                foreach (var spawn in sides[a].Spawns)
                {
                    var toCenter = BattleRoyaleSceneBuilder.MapCenter - spawn.Feet;
                    toCenter.y = 0f;
                    Assert.That(Vector3.Dot(spawn.Facing, toCenter.normalized), Is.GreaterThan(0.99f), $"squad {a} at {spawn.Feet}");

                    foreach (var mate in sides[a].Spawns)
                    {
                        Assert.That(Vector3.Distance(spawn.Feet, mate.Feet), Is.LessThan(10f), $"squad {a} stays together");
                    }

                    for (var b = a + 1; b < sides.Count; b++)
                    {
                        foreach (var other in sides[b].Spawns)
                        {
                            Assert.That(Vector3.Distance(spawn.Feet, other.Feet), Is.GreaterThan(100f), $"squads {a} and {b}");
                        }
                    }
                }
            }
        }

        [Test]
        public void TheMapIsThreeHundredMetersSquareAndEverythingIsInsideTheWalls()
        {
            Assert.That(BattleRoyaleSceneBuilder.MapFloorScale.x * 10f, Is.EqualTo(300f));
            Assert.That(BattleRoyaleSceneBuilder.MapFloorScale.z * 10f, Is.EqualTo(300f));

            foreach (var point in WalkPoints())
            {
                Assert.That(InsideWalls(point.Position, 1f), Is.True, point.Name);
            }

            foreach (var obstacle in BattleRoyaleSceneBuilder.MapObstacles)
            {
                var half = obstacle.Size / 2f;
                Assert.That(InsideWalls(obstacle.Center - half, 0f) && InsideWalls(obstacle.Center + half, 0f), Is.True, obstacle.Name);
            }

            foreach (var hill in BattleRoyaleSceneBuilder.Hills)
            {
                Assert.That(InsideWalls(hill.Center, hill.Radius), Is.True, hill.Name);
            }
        }

        [Test]
        public void TheMapHasSeveralBuildingClustersCoverClustersAndHills()
        {
            Assert.That(BattleRoyaleSceneBuilder.Buildings.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(BattleRoyaleSceneBuilder.Hills.Count, Is.GreaterThanOrEqualTo(3));

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var obstacle in BattleRoyaleSceneBuilder.MapObstacles)
            {
                Assert.That(names.Add(obstacle.Name), Is.True, $"obstacle '{obstacle.Name}' is used twice");
            }

            for (var squad = 0; squad < BattleRoyaleSceneBuilder.SquadCount; squad++)
            {
                Assert.That(names, Has.Member($"Rocks-{squad}-Rock"));
            }

            foreach (var building in BattleRoyaleSceneBuilder.Buildings)
            {
                Assert.That(building.Doors, Is.Not.EqualTo(BuildingDoors.None), $"{building.Name} can be entered");
                Assert.That(names, Has.Member(building.Name + "-Roof"));
            }
        }

        [Test]
        public void ObstaclesDoNotOverlapEachOtherOrTheHills()
        {
            var obstacles = BattleRoyaleSceneBuilder.MapObstacles;
            for (var i = 0; i < obstacles.Count; i++)
            {
                for (var j = i + 1; j < obstacles.Count; j++)
                {
                    Assert.That(Overlaps(obstacles[i], obstacles[j]), Is.False, $"{obstacles[i].Name} and {obstacles[j].Name}");
                }

                foreach (var hill in BattleRoyaleSceneBuilder.Hills)
                {
                    Assert.That(FootprintDistance(hill.Center, obstacles[i], 0f), Is.GreaterThan(hill.Radius), $"{obstacles[i].Name} on {hill.Name}");
                }
            }

            var hills = BattleRoyaleSceneBuilder.Hills;
            for (var i = 0; i < hills.Count; i++)
            {
                for (var j = i + 1; j < hills.Count; j++)
                {
                    Assert.That(Flat(hills[i].Center - hills[j].Center).magnitude, Is.GreaterThan(hills[i].Radius + hills[j].Radius));
                }
            }
        }

        [Test]
        public void SpawnsRallyPointsAndPickupsStandClearOfObstaclesOnTheGround()
        {
            foreach (var point in WalkPoints())
            {
                var p = point.Position;
                Assert.That(p.y, Is.EqualTo(BattleRoyaleSceneBuilder.GroundHeight(p.x, p.z)).Within(Epsilon), $"{point.Name} on the ground");
                foreach (var obstacle in BattleRoyaleSceneBuilder.MapObstacles)
                {
                    if (!Blocks(obstacle))
                    {
                        continue;
                    }

                    Assert.That(FootprintDistance(p, obstacle, 0f), Is.GreaterThan(EnemyTestSceneBuilder.EnemyRadius), $"{point.Name} in {obstacle.Name}");
                }
            }

            foreach (var side in BattleRoyaleSceneBuilder.SquadLayout())
            {
                foreach (var spawn in side.Spawns)
                {
                    Assert.That(BattleRoyaleSceneBuilder.GroundHeight(spawn.Feet.x, spawn.Feet.z), Is.Zero, $"spawn {spawn.Feet} is on the flat");
                }
            }
        }

        [Test]
        public void EveryoneStartsInsideTheZoneAndTheZoneShrinksInward()
        {
            var center = Flat(BattleRoyaleSceneBuilder.ZoneCenter);
            Assert.That(BattleRoyaleSceneBuilder.ZoneCenter, Is.EqualTo(BattleRoyaleSceneBuilder.MapCenter));
            foreach (var side in BattleRoyaleSceneBuilder.SquadLayout())
            {
                foreach (var spawn in side.Spawns)
                {
                    Assert.That(Vector2.Distance(center, Flat(spawn.Feet)), Is.LessThan(BattleRoyaleSceneBuilder.ZoneInitialRadius), $"spawn {spawn.Feet}");
                }
            }

            var previous = BattleRoyaleSceneBuilder.ZoneInitialRadius;
            var previousDamage = 0f;
            Assert.That(BattleRoyaleSceneBuilder.ZonePhases.Count, Is.GreaterThanOrEqualTo(3));
            foreach (var phase in BattleRoyaleSceneBuilder.ZonePhases)
            {
                Assert.That(phase.Radius, Is.LessThan(previous));
                Assert.That(phase.DamagePerSecond, Is.GreaterThan(previousDamage));
                Assert.That(() => phase.ToPhase(), Throws.Nothing);
                previous = phase.Radius;
                previousDamage = phase.DamagePerSecond;
            }

            // 最初の待ちの間に、出現点の先の武器を拾ってから最初の円の中へ歩ける。
            var first = BattleRoyaleSceneBuilder.ZonePhases[0];
            var walk = (BattleRoyaleSceneBuilder.SquadSpawnRadius - first.Radius) / EnemyTestSceneBuilder.EnemySpeed;
            Assert.That(first.WaitSeconds + first.ShrinkSeconds, Is.GreaterThan(walk));
        }

        [Test]
        public void ThereIsPlentyOfLootInsideAndAroundTheBuildingsAndOnTheHills()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var handgun = NewWeapon("Handgun", WeaponAssetBuilder.HandgunSpec());
            try
            {
                var placements = BattleRoyaleSceneBuilder.PickupPlacements(rifle, handgun);
                var names = new HashSet<string>(StringComparer.Ordinal);
                var weapons = 0;
                foreach (var placement in placements)
                {
                    Assert.That(names.Add(placement.Name), Is.True, $"'{placement.Name}' is used twice");
                    Assert.That(() => placement.Contents.ToSpec(), Throws.Nothing);
                    if (placement.Contents.Kind == PickupKind.Weapon)
                    {
                        weapons++;
                    }

                    foreach (var side in BattleRoyaleSceneBuilder.SquadLayout())
                    {
                        foreach (var spawn in side.Spawns)
                        {
                            Assert.That(Vector3.Distance(spawn.Feet, placement.Position), Is.GreaterThan(Pickup.DefaultRadius), placement.Name);
                        }
                    }
                }

                var players = BattleRoyaleSceneBuilder.SquadCount * BattleRoyaleSceneBuilder.SquadSize;
                Assert.That(placements.Count, Is.GreaterThanOrEqualTo(2 * players));
                Assert.That(weapons, Is.GreaterThanOrEqualTo(players), "enough weapons for everyone");

                foreach (var building in BattleRoyaleSceneBuilder.Buildings)
                {
                    var inside = 0;
                    foreach (var placement in placements)
                    {
                        if (building.ContainsInside(placement.Position.x, placement.Position.z))
                        {
                            inside++;
                        }
                    }

                    Assert.That(inside, Is.GreaterThanOrEqualTo(2), building.Name);
                }

                foreach (var hill in BattleRoyaleSceneBuilder.Hills)
                {
                    var onTop = false;
                    foreach (var placement in placements)
                    {
                        onTop |= Flat(placement.Position - hill.Center).magnitude < 1f
                            && Mathf.Abs(placement.Position.y - (hill.Height - TerrainHill.Sink)) < Epsilon;
                    }

                    Assert.That(onTop, Is.True, $"{hill.Name} has loot on top");
                }

                foreach (var side in BattleRoyaleSceneBuilder.SquadLayout())
                {
                    var near = 0;
                    foreach (var placement in placements)
                    {
                        if (placement.Contents.Kind == PickupKind.Weapon && Vector3.Distance(side.Spawns[0].Feet, placement.Position) < 15f)
                        {
                            near++;
                        }
                    }

                    Assert.That(near, Is.GreaterThanOrEqualTo(2), $"squad {side.Side.Id} has weapons near its spawn");
                }
            }
            finally
            {
                Object.DestroyImmediate(rifle);
                Object.DestroyImmediate(handgun);
            }
        }

        [Test]
        public void PickupPlacementsNeedBothWeapons()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            try
            {
                Assert.Throws<ArgumentNullException>(() => BattleRoyaleSceneBuilder.PickupPlacements(null, rifle));
                Assert.Throws<ArgumentNullException>(() => BattleRoyaleSceneBuilder.PickupPlacements(rifle, null));
            }
            finally
            {
                Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void HillsAreGentleAndFollowTheProfile()
        {
            Assert.That(TerrainHill.Profile(0f), Is.EqualTo(1f).Within(Epsilon));
            Assert.That(TerrainHill.Profile(0.5f), Is.EqualTo(0.5f).Within(Epsilon));
            Assert.That(TerrainHill.Profile(1f), Is.Zero);
            Assert.That(TerrainHill.Profile(2f), Is.Zero);

            foreach (var hill in BattleRoyaleSceneBuilder.Hills)
            {
                Assert.That(hill.Slope, Is.LessThanOrEqualTo(TerrainHill.MaxSlope), hill.Name);
                Assert.That(hill.HeightAt(hill.Center.x, hill.Center.z), Is.EqualTo(hill.Height - TerrainHill.Sink).Within(Epsilon));
                Assert.That(hill.HeightAt(hill.Center.x + hill.Radius, hill.Center.z), Is.Zero);
                Assert.That(
                    BattleRoyaleSceneBuilder.GroundHeight(hill.Center.x, hill.Center.z),
                    Is.EqualTo(hill.Height - TerrainHill.Sink).Within(Epsilon));
            }

            Assert.That(BattleRoyaleSceneBuilder.GroundHeight(BattleRoyaleSceneBuilder.MapCenter.x, BattleRoyaleSceneBuilder.MapCenter.z), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainHill("Cliff", Vector3.zero, 5f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainHill("Flat", Vector3.zero, 5f, 0f));
            Assert.Throws<ArgumentException>(() => new TerrainHill(" ", Vector3.zero, 5f, 1f));
        }

        [Test]
        public void TheHillMeshFacesUpAndMatchesTheProfile()
        {
            var mesh = BattleRoyaleSceneBuilder.BuildHillMesh(4, 8);
            try
            {
                Assert.That(mesh.name, Is.EqualTo(BattleRoyaleSceneBuilder.HillMeshName));
                Assert.That(mesh.vertexCount, Is.EqualTo(1 + (4 * 8)));
                Assert.That(mesh.triangles.Length, Is.EqualTo(8 * ((4 * 2) - 1) * 3));

                var vertices = mesh.vertices;
                foreach (var vertex in vertices)
                {
                    var r = Flat(vertex).magnitude;
                    Assert.That(r, Is.LessThanOrEqualTo(1f + Epsilon));
                    Assert.That(vertex.y, Is.EqualTo(TerrainHill.Profile(r)).Within(Epsilon));
                }

                var triangles = mesh.triangles;
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var a = vertices[triangles[i]];
                    var b = vertices[triangles[i + 1]];
                    var c = vertices[triangles[i + 2]];
                    Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0f), $"triangle {i / 3} faces up");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleSceneBuilder.BuildHillMesh(0, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleSceneBuilder.BuildHillMesh(4, 2));
        }

        [Test]
        public void ABuildingLeavesADoorwayInEachDoorWall()
        {
            var building = new BattleRoyaleBuilding("Hut", new Vector3(10f, 0f, 20f), 8f, 6f, BuildingDoors.North | BuildingDoors.East);
            var byName = new Dictionary<string, ModeObstacle>(StringComparer.Ordinal);
            foreach (var obstacle in building.Obstacles())
            {
                byName.Add(obstacle.Name, obstacle);
            }

            Assert.That(byName.Keys, Is.EquivalentTo(new[]
            {
                "Hut-WallNorth-A", "Hut-WallNorth-B", "Hut-WallSouth", "Hut-WallEast-A", "Hut-WallEast-B", "Hut-WallWest", "Hut-Roof",
            }));

            var northA = byName["Hut-WallNorth-A"];
            var northB = byName["Hut-WallNorth-B"];
            var gap = (northB.Center.x - (northB.Size.x / 2f)) - (northA.Center.x + (northA.Size.x / 2f));
            Assert.That(gap, Is.EqualTo(BattleRoyaleBuilding.DoorWidth).Within(Epsilon));
            Assert.That(northA.Size.x + northB.Size.x + gap, Is.EqualTo(8f).Within(Epsilon));

            var eastA = byName["Hut-WallEast-A"];
            var eastB = byName["Hut-WallEast-B"];
            var eastGap = (eastB.Center.z - (eastB.Size.z / 2f)) - (eastA.Center.z + (eastA.Size.z / 2f));
            Assert.That(eastGap, Is.EqualTo(BattleRoyaleBuilding.DoorWidth).Within(Epsilon));

            var roof = byName["Hut-Roof"];
            Assert.That(roof.Center.y - (roof.Size.y / 2f), Is.EqualTo(BattleRoyaleBuilding.WallHeight).Within(Epsilon));
            Assert.That(building.ContainsInside(10f, 20f), Is.True);
            Assert.That(building.ContainsInside(14f, 20f), Is.False);

            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleRoyaleBuilding("Tiny", Vector3.zero, 3f, 6f, BuildingDoors.North));
            Assert.Throws<ArgumentException>(() => new BattleRoyaleBuilding("", Vector3.zero, 8f, 6f, BuildingDoors.North));
        }

        [Test]
        public void TheSmokeRunReadsTheZoneScaleArgument()
        {
            Assert.That(BattleRoyaleSmokeRun.ParseZoneScale(Array.Empty<string>()), Is.EqualTo(BattleRoyaleSmokeRun.DefaultZoneScale));
            Assert.That(
                BattleRoyaleSmokeRun.ParseZoneScale(new[] { "-batchmode", BattleRoyaleSmokeRun.ZoneScaleArgument, "0.5" }),
                Is.EqualTo(0.5f));
            Assert.That(BattleRoyaleSmokeRun.ParseZoneScale(new[] { BattleRoyaleSmokeRun.ZoneScaleArgument, "1" }), Is.EqualTo(1f));
            Assert.Throws<ArgumentNullException>(() => BattleRoyaleSmokeRun.ParseZoneScale(null));
            Assert.Throws<ArgumentException>(() => BattleRoyaleSmokeRun.ParseZoneScale(new[] { BattleRoyaleSmokeRun.ZoneScaleArgument }));
            Assert.Throws<ArgumentException>(() => BattleRoyaleSmokeRun.ParseZoneScale(new[] { BattleRoyaleSmokeRun.ZoneScaleArgument, "abc" }));
            Assert.Throws<ArgumentException>(() => BattleRoyaleSmokeRun.ParseZoneScale(new[] { BattleRoyaleSmokeRun.ZoneScaleArgument, "0" }));
            Assert.Throws<ArgumentException>(() => BattleRoyaleSmokeRun.ParseZoneScale(new[] { BattleRoyaleSmokeRun.ZoneScaleArgument, "1.5" }));
        }

        [Test]
        public void TheSmokeRunShortensOnlyTheZoneTimes()
        {
            var phases = BattleRoyaleSceneBuilder.ZonePhases;
            var scaled = BattleRoyaleSmokeRun.ScalePhases(phases, 0.25f);

            Assert.That(scaled.Count, Is.EqualTo(phases.Count));
            for (var i = 0; i < phases.Count; i++)
            {
                Assert.That(scaled[i].WaitSeconds, Is.EqualTo(phases[i].WaitSeconds * 0.25f).Within(Epsilon));
                Assert.That(scaled[i].ShrinkSeconds, Is.EqualTo(phases[i].ShrinkSeconds * 0.25f).Within(Epsilon));
                Assert.That(scaled[i].Radius, Is.EqualTo(phases[i].Radius));
                Assert.That(scaled[i].DamagePerSecond, Is.EqualTo(phases[i].DamagePerSecond));
            }

            Assert.That(
                BattleRoyaleSmokeRun.TotalSeconds(scaled),
                Is.EqualTo(BattleRoyaleSmokeRun.TotalSeconds(phases) * 0.25f).Within(Epsilon));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleRoyaleSmokeRun.ScalePhases(phases, 0f));
            Assert.Throws<ArgumentNullException>(() => BattleRoyaleSmokeRun.ScalePhases(null, 1f));

            // 既定の倍率でも、bot が出現点の先の武器へ歩き着くより最初の待ちが長い。
            var first = BattleRoyaleSmokeRun.ScalePhases(phases, BattleRoyaleSmokeRun.DefaultZoneScale)[0];
            var toLoot = (BattleRoyaleSceneBuilder.SquadSpawnRadius - BattleRoyaleSceneBuilder.SquadLootRadius) / EnemyTestSceneBuilder.EnemySpeed;
            Assert.That(first.WaitSeconds, Is.GreaterThan(toLoot));
        }

        private static IEnumerable<(string Name, Vector3 Position)> WalkPoints()
        {
            foreach (var side in BattleRoyaleSceneBuilder.SquadLayout())
            {
                for (var i = 0; i < side.Spawns.Count; i++)
                {
                    yield return ($"spawn {side.Side.Id}-{i}", side.Spawns[i].Feet);
                }

                yield return ($"rally {side.Side.Id}", BattleRoyaleSceneBuilder.RallyPoint(side.Side.Id));
            }

            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var handgun = NewWeapon("Handgun", WeaponAssetBuilder.HandgunSpec());
            try
            {
                foreach (var placement in BattleRoyaleSceneBuilder.PickupPlacements(rifle, handgun))
                {
                    yield return (placement.Name, placement.Position);
                }
            }
            finally
            {
                Object.DestroyImmediate(rifle);
                Object.DestroyImmediate(handgun);
            }
        }

        private static bool InsideWalls(Vector3 point, float margin)
        {
            var center = BattleRoyaleSceneBuilder.MapCenter;
            var halfX = (BattleRoyaleSceneBuilder.MapFloorScale.x * 5f) - ModeSceneBuilder.BoundaryWallThickness - margin;
            var halfZ = (BattleRoyaleSceneBuilder.MapFloorScale.z * 5f) - ModeSceneBuilder.BoundaryWallThickness - margin;
            return Mathf.Abs(point.x - center.x) <= halfX + Epsilon && Mathf.Abs(point.z - center.z) <= halfZ + Epsilon;
        }

        /// <summary>bot の背より低い所に底がある (歩く人を遮る) か。屋根は遮らない。</summary>
        private static bool Blocks(ModeObstacle obstacle)
        {
            return obstacle.Center.y - (obstacle.Size.y / 2f) < EnemyTestSceneBuilder.EnemyHeight;
        }

        /// <summary>水平の point から obstacle の上から見た四角までの距離。中なら 0。</summary>
        private static float FootprintDistance(Vector3 point, ModeObstacle obstacle, float margin)
        {
            var dx = Mathf.Max(0f, Mathf.Abs(point.x - obstacle.Center.x) - (obstacle.Size.x / 2f) - margin);
            var dz = Mathf.Max(0f, Mathf.Abs(point.z - obstacle.Center.z) - (obstacle.Size.z / 2f) - margin);
            return new Vector2(dx, dz).magnitude;
        }

        private static bool Overlaps(ModeObstacle a, ModeObstacle b)
        {
            return Mathf.Abs(a.Center.x - b.Center.x) < ((a.Size.x + b.Size.x) / 2f) - Epsilon
                && Mathf.Abs(a.Center.y - b.Center.y) < ((a.Size.y + b.Size.y) / 2f) - Epsilon
                && Mathf.Abs(a.Center.z - b.Center.z) < ((a.Size.z + b.Size.z) / 2f) - Epsilon;
        }

        private static Vector2 Flat(Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }

        private static WeaponDefinition NewWeapon(string name, WeaponSpec spec)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = name;
            WeaponAssetBuilder.Configure(definition, spec);
            return definition;
        }
    }
}
