using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ObjectiveAreaTests
    {
        [SetUp]
        public void SetUp()
        {
            MinimapObjectives.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            MinimapObjectives.ResetAll();
        }

        [Test]
        public void AreasLookForThePlayerTagByDefault()
        {
            Assert.That(ObjectiveAreaBase.DefaultPlayerTag, Is.EqualTo(GameBootstrap.PlayerTag));
        }

        [Test]
        public void ADestroyedAreaIsDroppedFromTheMinimapWhenRead()
        {
            var host = new GameObject("Area");
            host.AddComponent<BoxCollider>().isTrigger = true;
            var area = host.AddComponent<ReachAreaTrigger>();
            MinimapObjectives.Add(area);
            Assert.That(MinimapObjectives.Count, Is.EqualTo(1));

            Object.DestroyImmediate(host);

            var shown = new List<IMinimapObjective>();
            MinimapObjectives.CopyShownTo(shown);
            Assert.That(shown, Is.Empty);
            Assert.That(MinimapObjectives.Count, Is.Zero);
        }
    }
}
