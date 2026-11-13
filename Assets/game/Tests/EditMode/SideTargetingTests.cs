using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>SideMember と EnemySenses.SelectHostile が、自分と違う陣営で生きているいちばん近い相手を選ぶことを確かめる。</summary>
    public sealed class SideTargetingTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                Object.DestroyImmediate(go);
            }

            _objects.Clear();
        }

        [Test]
        public void SelectsTheNearestLivingMemberOfAnotherSide()
        {
            var self = Member("Self", 1, Vector3.zero);
            var ally = Member("Ally", 1, new Vector3(1f, 0f, 0f));
            var far = Member("Far", 0, new Vector3(0f, 0f, 30f));
            var near = Member("Near", 2, new Vector3(0f, 0f, 6f));

            Assert.That(EnemySenses.SelectHostile(self, new[] { self, ally, far, near }), Is.SameAs(near));

            near.Damageable.Kill();
            Assert.That(EnemySenses.SelectHostile(self, new[] { self, ally, far, near }), Is.SameAs(far));

            far.Damageable.Kill();
            Assert.That(EnemySenses.SelectHostile(self, new[] { self, ally, far, near }), Is.Null);
        }

        [Test]
        public void SkipsMembersWithoutHealthAndNulls()
        {
            var self = Member("Self", 1, Vector3.zero);
            var go = new GameObject("NoHealth");
            _objects.Add(go);
            var noHealth = go.AddComponent<SideMember>();
            noHealth.Side = new Side(0);
            var target = Member("Target", 0, new Vector3(0f, 0f, 9f));

            Assert.That(EnemySenses.SelectHostile(self, new[] { noHealth, null, target }), Is.SameAs(target));
        }

        [Test]
        public void SideMemberKnowsAlliesAndItsOwner()
        {
            var a = Member("A", 3, Vector3.zero);
            var b = Member("B", 3, Vector3.zero);
            var c = Member("C", 4, Vector3.zero);
            var child = new GameObject("Child");
            child.transform.SetParent(a.transform, false);

            Assert.That(a.IsAllyOf(b), Is.True);
            Assert.That(a.IsAllyOf(c), Is.False);
            Assert.That(a.IsAllyOf(null), Is.False);
            Assert.That(SideMember.Of(child), Is.SameAs(a));
            Assert.That(SideMember.Of(null), Is.Null);
        }

        [Test]
        public void RejectsNullArguments()
        {
            var self = Member("Self", 1, Vector3.zero);

            Assert.Throws<ArgumentNullException>(() => EnemySenses.SelectHostile(null, Array.Empty<SideMember>()));
            Assert.Throws<ArgumentNullException>(() => EnemySenses.SelectHostile(self, null));
        }

        private SideMember Member(string name, int side, Vector3 position)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }
    }
}
