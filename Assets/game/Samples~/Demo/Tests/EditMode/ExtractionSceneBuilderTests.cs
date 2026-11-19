using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // シーンを組んで NavMesh を焼く処理は開いているシーンを置き換える (NewScene Single) ので、
    // -executeMethod Shooter.Editor.ExtractionSceneBuilder.Build で確かめる。
    public sealed class ExtractionSceneBuilderTests
    {
        [Test]
        public void TheSceneIsListedWithTheOtherModesUnderItsOwnRules()
        {
            var builder = new ExtractionSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(DemoSample.Root + "/Scenes/Extraction.unity"));
            Assert.That(builder.RulesPath, Is.EqualTo(DemoSample.Root + "/Rules/Extraction.asset"));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/Extraction/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[ExtractionSceneBuilder]"));
            Assert.That(ExtractionSceneBuilder.DefinitionPath, Is.EqualTo(DemoSample.Root + "/Modes/Extraction.asset"));
            Assert.That(ExtractionSceneBuilder.LootTuningPath, Is.EqualTo(DemoSample.Root + "/Modes/ExtractionLoot.json"));
            Assert.That(ExtractionSceneBuilder.ModeName, Is.EqualTo(GameRulesAssetBuilder.ExtractionName));
            Assert.That(GameRulesAssetBuilder.ModeNames, Has.Member(ExtractionSceneBuilder.ModeName));
            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(ExtractionSceneBuilder.ModeName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.ModeRulesPath(ExtractionSceneBuilder.ModeName)));
            Assert.That(BuildSceneList.Scenes, Has.Member(builder.ScenePath));
        }

        [Test]
        public void TheRaidEndsWhenThePlayerFallsAndTheAmmoComesFromTheCrates()
        {
            var rules = GameRulesAssetBuilder.ExtractionRules();

            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.None), "a raid ends when the player falls");
            Assert.That(rules.SpawnProtectionSeconds, Is.Zero);
            Assert.That(rules.Actions.Downed, Is.EqualTo(Downed.Off), "nobody is there to revive the player");
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(1f));
            Assert.That(rules.InfiniteReserve, Is.False);
        }

        [Test]
        public void ThePlayerRaidsAloneAgainstTheHostileSide()
        {
            var sides = ModeSceneBuilder.TwoSides(0, ExtractionSceneBuilder.HostileBots);

            Assert.That(ModeSceneBuilder.ValidateLayout(sides, new Side(0)), Is.SameAs(sides));
            Assert.That(sides[1].Side, Is.EqualTo(ExtractionSceneBuilder.HostileSide));
            Assert.That(ExtractionSceneBuilder.HostileSide, Is.Not.EqualTo(new Side(0)));
        }

        [Test]
        public void TheDemoSpecCanBeWon()
        {
            var spec = ExtractionSceneBuilder.DemoSpec();

            Assert.That(spec.RaidSeconds, Is.EqualTo(ExtractionSceneBuilder.RaidSeconds));
            Assert.That(spec.ExtractHoldSeconds, Is.EqualTo(ExtractionSceneBuilder.ExtractHoldSeconds));
            Assert.That(spec.OpenPoints, Is.EqualTo(ExtractionSceneBuilder.OpenPoints));
            Assert.That(spec.OpenAfterSeconds, Is.EqualTo(ExtractionSceneBuilder.OpenAfterSeconds));
            Assert.That(spec.OpenPoints, Is.LessThan(ExtractionSceneBuilder.Exits.Count), "some exits stay closed so the open ones have to be found");
        }

        [Test]
        public void TheLootJsonNamesEveryTableTheCratesUseAndOnlyKitItems()
        {
            var loot = LootJson.Parse(ExtractionSceneBuilder.LootJsonText(), ExtractionSceneBuilder.LootTuningPath);
            var items = InventoryJson.Parse(Resources.Load<TextAsset>(KitTuning.InventoryResourcePath).text, KitTuning.InventoryResourcePath);

            Assert.That(loot.Tables.Select(t => t.Id), Is.EqualTo(new[] { ExtractionSceneBuilder.SuppliesTable, ExtractionSceneBuilder.ValuablesTable }));
            foreach (var placement in ExtractionSceneBuilder.Containers)
            {
                Assert.That(loot.Find(placement.TableId), Is.Not.Null, placement.Name);
            }

            foreach (var table in loot.Tables)
            {
                foreach (var entry in table.Entries)
                {
                    Assert.That(items.TryFind(entry.ItemId, out _), Is.True, table.Id + " " + entry.ItemId);
                }
            }
        }

        [Test]
        public void CratesAndExitsStayInsideTheWallsAndAwayFromTheSpawns()
        {
            var halfX = (ModeSceneBuilder.DefaultFloorScale.x * 5f) - ModeSceneBuilder.BoundaryWallThickness;
            var minZ = ModeSceneBuilder.DefaultFloorCenter.z - (ModeSceneBuilder.DefaultFloorScale.z * 5f) + ModeSceneBuilder.BoundaryWallThickness;
            var maxZ = ModeSceneBuilder.DefaultFloorCenter.z + (ModeSceneBuilder.DefaultFloorScale.z * 5f) - ModeSceneBuilder.BoundaryWallThickness;
            var spawns = ModeSceneBuilder.TwoSides(0, ExtractionSceneBuilder.HostileBots).SelectMany(side => side.Spawns).Select(s => s.Feet).ToList();

            foreach (var exit in ExtractionSceneBuilder.Exits)
            {
                Assert.That(Mathf.Abs(exit.Position.x) + ExtractionZone.DefaultRadius, Is.LessThan(halfX), exit.PointId);
                Assert.That(exit.Position.z - ExtractionZone.DefaultRadius, Is.GreaterThan(minZ), exit.PointId);
                Assert.That(exit.Position.z + ExtractionZone.DefaultRadius, Is.LessThan(maxZ), exit.PointId);
                Assert.That(spawns.All(s => Flat(s - exit.Position) > ExtractionZone.DefaultRadius), Is.True, exit.PointId + " covers a spawn point");
            }

            foreach (var crate in ExtractionSceneBuilder.Containers)
            {
                Assert.That(Mathf.Abs(crate.Position.x), Is.LessThan(halfX), crate.Name);
                Assert.That(crate.Position.z, Is.InRange(minZ, maxZ), crate.Name);
                Assert.That(spawns.All(s => Flat(s - crate.Position) > LootContainer.DefaultReach), Is.True, crate.Name + " is on a spawn point");
                foreach (var obstacle in ModeSceneBuilder.DefaultObstacles)
                {
                    var bounds = new Bounds(obstacle.Center, obstacle.Size);
                    bounds.Expand(LootContainer.DefaultVisualSize.x);
                    Assert.That(bounds.Contains(new Vector3(crate.Position.x, obstacle.Center.y, crate.Position.z)), Is.False, crate.Name + " is inside " + obstacle.Name);
                }
            }

            Assert.That(ExtractionSceneBuilder.Exits.Select(e => e.PointId), Is.Unique);
            Assert.That(ExtractionSceneBuilder.Containers.Select(c => c.Name), Is.Unique);
        }

        private static float Flat(Vector3 offset)
        {
            offset.y = 0f;
            return offset.magnitude;
        }
    }
}
