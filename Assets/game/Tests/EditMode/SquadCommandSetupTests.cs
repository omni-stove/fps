using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 分隊への指示の取り付け (SquadCommandActionInstaller)、射撃場に置く物 (SquadCommandRangeFixture)、smoke の手順 (SquadCommandSmokeStep) が
    /// 差し込み口から見つかり、置く物が決まりどおりの場所に味方の bot を置くことを確かめる。
    /// </summary>
    public sealed class SquadCommandSetupTests
    {
        private GameObject _player;
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void TheInstallerIsRegisteredAndConfiguresTheController()
        {
            Assert.That(PlayerActions.Installers.OfType<SquadCommandActionInstaller>().Count(), Is.EqualTo(1));

            PlayerActions.Attach(_player, GameRules.Default);
            var controller = _player.GetComponent<SquadCommandController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsConfigured, Is.True);

            PlayerActions.Attach(_player, GameRules.Default);
            Assert.That(_player.GetComponents<SquadCommandController>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void TheFixtureAndTheSmokeStepAreFound()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<SquadCommandRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<SquadCommandSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void TheFootprintHoldsThePointsAwayFromTheSniper()
        {
            var footprint = new SquadCommandRangeFixture().Footprint;
            var flat = Rect.MinMaxRect(footprint.min.x, footprint.min.z, footprint.max.x, footprint.max.z);

            foreach (var point in new[]
            {
                SquadCommandRangeFixture.StandPoint,
                SquadCommandRangeFixture.BotPoint,
                SquadCommandRangeFixture.HoldPoint,
                SquadCommandRangeFixture.TargetPoint,
            })
            {
                Assert.That(flat.Contains(new Vector2(point.x, point.z)), Is.True, point.ToString());
            }

            var stand = new Vector2(SquadCommandRangeFixture.StandPoint.x, SquadCommandRangeFixture.StandPoint.z);
            foreach (var perch in SniperRangeFixture.Perches())
            {
                Assert.That(
                    Vector2.Distance(new Vector2(perch.x, perch.z), stand),
                    Is.GreaterThan(SniperRangeFixture.SightRange),
                    "the sniper on its perch " + perch + " cannot see where the player stands");
            }

            Assert.That(
                Vector3.Distance(SquadCommandRangeFixture.HoldPoint, SquadCommandRangeFixture.TargetPoint),
                Is.LessThan(EnemyAgent.DefaultAttackRange),
                "the bot holding its place can reach the target");
        }

        [Test]
        public void TheFixturePlacesAnAllyBotWaitingForTheNavMesh()
        {
            _scene = TestScenes.NewAdditive();
            var weaponHolder = new GameObject(TpsRangeSceneBuilder.BootstrapObjectName);
            SceneManager.MoveGameObjectToScene(weaponHolder, _scene);
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(rifle, WeaponAssetBuilder.RifleSpec());
            weaponHolder.AddComponent<GameBootstrap>().WeaponDefinitions = new[] { rifle };

            try
            {
                var placed = new SquadCommandRangeFixture().Place(_scene, GameRules.Default);

                Assert.That(placed.name, Is.EqualTo(SquadCommandRangeFixture.RootName));
                var bot = placed.transform.Find(SquadCommandRangeFixture.BotName);
                Assert.That(bot, Is.Not.Null);
                Assert.That(bot.gameObject.activeSelf, Is.False, "the bot waits for the NavMesh to be baked");
                Assert.That(bot.GetComponent<SideMember>().Side, Is.EqualTo(SquadCommandRangeFixture.BotSide));
                Assert.That(bot.GetComponent<EnemyAgent>().Id, Is.EqualTo(SquadCommandRangeFixture.BotId));
                Assert.That(placed.GetComponent<NavMeshBakeActivator>().ActivateAfterBake, Has.Member(bot.gameObject));
                Assert.That(placed.transform.Find(SquadCommandRangeFixture.HoldName).position, Is.EqualTo(SquadCommandRangeFixture.HoldPoint));
            }
            finally
            {
                Object.DestroyImmediate(rifle);
            }
        }
    }
}
