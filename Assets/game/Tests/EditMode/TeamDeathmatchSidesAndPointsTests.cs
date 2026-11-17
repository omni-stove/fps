using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class TeamDeathmatchSidesAndPointsTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var runner in Object.FindObjectsByType<ModeRunnerBase>(FindObjectsInactive.Include))
            {
                runner.Stop();
            }

            foreach (var obj in _objects)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void ANewDefinitionHasTwoSidesOfEightAndOnePointPerKill()
        {
            var definition = Create<TeamDeathmatchDefinition>("Tdm");

            Assert.That(definition.SideCount, Is.EqualTo(2));
            Assert.That(definition.SideCapacity, Is.EqualTo(8));
            Assert.That(definition.PointsPerKill, Is.EqualTo(1));

            var spec = definition.ToSpec();
            Assert.That(spec.SideCount, Is.EqualTo(2));
            Assert.That(spec.SideCapacity, Is.EqualTo(8));
            Assert.That(spec.PointsPerKill, Is.EqualTo(1));
        }

        [Test]
        public void TheDefinitionPassesSidesAndPointsToTheSpecAndCopiesThemBack()
        {
            var definition = Create<TeamDeathmatchDefinition>("Tdm");
            definition.CopyFrom(new TeamDeathmatchSpec(12, 200f, 1.5f, null, 3, 5, 4));

            Assert.That(definition.SideCount, Is.EqualTo(3));
            Assert.That(definition.SideCapacity, Is.EqualTo(5));
            Assert.That(definition.PointsPerKill, Is.EqualTo(4));

            var spec = definition.ToSpec();
            Assert.That(spec.SideCount, Is.EqualTo(3));
            Assert.That(spec.SideCapacity, Is.EqualTo(5));
            Assert.That(spec.PointsPerKill, Is.EqualTo(4));
        }

        [TestCase(1, 8, 1)]
        [TestCase(2, 0, 1)]
        [TestCase(2, 8, 0)]
        public void TheDefinitionRejectsInvalidSidesOrPointsWithItsName(int sideCount, int sideCapacity, int pointsPerKill)
        {
            var definition = Create<TeamDeathmatchDefinition>("BrokenSidesTdm");
            definition.SideCount = sideCount;
            definition.SideCapacity = sideCapacity;
            definition.PointsPerKill = pointsPerKill;

            var error = Assert.Throws<InvalidOperationException>(() => definition.ToSpec());
            StringAssert.Contains("BrokenSidesTdm", error.Message);
        }

        [Test]
        public void TheSessionTakesItsSidesFromTheSpec()
        {
            var session = TeamDeathmatchRunner.CreateSession(new TeamDeathmatchSpec(10, 60f, 3f, null, 3, 4, 1));

            Assert.That(session.Sides.Count, Is.EqualTo(3));
            Assert.That(session.Layout.Capacity, Is.EqualTo(4));
        }

        [Test]
        public void TheRunnerReadsSidesAndPointsFromTheDefinitionInsteadOfTheInspector()
        {
            var runner = Runner();
            runner.SideCount = 4;
            runner.SideCapacity = 1;
            runner.PointsPerKill = 1;
            var definition = Create<TeamDeathmatchDefinition>("ThreeSideTdm");
            definition.SideCount = 3;
            definition.SideCapacity = 2;
            definition.PointsPerKill = 5;
            runner.Definition = definition;

            var spec = runner.CreateSpec();

            Assert.That(spec.SideCount, Is.EqualTo(3), "not the Inspector's " + runner.SideCount);
            Assert.That(spec.SideCapacity, Is.EqualTo(2), "not the Inspector's " + runner.SideCapacity);
            Assert.That(spec.PointsPerKill, Is.EqualTo(5), "not the Inspector's " + runner.PointsPerKill);
        }

        [Test]
        public void TheRunnerUsesTheInspectorSidesAndPointsWithoutADefinition()
        {
            var runner = Runner();
            runner.SideCount = 3;
            runner.SideCapacity = 6;
            runner.PointsPerKill = 2;

            var spec = runner.CreateSpec();

            Assert.That(spec.SideCount, Is.EqualTo(3));
            Assert.That(spec.SideCapacity, Is.EqualTo(6));
            Assert.That(spec.PointsPerKill, Is.EqualTo(2));
        }

        [Test]
        public void AKillInTheRunnersMatchScoresTheDefinitionsPointsPerKill()
        {
            var runner = Runner();
            var definition = Create<TeamDeathmatchDefinition>("FivePointTdm");
            definition.PointsPerKill = 5;
            runner.Definition = definition;
            Member("Alpha-1", 0);
            Member("Bravo-1", 1);

            Assert.That(runner.TryBegin(), Is.True);
            var session = runner.Session;
            Assert.That(session.Sides.Count, Is.EqualTo(2));

            var ids = session.GetPlayerIds();
            var alpha = session.GetSide(ids[0]).Id == 0 ? ids[0] : ids[1];
            var bravo = alpha == ids[0] ? ids[1] : ids[0];
            session.ReportKill(alpha, bravo);

            Assert.That(session.GetScore(new Side(0)), Is.EqualTo(5d));
            Assert.That(session.GetScore(new Side(1)), Is.EqualTo(0d));
        }

        [Test]
        public void TheDefinitionPassesPointsPerAssistToTheSpecAndCopiesItBack()
        {
            var definition = Create<TeamDeathmatchDefinition>("AssistTdm");
            Assert.That(definition.PointsPerAssist, Is.Zero);
            Assert.That(definition.ToSpec().PointsPerAssist, Is.Zero);

            definition.CopyFrom(new TeamDeathmatchSpec(12, 200f, 1.5f, null, 2, 8, 1, 3));

            Assert.That(definition.PointsPerAssist, Is.EqualTo(3));
            Assert.That(definition.ToSpec().PointsPerAssist, Is.EqualTo(3));

            definition.PointsPerAssist = -1;
            var error = Assert.Throws<InvalidOperationException>(() => definition.ToSpec());
            StringAssert.Contains("AssistTdm", error.Message);
        }

        [Test]
        public void TheRunnerUsesTheInspectorPointsPerAssistWithoutADefinition()
        {
            var runner = Runner();
            runner.PointsPerAssist = 2;

            Assert.That(runner.CreateSpec().PointsPerAssist, Is.EqualTo(2));
        }

        [Test]
        public void AnAllyWhoHitTheVictimEarnsAnAssistAndItsPointsInTheRunnersMatch()
        {
            var runner = Runner();
            runner.AssistSpec = new AssistSpec(windowSeconds: 10f, minDamage: 20);
            var definition = Create<TeamDeathmatchDefinition>("AssistTdm");
            definition.PointsPerAssist = 2;
            runner.Definition = definition;
            var killer = Member("Alpha-1", 0);
            var helper = Member("Alpha-2", 0);
            var victim = Member("Bravo-1", 1);

            Assert.That(runner.TryBegin(), Is.True);
            var session = runner.Session;
            var helperId = runner.IdOf(helper.GetComponent<SideMember>());
            var killerId = runner.IdOf(killer.GetComponent<SideMember>());
            var victimDamageable = victim.GetComponent<Damageable>();

            using (CombatEvents.Attribute(helper, "rifle"))
            {
                victimDamageable.TakeDamage(30);
            }

            runner.Tick(2f);
            using (CombatEvents.Attribute(killer, "rifle"))
            {
                victimDamageable.TakeDamage(Damageable.DefaultMaxHealth);
            }

            Assert.That(session.GetKills(killerId), Is.EqualTo(1));
            Assert.That(session.GetAssists(helperId), Is.EqualTo(1));
            Assert.That(session.GetAssists(killerId), Is.Zero);
            Assert.That(session.GetScore(new Side(0)), Is.EqualTo(3d));
        }

        [Test]
        public void DamageFromAnAllyOrBelowTheMinimumIsNoAssistInTheRunnersMatch()
        {
            var runner = Runner();
            runner.AssistSpec = new AssistSpec(windowSeconds: 10f, minDamage: 20);
            var killer = Member("Alpha-1", 0);
            var helper = Member("Alpha-2", 0);
            var victim = Member("Bravo-1", 1);
            var victimAlly = Member("Bravo-2", 1);

            Assert.That(runner.TryBegin(), Is.True);
            var session = runner.Session;
            var victimDamageable = victim.GetComponent<Damageable>();

            using (CombatEvents.Attribute(helper, "rifle"))
            {
                victimDamageable.TakeDamage(19);
            }

            using (CombatEvents.Attribute(victimAlly, "rifle"))
            {
                victimDamageable.TakeDamage(30);
            }

            using (CombatEvents.Attribute(killer, "rifle"))
            {
                victimDamageable.TakeDamage(Damageable.DefaultMaxHealth);
            }

            Assert.That(session.GetAssists(runner.IdOf(helper.GetComponent<SideMember>())), Is.Zero);
            Assert.That(session.GetAssists(runner.IdOf(victimAlly.GetComponent<SideMember>())), Is.Zero);
        }

        private TeamDeathmatchRunner Runner()
        {
            var go = new GameObject("Runner");
            _objects.Add(go);
            return go.AddComponent<TeamDeathmatchRunner>();
        }

        private GameObject Member(string name, int side)
        {
            var go = new GameObject(name, typeof(CapsuleCollider));
            _objects.Add(go);
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            go.AddComponent<SideMember>().Side = new Side(side);
            return go;
        }

        private T Create<T>(string name)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _objects.Add(asset);
            return asset;
        }
    }
}
