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
    /// <summary>印の取り付け (PingActionInstaller)、射撃場に置く物 (PingRangeFixture)、smoke の手順 (PingSmokeStep) が差し込み口から見つかることを確かめる。</summary>
    public sealed class PingSetupTests
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
        public void TheInstallerIsRegisteredAndFollowsTheRule()
        {
            Assert.That(PlayerActions.Installers.OfType<PingActionInstaller>().Count(), Is.EqualTo(1));

            PlayerActions.Attach(_player, GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Fps.Core.Ping.On)));
            var pinger = _player.GetComponent<PingController>();
            Assert.That(pinger, Is.Not.Null);
            Assert.That(pinger.UsesPing, Is.True);

            PlayerActions.Attach(_player, GameRules.Default);
            Assert.That(_player.GetComponents<PingController>(), Has.Length.EqualTo(1));
            Assert.That(pinger.Rule, Is.EqualTo(Fps.Core.Ping.Off));
            Assert.That(pinger.UsesPing, Is.False);
        }

        [Test]
        public void TheFixtureAndTheSmokeStepAreFound()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<PingRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<PingSmokeStep>().Count(), Is.EqualTo(1));
            Assert.That(new PingSmokeStep().TimeoutSeconds, Is.GreaterThan(PingSpec.DefaultLifetimeSeconds));
        }

        [Test]
        public void TheFootprintHoldsThePointsAndTheBlind()
        {
            var fixture = new PingRangeFixture();
            var footprint = fixture.Footprint;

            foreach (var point in new[] { PingRangeFixture.StandPoint, PingRangeFixture.ShowPoint, PingRangeFixture.HiddenPoint })
            {
                Assert.That(Flat(footprint).Contains(new Vector2(point.x, point.z)), Is.True, point.ToString());
            }

            var blind = new Bounds(PingRangeFixture.Blind.Center, PingRangeFixture.Blind.Size);
            Assert.That(Flat(footprint).Contains(new Vector2(blind.min.x, blind.min.z)), Is.True);
            Assert.That(Flat(footprint).Contains(new Vector2(blind.max.x, blind.max.z)), Is.True);
        }

        [Test]
        public void TheBlindHidesTheHiddenPointButNotTheShowPoint()
        {
            _scene = TestScenes.NewAdditive();
            var placed = new PingRangeFixture().Place(_scene, GameRules.Default);
            Physics.SyncTransforms();

            Assert.That(placed.name, Is.EqualTo(PingRangeFixture.FixtureName));
            Assert.That(placed.transform.Find(PingRangeFixture.BlindName).GetComponent<Collider>(), Is.Not.Null);
            Assert.That(placed.transform.Find(PingRangeFixture.StandName).position, Is.EqualTo(PingRangeFixture.StandPoint));

            // 肩越しのカメラの高さ (立った目の高さに肩越しの持ち上げを足したもの) から、ダミーの胴の高さを見る。
            var eye = PingRangeFixture.StandPoint + new Vector3(ViewRig.DefaultThirdPersonOffset.x, GameBootstrap.PlayerHeight, 0f);
            var body = new Vector3(0f, TargetDummy.BodyLocalPosition.y, 0f);
            Assert.That(Blocked(eye, PingRangeFixture.ShowPoint + body), Is.False, "the show point is in plain sight");
            Assert.That(Blocked(eye, PingRangeFixture.HiddenPoint + body), Is.True, "the hidden point is behind the blind");
            Assert.That(Blocked(eye, PingRangeFixture.HiddenPoint + (Vector3.up * TargetDummy.HeadLocalPosition.y)), Is.True, "the head is hidden too");
        }

        private static bool Blocked(Vector3 from, Vector3 to)
        {
            return Physics.Linecast(from, to, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private static Rect Flat(Bounds bounds)
        {
            return Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
        }
    }
}
