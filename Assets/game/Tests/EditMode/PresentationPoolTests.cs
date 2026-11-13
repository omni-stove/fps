using System;
using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    /// <summary><see cref="PresentationPool{T}"/> の上限・使い回し・返す時刻を、ただのオブジェクトで確かめる。</summary>
    public sealed class PresentationPoolTests
    {
        private sealed class Part
        {
            public bool Visible;
        }

        private int _created;
        private List<Part> _hidden;

        [SetUp]
        public void SetUp()
        {
            _created = 0;
            _hidden = new List<Part>();
        }

        [Test]
        public void CreatesUpToTheCapacityAndThenTakesTheOldest()
        {
            var pool = Pool(3);

            var first = pool.Rent(10f);
            var second = pool.Rent(10f);
            var third = pool.Rent(10f);
            var fourth = pool.Rent(10f);

            Assert.That(_created, Is.EqualTo(3));
            Assert.That(pool.Count, Is.EqualTo(3));
            Assert.That(pool.ActiveCount, Is.EqualTo(3));
            Assert.That(fourth, Is.SameAs(first), "the oldest lease is taken over");
            Assert.That(_hidden, Is.EqualTo(new[] { first }), "the taken part is hidden before it is handed out again");
            Assert.That(fourth.Visible, Is.True);

            var fifth = pool.Rent(10f);
            Assert.That(fifth, Is.SameAs(second), "the next oldest is taken next");
            Assert.That(pool.Items, Is.EquivalentTo(new[] { first, second, third }));
        }

        [Test]
        public void ManyRentsNeverCreateMoreThanTheCapacity()
        {
            var pool = Pool(4);

            for (var i = 0; i < 1000; i++)
            {
                pool.Rent(i);
                pool.ReleaseDue(i - 2);
            }

            Assert.That(_created, Is.EqualTo(4));
            Assert.That(pool.Count, Is.EqualTo(4));
        }

        [Test]
        public void ReleasedPartsAreReusedWithoutCreatingNewOnes()
        {
            var pool = Pool(5);
            var first = pool.Rent(1f);
            var second = pool.Rent(2f);

            Assert.That(pool.ReleaseDue(1.5f), Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(first.Visible, Is.False);
            Assert.That(second.Visible, Is.True);

            var reused = pool.Rent(3f);

            Assert.That(reused, Is.SameAs(first));
            Assert.That(_created, Is.EqualTo(2));
        }

        [Test]
        public void ReleaseDueReturnsEveryLeaseWhoseTimeHasCome()
        {
            var pool = Pool(5);
            pool.Rent(1f);
            pool.Rent(2f);
            pool.Rent(3f);

            Assert.That(pool.ReleaseDue(0.5f), Is.Zero);
            Assert.That(pool.ReleaseDue(2f), Is.EqualTo(2), "a lease due exactly now is returned");
            Assert.That(pool.ReleaseDue(float.PositiveInfinity), Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.Zero);
        }

        [Test]
        public void ExtendMovesTheReleaseTime()
        {
            var pool = Pool(2);
            var part = pool.Rent(1f);

            Assert.That(pool.Extend(part, 5f), Is.True);
            Assert.That(pool.ReleaseDue(2f), Is.Zero);
            Assert.That(pool.ReleaseDue(5f), Is.EqualTo(1));
            Assert.That(pool.Extend(part, 9f), Is.False, "a returned part cannot be extended");
        }

        [Test]
        public void ReleaseIgnoresPartsThatAreNotLent()
        {
            var pool = Pool(2);
            var part = pool.Rent(1f);

            Assert.That(pool.Release(new Part()), Is.False);
            Assert.That(pool.Release(part), Is.True);
            Assert.That(pool.Release(part), Is.False, "releasing twice does nothing");
            Assert.That(_hidden, Has.Count.EqualTo(1));
        }

        [Test]
        public void CopyActiveListsLeasesOldestFirst()
        {
            var pool = Pool(3);
            var a = pool.Rent(1f);
            var b = pool.Rent(1f);
            var into = new List<Part> { new Part() };

            pool.CopyActive(into);

            Assert.That(into, Is.EqualTo(new[] { a, b }));
        }

        [Test]
        public void ClearHidesAndHandsEveryPartToDestroy()
        {
            var pool = Pool(3);
            var lent = pool.Rent(1f);
            var idle = pool.Rent(1f);
            pool.Release(idle);
            var destroyed = new List<Part>();

            pool.Clear(destroyed.Add);

            Assert.That(destroyed, Is.EquivalentTo(new[] { lent, idle }));
            Assert.That(lent.Visible, Is.False);
            Assert.That(pool.Count, Is.Zero);
            Assert.That(pool.ActiveCount, Is.Zero);

            pool.Rent(1f);
            Assert.That(_created, Is.EqualTo(3), "a cleared pool creates new parts");
        }

        [Test]
        public void RejectsABadCapacityOrFactory()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PresentationPool<Part>(0, () => new Part()));
            Assert.Throws<ArgumentNullException>(() => new PresentationPool<Part>(1, null));
            Assert.Throws<InvalidOperationException>(() => new PresentationPool<Part>(1, () => null).Rent(1f));
        }

        private PresentationPool<Part> Pool(int capacity)
        {
            return new PresentationPool<Part>(
                capacity,
                () =>
                {
                    _created++;
                    return new Part();
                },
                part => part.Visible = true,
                part =>
                {
                    part.Visible = false;
                    _hidden.Add(part);
                });
        }
    }
}
