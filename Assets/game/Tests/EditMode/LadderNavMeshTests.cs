using System.Text.RegularExpressions;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LadderComponent = Fps.Runtime.Traversal.Ladder;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// はしご (<see cref="LadderComponent"/>) が張る NavMesh のリンクで、敵と Bot が地面から台の上へ (と、その逆へ) 経路を引けること。
    /// 台と床だけを焼いた NavMesh では、台の上は地面とつながっていない。
    /// </summary>
    public sealed class LadderNavMeshTests
    {
        private const float SampleRadius = 1f;

        // 台は z = 2〜6、はしごは台の -Z 側の面 (z = 2) に立ち、つかまる人は +Z を向く。
        private static readonly Vector3 _towerBase = new Vector3(0f, 0f, 4f);
        private static readonly Vector3 _towerSize = new Vector3(4f, 3f, 4f);
        private static readonly Vector3 _ground = new Vector3(0f, 0f, -6f);
        private static readonly Vector3 _top = new Vector3(0f, 3f, 4.5f);

        private Scene _scene;
        private LadderComponent _ladder;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
            var floor = SceneBuildHelpers.CreateStaticPlane(_scene, null, "Floor", Vector3.zero, new Vector3(3f, 1f, 3f));
            var surface = SceneBuildHelpers.AddNavMeshSurface(floor, CollectObjects.All);
            var tower = LadderTowerBuilder.Create(_scene, "LadderTower", _towerBase, _towerSize, Vector3.back, 0.8f);
            _ladder = tower.GetComponentInChildren<LadderComponent>();
            SceneBuildHelpers.BakeNavMesh(surface);
        }

        [TearDown]
        public void TearDown()
        {
            if (_ladder != null)
            {
                _ladder.Unregister();
            }

            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void TheTowerBuilderStandsTheLadderAgainstThePlatform()
        {
            Assert.That(_ladder, Is.Not.Null);
            Assert.That(_ladder.Height, Is.EqualTo(_towerSize.y));
            Assert.That(_ladder.transform.position, Is.EqualTo(new Vector3(0f, 0f, 2f)));
            Assert.That(Vector3.Dot(_ladder.transform.forward, Vector3.forward), Is.EqualTo(1f).Within(1e-5f), "the climber faces the platform");
            Assert.That(_ladder.GetComponentsInChildren<Collider>(), Is.Empty, "the ladder's look does not block the climber");

            var (bottom, facing) = LadderTowerBuilder.LadderPlacement(_towerBase, _towerSize, Vector3.back);
            Assert.That(bottom, Is.EqualTo(_ladder.transform.position));
            Assert.That(facing, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void WithoutTheLadderThePlatformCannotBeReached()
        {
            Assert.That(PathStatus(_ground, _top), Is.Not.EqualTo(NavMeshPathStatus.PathComplete));
        }

        [Test]
        public void TheLadderLinksTheGroundToThePlatformBothWays()
        {
            var links = _ladder.Link();

            Assert.That(links, Is.EqualTo(NavMesh.GetSettingsCount()), "one link for each agent type");
            Assert.That(_ladder.IsLinked, Is.True);
            Assert.That(PathStatus(_ground, _top), Is.EqualTo(NavMeshPathStatus.PathComplete), "up the ladder");
            Assert.That(PathStatus(_top, _ground), Is.EqualTo(NavMeshPathStatus.PathComplete), "down the ladder");

            _ladder.Unlink();
            Assert.That(_ladder.IsLinked, Is.False);
            Assert.That(PathStatus(_ground, _top), Is.Not.EqualTo(NavMeshPathStatus.PathComplete), "unlinked again");
        }

        [Test]
        public void AnAgentOnTheLinkCanFindTheLadderByItsEnds()
        {
            _ladder.Register();
            var span = _ladder.Span;
            var clearance = NavMesh.GetSettingsByIndex(0).agentRadius + LadderComponent.LinkMargin;
            var bottom = span.LinkBottom(clearance).ToUnity();
            var top = span.LinkTop(clearance).ToUnity();

            Assert.That(LadderComponent.FindLinked(bottom, top, EnemyAgent.LadderLinkTolerance), Is.SameAs(_ladder));
            Assert.That(LadderComponent.FindLinked(top, bottom, EnemyAgent.LadderLinkTolerance), Is.SameAs(_ladder), "either way round");
            Assert.That(LadderComponent.FindLinked(bottom + (Vector3.right * 5f), top, EnemyAgent.LadderLinkTolerance), Is.Null);

            _ladder.Unregister();
            Assert.That(LadderComponent.FindLinked(bottom, top, EnemyAgent.LadderLinkTolerance), Is.Null, "an unregistered ladder has no links");
        }

        [Test]
        public void TogglingTheLinksOfARegisteredLadderLinksAndUnlinks()
        {
            _ladder.Register();
            Assert.That(_ladder.IsLinked, Is.True);

            _ladder.CreatesNavMeshLinks = false;
            Assert.That(_ladder.IsLinked, Is.False);

            _ladder.CreatesNavMeshLinks = true;
            Assert.That(_ladder.IsLinked, Is.True);
        }

        [Test]
        public void AnInvalidLadderNeitherLinksNorThrows()
        {
            _ladder.Width = 0f;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Ladder\] 'Ladder' の設定が不正"));
            Assert.That(_ladder.Link(), Is.Zero);
            Assert.That(_ladder.TryGetShape(out _, out _), Is.False);
        }

        private static NavMeshPathStatus PathStatus(Vector3 from, Vector3 to)
        {
            Assert.That(NavMesh.SamplePosition(from, out var start, SampleRadius, NavMesh.AllAreas), Is.True, $"{from} is on the NavMesh");
            Assert.That(NavMesh.SamplePosition(to, out var end, SampleRadius, NavMesh.AllAreas), Is.True, $"{to} is on the NavMesh");
            var path = new NavMeshPath();
            NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path);
            return path.status;
        }
    }
}
