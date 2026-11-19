using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MinimapObjectivesTests
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
        public void OnlyShownObjectivesAreListedInTheOrderTheyWereAdded()
        {
            var second = new Objective(new Vector3(0f, 0f, 2f), true);
            var hidden = new Objective(new Vector3(9f, 0f, 9f), false);
            var first = new Objective(new Vector3(1f, 0f, 0f), true);
            MinimapObjectives.Add(first);
            MinimapObjectives.Add(hidden);
            MinimapObjectives.Add(second);

            Assert.That(Shown(), Is.EqualTo(new IMinimapObjective[] { first, second }));
            Assert.That(MinimapObjectives.Count, Is.EqualTo(3), "a hidden objective stays registered and can show later");

            hidden.Shows = true;
            Assert.That(Shown(), Is.EqualTo(new IMinimapObjective[] { first, hidden, second }));
        }

        [Test]
        public void ARemovedObjectiveIsNotListed()
        {
            var objective = new Objective(Vector3.zero, true);
            MinimapObjectives.Add(objective);

            Assert.That(MinimapObjectives.Remove(objective), Is.True);
            Assert.That(Shown(), Is.Empty);
            Assert.That(MinimapObjectives.Remove(objective), Is.False, "removing twice is fine");
            Assert.That(MinimapObjectives.Remove(null), Is.False);
        }

        [Test]
        public void AddingTwiceListsItOnce()
        {
            var objective = new Objective(Vector3.zero, true);
            MinimapObjectives.Add(objective);
            MinimapObjectives.Add(objective);

            Assert.That(Shown(), Is.EqualTo(new IMinimapObjective[] { objective }));
            Assert.That(MinimapObjectives.Count, Is.EqualTo(1));
        }

        [Test]
        public void ADestroyedObjectiveIsDroppedWhenRead()
        {
            var host = new GameObject("Area");
            var area = host.AddComponent<FakeMinimapObjectiveArea>();
            MinimapObjectives.Add(area);
            Assert.That(MinimapObjectives.Count, Is.EqualTo(1));

            Object.DestroyImmediate(host);

            Assert.That(Shown(), Is.Empty);
            Assert.That(MinimapObjectives.Count, Is.Zero);
        }

        [Test]
        public void ResetForgetsEveryObjective()
        {
            MinimapObjectives.Add(new Objective(Vector3.zero, true));

            MinimapObjectives.ResetAll();

            Assert.That(Shown(), Is.Empty);
            Assert.That(MinimapObjectives.Count, Is.Zero);
        }

        [Test]
        public void RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => MinimapObjectives.Add(null));
            Assert.Throws<ArgumentNullException>(() => MinimapObjectives.CopyShownTo(null));
        }

        private static List<IMinimapObjective> Shown()
        {
            var shown = new List<IMinimapObjective>();
            MinimapObjectives.CopyShownTo(shown);
            return shown;
        }

        private sealed class Objective : IMinimapObjective
        {
            public Objective(Vector3 position, bool shows)
            {
                Position = position;
                Shows = shows;
            }

            public Vector3 Position { get; }

            public bool Shows { get; set; }

            public bool ShowsOnMinimap => Shows;
        }
    }
}
