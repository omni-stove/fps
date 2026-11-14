using System.Linq;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LadderComponent = Fps.Runtime.Traversal.Ladder;

namespace Fps.Tests.EditMode
{
    /// <summary>射撃場に置くはしごの台 (<see cref="LadderRangeFixture"/>) と、その smoke の手順 (<see cref="LadderSmokeStep"/>) が見つかり、決まりどおりに置かれること。</summary>
    public sealed class LadderRangeFixtureTests
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void TheRangeFindsTheFixtureAndTheSmokeFindsTheStep()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<LadderRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<LadderSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void ItPlacesATowerWithALadderInsideItsFootprint()
        {
            var fixture = new LadderRangeFixture();

            var placed = fixture.Place(_scene, GameRules.Default);

            Assert.That(placed.name, Is.EqualTo(fixture.Name));
            Assert.That(placed.scene, Is.EqualTo(_scene));
            Assert.That(placed.transform.parent, Is.Null);
            var ladderObject = GameObject.Find(LadderRangeFixture.LadderPath);
            Assert.That(ladderObject, Is.Not.Null);
            var ladder = ladderObject.GetComponent<LadderComponent>();
            Assert.That(ladder, Is.Not.Null);
            Assert.That(ladder.Height, Is.EqualTo(LadderRangeFixture.TowerSize.y));
            Assert.That(ladder.Width, Is.EqualTo(LadderRangeFixture.LadderWidth));
            Assert.That(ladder.TryGetShape(out var span, out _), Is.True);

            var footprint = fixture.Footprint;
            var inFront = span.FeetAt(0f, 0.5f);
            Assert.That(footprint.Contains(new Vector3(inFront.X, 0.5f, inFront.Z)), Is.True, "the climber's spot is inside the footprint");
            Assert.That(footprint.Contains(placed.transform.Find(LadderTowerBuilder.PlatformName).position), Is.True);
        }
    }
}
