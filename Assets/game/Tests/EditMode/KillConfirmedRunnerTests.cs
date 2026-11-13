using System;
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
    /// KillConfirmedRunner が、倒れた参加者に DropOnDeath でタグを落とさせ、拾われたタグを確定・阻止としてモードに渡し、
    /// 消えたタグと試合の終わりに残ったタグを片付け、HUD に確定数と阻止数を出すことを確かめる。
    /// Edit Mode では物理が進まないので、タグの範囲に入ったことは <see cref="Pickup.TryCollect"/> と <see cref="Pickup.HandleEnter"/> で起こす。
    /// </summary>
    public sealed class KillConfirmedRunnerTests
    {
        private const float TagLifetimeSeconds = 10f;
        private const float RespawnSeconds = 1f;

        private static readonly string[] ConfirmedHudLines = { "確定 ALPHA 1 / BRAVO 0", "阻止 ALPHA 0 / BRAVO 0", "落ちているタグ 0" };

        private readonly List<Object> _objects = new List<Object>();
        private KillConfirmedRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _runner = Track(new GameObject("Runner")).AddComponent<KillConfirmedRunner>();
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            foreach (var pickup in Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(pickup.gameObject);
            }

            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void BeginGivesEveryMemberADropOnDeathThatDropsOnlyTheTag()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-1-1", 1, new Vector3(0f, 0f, 10f));
            var configured = bot.gameObject.AddComponent<DropOnDeath>();
            configured.Drops = new[] { PickupContents.ForHealth(25) };

            Begin(player, bot);

            var added = player.GetComponent<DropOnDeath>();
            Assert.That(added, Is.Not.Null, "the runtime player gets one");
            Assert.That(added.Drops, Is.Empty, "the default ammo is not dropped");
            Assert.That(added.Damageable, Is.SameAs(player.Damageable));
            Assert.That(configured.Damageable, Is.SameAs(bot.Damageable));

            var dropped = KillAndCollectDrops(bot, player);
            Assert.That(dropped, Has.Count.EqualTo(2), "drops set in the scene are kept next to the tag");
        }

        [Test]
        public void AKillDropsATagButScoresNothing()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-1-1", 1, new Vector3(0f, 0f, 10f));
            Begin(player, bot);

            var tag = KillAndFindTag(bot, player);

            Assert.That(_runner.Session.GetKills(0), Is.EqualTo(1));
            Assert.That(_runner.Session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(_runner.KillConfirmed.Tags, Has.Count.EqualTo(1));
            Assert.That(tag.Spec.TagOwnerId, Is.EqualTo(_runner.IdOf(bot)));
            Assert.That(tag.Spec.TagOwnerSide, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(tag.LifetimeSeconds, Is.Zero, "the mode counts the lifetime, not the pickup");
            Assert.That(tag.Radius, Is.EqualTo(KillConfirmedRunner.DefaultTagRadius));
            Assert.That(Vector3.Distance(tag.transform.position, bot.Damageable.GroundPoint), Is.LessThan(1e-3f));
        }

        [Test]
        public void AnEnemyPickingUpTheTagConfirmsTheKill()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-1-1", 1, new Vector3(0f, 0f, 10f));
            var hud = BoundHudView();
            Begin(player, bot, hud);
            var tag = KillAndFindTag(bot, player);

            Assert.That(tag.TryCollect(player.Damageable, out _), Is.True);

            Assert.That(_runner.Session.GetScore(Side.Alpha), Is.EqualTo(1d));
            Assert.That(_runner.KillConfirmed.GetConfirmed(Side.Alpha), Is.EqualTo(1));
            Assert.That(_runner.KillConfirmed.Tags, Is.Empty);
            Assert.That(_runner.TagPickupCount, Is.Zero);
            Assert.That(hud.Lines, Is.EqualTo(ConfirmedHudLines));
        }

        [Test]
        public void AnAllyPickingUpTheTagDeniesIt()
        {
            var player = Member("Player", 0, Vector3.zero);
            var ally = Member("Bot-0-1", 0, new Vector3(0f, 0f, 5f));
            var bot = Member("Bot-1-1", 1, new Vector3(0f, 0f, 10f));
            var hud = BoundHudView();
            Begin(player, ally, bot, hud);
            var tag = KillAndFindTag(ally, bot);

            Assert.That(tag.TryCollect(player.Damageable, out _), Is.True);

            Assert.That(_runner.Session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(_runner.Session.GetScore(Side.Bravo), Is.Zero);
            Assert.That(_runner.KillConfirmed.GetDenied(Side.Alpha), Is.EqualTo(1));
            Assert.That(hud.Lines[1], Is.EqualTo("阻止 ALPHA 1 / BRAVO 0"));
        }

        [Test]
        public void ATagNobodyPicksUpDisappears()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-1-1", 1, new Vector3(0f, 0f, 10f));
            Begin(player, bot);
            var tag = KillAndFindTag(bot, player);
            var tagId = _runner.KillConfirmed.Tags[0].Id;

            _runner.Tick(TagLifetimeSeconds - 0.5f);
            Assert.That(_runner.PickupOf(tagId), Is.SameAs(tag));

            _runner.Tick(0.5f);
            Assert.That(tag == null, Is.True, "the pickup is destroyed");
            Assert.That(_runner.PickupOf(tagId), Is.Null);
            Assert.That(_runner.KillConfirmed.Tags, Is.Empty);
            Assert.That(_runner.Session.GetScore(Side.Alpha), Is.Zero);
        }

        [Test]
        public void AStrangerPickingUpTheTagCountsForNobody()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-1-1", 1, new Vector3(0f, 0f, 10f));
            Begin(player, bot);
            var tag = KillAndFindTag(bot, player);

            var stranger = Track(new GameObject("Stranger", typeof(CapsuleCollider))).AddComponent<Damageable>();
            stranger.Initialize("Stranger", Damageable.DefaultMaxHealth);
            Assert.That(tag.TryCollect(stranger, out _), Is.True);

            Assert.That(_runner.KillConfirmed.Tags, Is.Empty);
            Assert.That(_runner.KillConfirmed.GetConfirmed(Side.Alpha), Is.Zero);
            Assert.That(_runner.KillConfirmed.GetConfirmed(Side.Bravo), Is.Zero);
            Assert.That(_runner.KillConfirmed.GetDenied(Side.Alpha), Is.Zero);
            Assert.That(_runner.KillConfirmed.GetDenied(Side.Bravo), Is.Zero);
        }

        [Test]
        public void ARespawnedMemberIsNoLongerInsideTheTagTheyDropped()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-1-1", 1, new Vector3(0f, 0f, 10f));
            Begin(player, bot);
            var tag = KillAndFindTag(bot, player);
            tag.HandleEnter(bot.GetComponent<Collider>());
            Assert.That(tag.InsideCount, Is.EqualTo(1));

            _runner.Tick(RespawnSeconds);

            Assert.That(bot.Damageable.IsDead, Is.False);
            Assert.That(tag.InsideCount, Is.Zero);
            Assert.That(tag.IsAvailable, Is.True, "the owner did not deny their own tag from the spawn point");
        }

        [Test]
        public void TheEndOfTheMatchClearsTagsLeftOnTheField()
        {
            var player = Member("Player", 0, Vector3.zero);
            var ally = Member("Bot-0-1", 0, new Vector3(0f, 0f, 5f));
            var bot = Member("Bot-1-1", 1, new Vector3(0f, 0f, 10f));
            Begin(new KillConfirmed(new KillConfirmedSpec(1, 300f, RespawnSeconds, TagLifetimeSeconds)), player, ally, bot);
            var leftover = KillAndFindTag(ally, bot);
            var winning = KillAndFindTag(bot, player);

            Assert.That(winning.TryCollect(player.Damageable, out _), Is.True);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(leftover == null, Is.True, "nothing is left to pick up after the match");
            Assert.That(_runner.TagPickupCount, Is.Zero);
        }

        [Test]
        public void StopRestoresTheHooksItReplaced()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-1-1", 1, new Vector3(0f, 0f, 10f));
            var drop = bot.gameObject.AddComponent<DropOnDeath>();
            drop.Drops = Array.Empty<PickupContents>();
            var calls = 0;
            Action<DropContext, List<PickupContents>> previous = (context, drops) => calls++;
            drop.DecideDrops = previous;

            Begin(player, bot);
            KillAndFindTag(bot, player);
            Assert.That(calls, Is.EqualTo(1), "the earlier hook still runs");

            _runner.Stop();
            Assert.That(drop.DecideDrops, Is.SameAs(previous));
        }

        [Test]
        public void TheSpecComesFromTheInspectorValues()
        {
            _runner.ScoreLimit = 40;
            _runner.TimeLimitSeconds = 120f;
            _runner.RespawnSeconds = 4f;
            _runner.TagLifetimeSeconds = 15f;

            var spec = _runner.CreateSpec();

            Assert.That(spec.ScoreLimit, Is.EqualTo(40));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(120f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(4f));
            Assert.That(spec.TagLifetimeSeconds, Is.EqualTo(15f));

            _runner.TagLifetimeSeconds = 0f;
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreateSpec());
        }

        [Test]
        public void AFailedBeginLeavesNoModeAttached()
        {
            var stray = Member("Bot-Stray", 5, Vector3.zero);

            Assert.Throws<ArgumentException>(
                () => _runner.Begin(CreateMode(), new[] { stray }, Array.Empty<SideSpawnPoint>()),
                "side 5 is not part of the two teams");
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.KillConfirmed, Is.Null);
        }

        private void Begin(SideMember player, SideMember bot, ModeHudView hud = null)
        {
            Begin(CreateMode(), new[] { player, bot }, player, hud);
        }

        private void Begin(SideMember player, SideMember ally, SideMember bot, ModeHudView hud)
        {
            Begin(CreateMode(), new[] { player, ally, bot }, player, hud);
        }

        private void Begin(KillConfirmed mode, SideMember player, SideMember ally, SideMember bot)
        {
            Begin(mode, new[] { player, ally, bot }, player, null);
        }

        private void Begin(KillConfirmed mode, IReadOnlyList<SideMember> members, SideMember player, ModeHudView hud)
        {
            _runner.Begin(mode, members, Array.Empty<SideSpawnPoint>(), player, hud);
        }

        private static KillConfirmed CreateMode()
        {
            return new KillConfirmed(new KillConfirmedSpec(10, 300f, RespawnSeconds, TagLifetimeSeconds));
        }

        /// <summary>killer の弾で victim を倒し、落ちたタグを返す。</summary>
        private static Pickup KillAndFindTag(SideMember victim, SideMember killer)
        {
            foreach (var pickup in KillAndCollectDrops(victim, killer))
            {
                if (pickup.Spec.Kind == PickupKind.Tag)
                {
                    return pickup;
                }
            }

            Assert.Fail($"'{victim.name}' dropped no tag");
            return null;
        }

        private static IReadOnlyList<Pickup> KillAndCollectDrops(SideMember victim, SideMember killer)
        {
            var drop = victim.GetComponent<DropOnDeath>();
            IReadOnlyList<Pickup> dropped = null;
            Action<DropOnDeath, IReadOnlyList<Pickup>> record = (source, pickups) => dropped = pickups;
            drop.Dropped += record;
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }

            drop.Dropped -= record;
            Assert.That(dropped, Is.Not.Null, $"'{victim.name}' dropped nothing");
            return dropped;
        }

        private SideMember Member(string name, int side, Vector3 position)
        {
            var go = Track(new GameObject(name, typeof(CapsuleCollider)));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }

        private ModeHudView BoundHudView()
        {
            var view = Track(new GameObject("ModeHud")).AddComponent<ModeHudView>();
            view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
            return view;
        }

        private T Track<T>(T obj)
            where T : Object
        {
            _objects.Add(obj);
            return obj;
        }
    }
}
