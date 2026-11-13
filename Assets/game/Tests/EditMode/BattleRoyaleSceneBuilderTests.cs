using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="BattleRoyaleSceneBuilder"/> の置き場所・ルール・分隊の配置・拾える物・エリアの予定を確かめる。シーンを組んで NavMesh を焼く処理は、
    /// 開いているシーンを置き換える (NewScene Single) ので <c>-executeMethod Fps.Editor.BattleRoyaleSceneBuilder.Build</c> で確かめる。
    /// </summary>
    public sealed class BattleRoyaleSceneBuilderTests
    {
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
        public void EverySquadIsFullAndTheLayoutIsValid()
        {
            var sides = BattleRoyaleSceneBuilder.SquadLayout();

            Assert.That(sides.Count, Is.EqualTo(BattleRoyaleSceneBuilder.SquadCount));
            Assert.That(ModeSceneBuilder.ValidateLayout(sides, new Side(0)), Is.SameAs(sides));
            foreach (var side in sides)
            {
                var players = side.BotCount + (side.Side == new Side(0) ? 1 : 0);
                Assert.That(players, Is.EqualTo(BattleRoyaleSceneBuilder.SquadSize), $"squad {side.Side.Id}");
                Assert.That(side.Side.Id, Is.LessThan(BattleRoyaleSceneBuilder.SquadCount));
            }
        }

        [Test]
        public void EveryoneStartsInsideTheZoneAndTheZoneShrinksInward()
        {
            var center = new Vector2(BattleRoyaleSceneBuilder.ZoneCenter.x, BattleRoyaleSceneBuilder.ZoneCenter.z);
            foreach (var side in BattleRoyaleSceneBuilder.SquadLayout())
            {
                foreach (var spawn in side.Spawns)
                {
                    var distance = Vector2.Distance(center, new Vector2(spawn.Feet.x, spawn.Feet.z));
                    Assert.That(distance, Is.LessThan(BattleRoyaleSceneBuilder.ZoneInitialRadius), $"spawn {spawn.Feet}");
                }
            }

            var previous = BattleRoyaleSceneBuilder.ZoneInitialRadius;
            foreach (var phase in BattleRoyaleSceneBuilder.ZonePhases)
            {
                Assert.That(phase.Radius, Is.LessThan(previous));
                Assert.That(phase.DamagePerSecond, Is.GreaterThan(0f));
                previous = phase.Radius;
            }
        }

        [Test]
        public void EachSquadHasWeaponsNearbyAndNothingSitsOnASpawn()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var handgun = NewWeapon("Handgun", WeaponAssetBuilder.HandgunSpec());
            try
            {
                var placements = BattleRoyaleSceneBuilder.PickupPlacements(rifle, handgun);
                var names = new HashSet<string>();
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

                Assert.That(weapons, Is.EqualTo(2 * BattleRoyaleSceneBuilder.SquadCount), "a rifle and a handgun for every squad");
            }
            finally
            {
                Object.DestroyImmediate(rifle);
                Object.DestroyImmediate(handgun);
            }
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
