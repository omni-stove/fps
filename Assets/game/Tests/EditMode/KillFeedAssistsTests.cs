using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class KillFeedAssistsTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly List<KillFeedEntry> _kills = new List<KillFeedEntry>();
        private GameObject _first;
        private GameObject _second;
        private Damageable _victim;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            CombatEvents.Killed += _kills.Add;
            UseAssistTuning("{ \"windowSeconds\": 10, \"minDamage\": 20 }");
            _first = Track(new GameObject("First"));
            _second = Track(new GameObject("Second"));
            _victim = Track(new GameObject("Victim")).AddComponent<Damageable>();
            _victim.Initialize("victim", 100);
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
            GameContent.Use(null);
            KillFeedAssists.Reset();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            _kills.Clear();
        }

        [Test]
        public void TheOneWhoShotButDidNotKillIsListedAsTheAssist()
        {
            Hit(_first, "rifle", 30);
            Hit(_second, "pistol", 70);

            Assert.That(_kills.Count, Is.EqualTo(1));
            Assert.That(_kills[0].KillerId, Is.EqualTo(CombatEvents.OfflineId(_second)));
            Assert.That(_kills[0].VictimId, Is.EqualTo(CombatEvents.OfflineId(_victim.gameObject)));
            Assert.That(_kills[0].WeaponId, Is.EqualTo("pistol"));
            Assert.That(_kills[0].AssisterIds, Is.EqualTo(new[] { CombatEvents.OfflineId(_first) }));
        }

        [Test]
        public void TheKillerIsNotItsOwnAssistEvenAfterEarlierHits()
        {
            Hit(_second, "pistol", 40);
            Hit(_first, "rifle", 30);
            Hit(_second, "pistol", 30);

            Assert.That(_kills.Count, Is.EqualTo(1));
            Assert.That(_kills[0].KillerId, Is.EqualTo(CombatEvents.OfflineId(_second)));
            Assert.That(_kills[0].AssisterIds, Is.EqualTo(new[] { CombatEvents.OfflineId(_first) }));
        }

        [Test]
        public void LessThanTheMinimumDamageIsNotAnAssist()
        {
            Hit(_first, "rifle", 19);
            Hit(_second, "pistol", 81);

            Assert.That(_kills.Count, Is.EqualTo(1));
            Assert.That(_kills[0].AssisterIds, Is.Empty);
        }

        [Test]
        public void HitsAddUpTowardTheMinimum()
        {
            Hit(_first, "rifle", 10);
            Hit(_first, "rifle", 10);
            Hit(_second, "pistol", 80);

            Assert.That(_kills[0].AssisterIds, Is.EqualTo(new[] { CombatEvents.OfflineId(_first) }));
        }

        [Test]
        public void TheEntryFieldOfTheGameContentSetsTheMinimum()
        {
            UseAssistTuning("{ \"minDamage\": 50 }");

            Hit(_first, "rifle", 30);
            Hit(_second, "pistol", 70);

            Assert.That(KillFeedAssists.Ledger.Spec.MinDamage, Is.EqualTo(50));
            Assert.That(_kills[0].AssisterIds, Is.Empty);
        }

        [Test]
        public void DamageWithoutAShooterIsNotRecorded()
        {
            _victim.TakeDamage(30);
            Hit(_second, "pistol", 70);

            Assert.That(_kills[0].AssisterIds, Is.Empty);
        }

        [Test]
        public void ADeathWithoutAShooterStillListsWhoWoreItDown()
        {
            Hit(_first, "rifle", 30);
            _victim.Kill();

            Assert.That(_kills.Count, Is.EqualTo(1));
            Assert.That(_kills[0].IsSuicide, Is.True);
            Assert.That(_kills[0].AssisterIds, Is.EqualTo(new[] { CombatEvents.OfflineId(_first) }));
        }

        [Test]
        public void RefillingHealthForgetsTheEarlierHits()
        {
            Hit(_first, "rifle", 30);
            _victim.ResetHealth();
            Hit(_second, "pistol", 100);

            Assert.That(_kills.Count, Is.EqualTo(1));
            Assert.That(_kills[0].AssisterIds, Is.Empty);
        }

        [Test]
        public void ADeathTakesTheRecordsSoTheNextLifeStartsClean()
        {
            Hit(_first, "rifle", 30);
            Hit(_second, "pistol", 70);
            _victim.ResetHealth();
            Hit(_second, "pistol", 100);

            Assert.That(_kills.Count, Is.EqualTo(2));
            Assert.That(_kills[1].AssisterIds, Is.Empty);
        }

        [Test]
        public void ResettingDropsEveryRecord()
        {
            Hit(_first, "rifle", 30);
            KillFeedAssists.Reset();
            Hit(_second, "pistol", 70);

            Assert.That(_kills[0].AssisterIds, Is.Empty);
        }

        [Test]
        public void ThingsThatDoNotReportKillsAreNotRecorded()
        {
            _victim.ReportsKills = false;
            Hit(_first, "rifle", 30);
            _victim.ReportsKills = true;
            Hit(_second, "pistol", 70);

            Assert.That(_kills[0].AssisterIds, Is.Empty);
        }

        [Test]
        public void TheKillIsLoggedWithItsAssists()
        {
            var first = CombatEvents.OfflineId(_first);
            var second = CombatEvents.OfflineId(_second);
            var victim = CombatEvents.OfflineId(_victim.gameObject);
            LogAssert.Expect(
                LogType.Log,
                $"[Combat] Killed killer={second} victim={victim} weapon=pistol headshot=False friendly=False assisters={first}");

            Hit(_first, "rifle", 30);
            Hit(_second, "pistol", 70);
        }

        [Test]
        public void TheLedgerIsSharedUntilTheTuningIsReadAgain()
        {
            var ledger = KillFeedAssists.Ledger;

            Assert.That(KillFeedAssists.Ledger, Is.SameAs(ledger));

            UseAssistTuning("{ \"minDamage\": 40 }");

            Assert.That(KillFeedAssists.Ledger, Is.Not.SameAs(ledger));
            Assert.That(KillFeedAssists.Ledger.Spec.MinDamage, Is.EqualTo(40));
        }

        [Test]
        public void TheBundledTuningIsReadWhenTheEntryFieldIsEmpty()
        {
            GameContent.Use(Track(ScriptableObject.CreateInstance<GameContent>()));

            var spec = KitTuning.Assist;

            Assert.That(spec.WindowSeconds, Is.EqualTo(AssistSpec.DefaultWindowSeconds));
            Assert.That(spec.MinDamage, Is.EqualTo(AssistSpec.DefaultMinDamage));
            Assert.That(spec, Is.Not.SameAs(AssistSpec.Default), "read from Resources/Tuning/assist, not the code default");
        }

        private void Hit(GameObject shooter, string weaponId, int amount)
        {
            using (CombatEvents.Attribute(shooter, weaponId))
            {
                _victim.TakeDamage(amount);
            }
        }

        private void UseAssistTuning(string json)
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "Game";
            var text = Track(new TextAsset(json));
            text.name = "GameAssist";
            content.AssistTuning = text;
            GameContent.Use(content);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
