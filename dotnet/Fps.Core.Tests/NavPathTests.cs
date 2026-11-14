using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class NavPathTests
    {
        [Test]
        public void StartsInvalidAndEmpty()
        {
            var path = new NavPath();

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Invalid));
            Assert.That(path.Points, Is.Empty);
            Assert.That(path.Length(), Is.Zero);
        }

        [Test]
        public void PointsKeepTheirLinks()
        {
            var path = new NavPath();
            path.Add(Vector3.Zero);
            path.Add(new Vector3(3f, 0f, 0f), 7);
            path.Add(new Vector3(3f, 4f, 0f));
            path.SetStatus(NavPathStatus.Complete);

            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Complete));
            Assert.That(path.Points, Has.Count.EqualTo(3));
            Assert.That(path.Points[0].StartsLink, Is.False);
            Assert.That(path.Points[1].StartsLink, Is.True);
            Assert.That(path.Points[1].LinkId, Is.EqualTo(7));
            Assert.That(path.Length(), Is.EqualTo(7f).Within(1e-5f));
        }

        [Test]
        public void NegativeLinkIdMeansWalking()
        {
            var point = new NavPathPoint(Vector3.One, -5);

            Assert.That(point.LinkId, Is.EqualTo(NavPathPoint.NoLink));
            Assert.That(point.StartsLink, Is.False);
        }

        [Test]
        public void ClearDropsPointsAndStatus()
        {
            var path = new NavPath();
            path.Add(Vector3.Zero);
            path.SetStatus(NavPathStatus.Partial);

            path.Clear();

            Assert.That(path.Points, Is.Empty);
            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Invalid));
        }

        [Test]
        public void EmptyPathCanOnlyBeInvalid()
        {
            var path = new NavPath();

            Assert.Throws<InvalidOperationException>(() => path.SetStatus(NavPathStatus.Complete));
            Assert.Throws<ArgumentOutOfRangeException>(() => path.SetStatus((NavPathStatus)99));
            path.SetStatus(NavPathStatus.Invalid);
            Assert.That(path.Status, Is.EqualTo(NavPathStatus.Invalid));
        }
    }
}
