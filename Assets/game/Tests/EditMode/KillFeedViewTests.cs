using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="KillFeedView"/> が、オフラインの Id から <see cref="Damageable"/> の名前を引いて行を足し、
    /// 5 行・5 秒で消し、自分の関わる行を強調することを確かめる。
    /// </summary>
    public sealed class KillFeedViewTests
    {
        private readonly List<Object> _created = new List<Object>();
        private Hud _hud;
        private KillFeedView _view;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            _hud = Hud.Create("KillFeedTestHud", null, null, null);
            _created.Add(_hud.gameObject);
            _created.Add(_hud.OwnedPanelSettings);
            _view = _hud.KillFeed;
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void HudCreatesTheViewWithAnEmptyFeed()
        {
            Assert.That(_view, Is.Not.Null);
            Assert.That(_view.Feed.Capacity, Is.EqualTo(KillFeed.DefaultCapacity));
            Assert.That(_view.Feed.LineSeconds, Is.EqualTo(KillFeed.DefaultLineSeconds));
            Assert.That(_view.Feed.Count, Is.Zero);
            Assert.That(_view.Names, Is.InstanceOf<OfflineKillFeedNames>());
        }

        [Test]
        public void OfflineLinesUseTheDamageableNamesAndHighlightTheLocalPlayer()
        {
            var player = CreateDamageable("Player", local: true);
            var dummy = CreateDamageable("Dummy-1", local: false);

            _view.Show(new KillFeedEntry(
                CombatEvents.OfflineId(player.gameObject), CombatEvents.OfflineId(dummy.gameObject), "w", true, false));

            var line = _view.Feed.Lines[0];
            Assert.That(line.Killer.Text, Is.EqualTo("Player"));
            Assert.That(line.Victim.Text, Is.EqualTo("Dummy-1"));
            Assert.That(line.Killer.Team, Is.Null);
            Assert.That(line.Highlighted, Is.True);
            Assert.That(line.WeaponText, Is.EqualTo(KillFeedFormat.Weapon(string.Empty, true, false)));
        }

        [Test]
        public void ALineBetweenOthersIsNotHighlighted()
        {
            var a = CreateDamageable("Enemy-A", local: false);
            var b = CreateDamageable("Dummy-B", local: false);

            _view.Show(new KillFeedEntry(CombatEvents.OfflineId(a.gameObject), CombatEvents.OfflineId(b.gameObject), "", false, false));

            Assert.That(_view.Feed.Lines[0].Highlighted, Is.False);
        }

        [Test]
        public void AnUnknownIdGetsAPlaceholderName()
        {
            _view.Show(new KillFeedEntry(9001, 9002, "", false, false));

            Assert.That(_view.Feed.Lines[0].Killer.Text, Is.EqualTo(KillFeedFormat.Unknown(9001)));
            Assert.That(_view.Feed.Lines[0].Victim.Text, Is.EqualTo(KillFeedFormat.Unknown(9002)));
        }

        [Test]
        public void KillingADamageableWhileEnabledAddsALine()
        {
            var player = CreateDamageable("Player", local: true);
            var dummy = CreateDamageable("Dummy-1", local: false);

            // Edit Mode では OnEnable が走らないので、Play 中の購読と同じものを付ける。
            CombatEvents.Killed += _view.Show;
            using (CombatEvents.Attribute(player.gameObject, "w"))
            {
                dummy.Kill();
            }

            Assert.That(_view.Feed.Count, Is.EqualTo(1));
            Assert.That(_view.Feed.Lines[0].Killer.Text, Is.EqualTo("Player"));
            Assert.That(_view.Feed.Lines[0].Victim.Text, Is.EqualTo("Dummy-1"));
        }

        [Test]
        public void TheSixthKillDropsTheOldestAndLinesExpireAfterFiveSeconds()
        {
            for (var i = 1; i <= 6; i++)
            {
                _view.Show(new KillFeedEntry(i, 100 + i, "", false, false));
            }

            Assert.That(_view.Feed.Count, Is.EqualTo(5));
            Assert.That(_view.Feed.Lines[0].Entry.KillerId, Is.EqualTo(2));
            AssertRows(5);

            _view.Advance(4.5f);
            Assert.That(_view.Feed.Count, Is.EqualTo(5));

            _view.Advance(0.5f);
            Assert.That(_view.Feed.Count, Is.Zero);
            AssertRows(0);
        }

        [Test]
        public void TheRowsShowTheMarksAndTheLocalHighlight()
        {
            _view.Names = new FixedNames();

            _view.Show(new KillFeedEntry(1, 2, "rifle", true, true));

            Assume.That(_view.Element, Is.Not.Null, "the UIDocument built no visual tree in Edit Mode");
            var row = _view.Element[0];
            Assert.That(row.ClassListContains(KillFeedView.RowClass), Is.True);
            Assert.That(row.ClassListContains(KillFeedView.LocalRowClass), Is.True);

            var labels = row.Query<Label>().ToList();
            Assert.That(labels.ConvertAll(l => l.text), Is.EqualTo(new[] { KillFeedFormat.FriendlyMark, "P1", "[Rifle] " + KillFeedFormat.HeadshotMark, "P2" }));
            Assert.That(labels[1].style.color.value, Is.EqualTo(_hud.TeamColor(Team.Alpha)));
            Assert.That(labels[2].ClassListContains(KillFeedView.HeadshotClass), Is.True);
            Assert.That(_view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ASuicideRowShowsOnlyTheVictim()
        {
            _view.Names = new FixedNames();

            _view.Show(new KillFeedEntry(2, 2, "", false, false));

            Assume.That(_view.Element, Is.Not.Null, "the UIDocument built no visual tree in Edit Mode");
            var labels = _view.Element[0].Query<Label>().ToList();
            Assert.That(labels.ConvertAll(l => l.text), Is.EqualTo(new[] { "[" + KillFeedFormat.Suicide + "]", "P2" }));
        }

        private void AssertRows(int count)
        {
            if (_view.Element == null)
            {
                return;
            }

            Assert.That(_view.Element.childCount, Is.EqualTo(count));
            Assert.That(_view.Element.style.display.value, Is.EqualTo(count > 0 ? DisplayStyle.Flex : DisplayStyle.None));
        }

        private Damageable CreateDamageable(string name, bool local)
        {
            var gameObject = new GameObject(name);
            _created.Add(gameObject);
            var damageable = gameObject.AddComponent<Damageable>();
            damageable.Initialize(name, 1);
            damageable.IsLocalPlayer = local;
            return damageable;
        }

        // オンラインと同じく、Id をそのまま名前にし、1 番を自分 (Alpha)、他を Bravo にする。
        private sealed class FixedNames : IKillFeedNames
        {
            public KillFeedLine Describe(KillFeedEntry entry)
            {
                return new KillFeedLine(entry, NameOf(entry.KillerId), NameOf(entry.VictimId), entry.WeaponId.Length > 0 ? "Rifle" : string.Empty);
            }

            private static KillFeedName NameOf(int id)
            {
                return new KillFeedName(KillFeedFormat.Player(id), id == 1 ? Team.Alpha : Team.Bravo, id == 1);
            }
        }
    }
}
