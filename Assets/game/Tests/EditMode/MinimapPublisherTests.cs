using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MinimapPublisherTests
    {
        private const float _tolerance = 1e-3f;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
            GameSignals.ResetAll();
            MinimapReveals.ResetAll();
            MinimapReveals.Clock = () => _now;
            MinimapObjectives.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
            GameSignals.ResetAll();
            MinimapReveals.ResetAll();
            MinimapObjectives.ResetAll();
        }

        [Test]
        public void ListsLivingAlliesAroundThePlayer()
        {
            var player = Player(new Vector3(1f, 0f, 2f), yaw: 30f, side: 0);
            Body("Ally", new Vector3(3f, 0f, 4f), yaw: 90f, side: 0);
            Body("Enemy", new Vector3(-3f, 0f, 4f), yaw: 0f, side: 1);
            Body("DeadAlly", new Vector3(5f, 0f, 5f), yaw: 0f, side: 0).Kill();
            var publisher = Publisher(player);

            var status = publisher.Refresh();

            Assert.That(status.HasValue, Is.True);
            Assert.That(status.Value.SelfPosition, Is.EqualTo(new System.Numerics.Vector3(1f, 0f, 2f)));
            Assert.That(status.Value.SelfHeadingDegrees, Is.EqualTo(30f).Within(_tolerance));
            var ally = status.Value.Contacts.Single();
            Assert.That(ally.Kind, Is.EqualTo(MinimapContactKind.Ally));
            Assert.That(ally.Position, Is.EqualTo(new System.Numerics.Vector3(3f, 0f, 4f)));
            Assert.That(ally.HeadingDegrees, Is.EqualTo(90f).Within(_tolerance));
            Assert.That(ally.Team, Is.EqualTo(Team.Alpha));
            Assert.That(GameSignals.Minimap.Value, Is.EqualTo(status), "an unrevealed enemy and a dead ally are not listed");
        }

        [Test]
        public void APlayerWithoutASideHasNoAllies()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: null);
            Body("Other", new Vector3(3f, 0f, 4f), yaw: 0f, side: 0);

            var status = Publisher(player).Refresh();

            Assert.That(status.Value.Contacts, Is.Empty);
        }

        [Test]
        public void ListsThePlayersPings()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: 1);
            GameSignals.Pings.Publish(new[]
            {
                new PingMarker(1, PingKind.Location, null, new System.Numerics.Vector3(5f, 0f, 6f), false, 0f, 10f),
                new PingMarker(2, PingKind.Location, null, new System.Numerics.Vector3(-2f, 1f, 3f), false, 0f, 10f),
            });

            var status = Publisher(player).Refresh();

            Assert.That(status.Value.Contacts, Is.EqualTo(new[]
            {
                new MinimapContact(MinimapContactKind.Ping, new System.Numerics.Vector3(5f, 0f, 6f), 0f, Team.Bravo),
                new MinimapContact(MinimapContactKind.Ping, new System.Numerics.Vector3(-2f, 1f, 3f), 0f, Team.Bravo),
            }));
        }

        [Test]
        public void ListsEnemiesRevealedByTheUav()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: 0);
            var pings = player.AddComponent<PingController>();
            pings.Clock = () => _now;
            pings.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Shooter.Core.Ping.On)));
            Body("Enemy", new Vector3(0f, 0f, 10f), yaw: 180f, side: 1);
            Body("Dummy", new Vector3(4f, 0f, 10f), yaw: 0f, side: null);
            var publisher = Publisher(player);

            Assert.That(publisher.Refresh().Value.Contacts, Is.Empty, "enemies are not listed before the UAV");

            LogAssert.Expect(LogType.Log, "[Streak] uav user=Player marked=2 seconds=10.00");
            UavReward.Use(player, pings, 10f);
            var contacts = publisher.Refresh().Value.Contacts;

            var enemies = contacts.Where(c => c.Kind == MinimapContactKind.Enemy).OrderBy(c => c.Position.X).ToArray();
            Assert.That(enemies.Length, Is.EqualTo(2));
            Assert.That(enemies[0].Position, Is.EqualTo(new System.Numerics.Vector3(0f, 0f, 10f)));
            Assert.That(enemies[0].HeadingDegrees, Is.EqualTo(180f).Within(_tolerance));
            Assert.That(enemies[0].Team, Is.EqualTo(Team.Bravo));
            Assert.That(enemies[1].Position, Is.EqualTo(new System.Numerics.Vector3(4f, 0f, 10f)));
            Assert.That(enemies[1].Team, Is.Null, "a body without a side has no team");
            Assert.That(contacts.Count(c => c.Kind == MinimapContactKind.Ping), Is.EqualTo(2), "the UAV's marks are listed as pings too");
            Assert.That(GameSignals.Minimap.Value.Value.Contacts.Count, Is.EqualTo(4));
        }

        [Test]
        public void RevealedEnemiesLeaveWhenTheSecondsRunOut()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: 0);
            var enemy = Body("Enemy", new Vector3(0f, 0f, 10f), yaw: 0f, side: 1);
            var publisher = Publisher(player);
            MinimapReveals.Reveal(enemy, 3f);

            _now = 2.99f;
            Assert.That(publisher.Refresh().Value.Contacts.Single().Kind, Is.EqualTo(MinimapContactKind.Enemy));

            _now = 3f;
            Assert.That(publisher.Refresh().Value.Contacts, Is.Empty);
            Assert.That(GameSignals.Minimap.Value.Value.Contacts, Is.Empty);
            Assert.That(MinimapReveals.Count, Is.Zero);
        }

        [Test]
        public void RevealedEnemiesFollowTheirMovesAndLeaveWhenKilled()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: 0);
            var enemy = Body("Enemy", new Vector3(0f, 0f, 10f), yaw: 0f, side: 1);
            var publisher = Publisher(player);
            MinimapReveals.Reveal(enemy, 10f);

            enemy.transform.position = new Vector3(6f, 0f, 8f);
            Assert.That(publisher.Refresh().Value.Contacts.Single().Position, Is.EqualTo(new System.Numerics.Vector3(6f, 0f, 8f)));

            enemy.Kill();
            Assert.That(publisher.Refresh().Value.Contacts, Is.Empty);
        }

        [Test]
        public void ARevealedBodyThatIsNowAnAllyIsNotAnEnemy()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: 0);
            var body = Body("Switched", new Vector3(0f, 0f, 10f), yaw: 0f, side: 1);
            var publisher = Publisher(player);
            MinimapReveals.Reveal(body, 10f);

            body.GetComponent<SideMember>().Side = new Side(0);

            Assert.That(publisher.Refresh().Value.Contacts.Single().Kind, Is.EqualTo(MinimapContactKind.Ally));
        }

        [Test]
        public void ListsObjectivesButNotABombCarriedByAnEnemy()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: 0);
            var ally = Body("Ally", new Vector3(-8f, 0f, 0f), yaw: 0f, side: 0);
            var enemy = Body("Enemy", new Vector3(8f, 0f, 0f), yaw: 0f, side: 1);
            Track(new GameObject("Point")).AddComponent<ControlPointZone>().transform.position = new Vector3(10f, 0f, 0f);
            Track(new GameObject("Site")).AddComponent<BombSite>().transform.position = new Vector3(0f, 0f, 20f);
            var bombObject = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            bombObject.name = "Bomb";
            var bomb = bombObject.AddComponent<BombObject>();
            var publisher = Publisher(player);

            bomb.ShowCarried(enemy.transform);
            Assert.That(Objectives(publisher), Is.EqualTo(new[]
            {
                new MinimapContact(MinimapContactKind.Objective, new System.Numerics.Vector3(10f, 0f, 0f), 0f, null),
                new MinimapContact(MinimapContactKind.Objective, new System.Numerics.Vector3(0f, 0f, 20f), 0f, null),
            }));

            bomb.ShowCarried(ally.transform);
            Assert.That(Objectives(publisher).Length, Is.EqualTo(3), "a bomb an ally carries is listed");

            bomb.ShowOnGround(new Vector3(4f, 0f, 4f), BombState.Dropped);
            var dropped = Objectives(publisher).Last();
            Assert.That(dropped.Position.X, Is.EqualTo(4f).Within(_tolerance));
            Assert.That(dropped.Position.Z, Is.EqualTo(4f).Within(_tolerance));

            bomb.Hide();
            Assert.That(Objectives(publisher).Length, Is.EqualTo(2), "a hidden bomb is not listed");
        }

        [Test]
        public void ListsTheRegisteredObjectivesThatAreShownBeforeTheModeObjectives()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: 0);
            Track(new GameObject("Point")).AddComponent<ControlPointZone>().transform.position = new Vector3(10f, 0f, 0f);
            var shown = new Objective(new Vector3(3f, 0f, 4f), true);
            var hidden = new Objective(new Vector3(-3f, 0f, 4f), false);
            MinimapObjectives.Add(shown);
            MinimapObjectives.Add(hidden);
            var publisher = Publisher(player);

            Assert.That(Objectives(publisher), Is.EqualTo(new[]
            {
                new MinimapContact(MinimapContactKind.Objective, new System.Numerics.Vector3(3f, 0f, 4f), 0f, null),
                new MinimapContact(MinimapContactKind.Objective, new System.Numerics.Vector3(10f, 0f, 0f), 0f, null),
            }));

            shown.Position = new Vector3(5f, 0f, 5f);
            hidden.Shows = true;
            Assert.That(Objectives(publisher), Is.EqualTo(new[]
            {
                new MinimapContact(MinimapContactKind.Objective, new System.Numerics.Vector3(5f, 0f, 5f), 0f, null),
                new MinimapContact(MinimapContactKind.Objective, new System.Numerics.Vector3(-3f, 0f, 4f), 0f, null),
                new MinimapContact(MinimapContactKind.Objective, new System.Numerics.Vector3(10f, 0f, 0f), 0f, null),
            }), "the publisher reads the objectives again on each refresh");

            MinimapObjectives.Remove(shown);
            MinimapObjectives.Remove(hidden);
            Assert.That(Objectives(publisher).Length, Is.EqualTo(1), "a removed objective is no longer listed");
        }

        [Test]
        public void PublishesOncePerUpdateInterval()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: 0);
            var publisher = Publisher(player);
            publisher.ApplySpec(new MinimapSpec(updateIntervalSeconds: 0.5f));
            var published = 0;
            GameSignals.Minimap.Changed += _ => published++;

            Assert.That(publisher.Tick(0f), Is.True, "the first tick publishes right away");
            Assert.That(publisher.Tick(0.49f), Is.False);
            Assert.That(publisher.Tick(0.5f), Is.True);
            Assert.That(publisher.Tick(0.99f), Is.False);
            Assert.That(publisher.Tick(1f), Is.True);
            Assert.That(published, Is.EqualTo(1), "an unchanged status is not published again");

            player.transform.position = new Vector3(1f, 0f, 0f);
            Assert.That(publisher.Tick(1.2f), Is.False);
            Assert.That(publisher.Tick(1.5f), Is.True);
            Assert.That(published, Is.EqualTo(2));
            Assert.That(GameSignals.Minimap.Value.Value.SelfPosition, Is.EqualTo(new System.Numerics.Vector3(1f, 0f, 0f)));
        }

        [Test]
        public void ClearsTheMinimapWhenThereIsNoLocalPlayer()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: 0);
            var publisher = Publisher(player);
            publisher.Refresh();
            Assert.That(GameSignals.Minimap.HasValue, Is.True);

            GameSignals.LocalPlayer.Clear();

            Assert.That(publisher.Refresh(), Is.Null);
            Assert.That(GameSignals.Minimap.HasValue, Is.False);
            Assert.That(publisher.Published, Is.Null);
        }

        [Test]
        public void DoesNotClearAMinimapAnotherPublisherPublished()
        {
            var player = Player(Vector3.zero, yaw: 0f, side: 0);
            var publisher = Publisher(player);
            publisher.Refresh();
            var other = new MinimapStatus(new System.Numerics.Vector3(9f, 0f, 9f), 0f, null);
            GameSignals.Minimap.Publish(other);

            GameSignals.LocalPlayer.Clear();
            publisher.Refresh();

            Assert.That(GameSignals.Minimap.Value, Is.EqualTo(other));
        }

        [Test]
        public void ApplySpecRejectsNull()
        {
            var publisher = Publisher(Player(Vector3.zero, yaw: 0f, side: 0));

            Assert.Throws<System.ArgumentNullException>(() => publisher.ApplySpec(null));
        }

        private static MinimapContact[] Objectives(MinimapPublisher publisher)
        {
            return publisher.Refresh().Value.Contacts.Where(c => c.Kind == MinimapContactKind.Objective).ToArray();
        }

        private MinimapPublisher Publisher(GameObject player)
        {
            var publisher = player.AddComponent<MinimapPublisher>();
            publisher.ApplySpec(MinimapSpec.Default);
            publisher.Clock = () => _now;
            return publisher;
        }

        private GameObject Player(Vector3 position, float yaw, int? side)
        {
            var go = Track(new GameObject("Player"));
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize("Player", Damageable.DefaultMaxHealth);
            if (side.HasValue)
            {
                go.AddComponent<SideMember>().Side = new Side(side.Value);
            }

            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(go, damageable, null, null, null, null, null, null));
            return go;
        }

        private Damageable Body(string name, Vector3 position, float yaw, int? side)
        {
            var go = Track(new GameObject(name));
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize(name, Damageable.DefaultMaxHealth);
            if (side.HasValue)
            {
                go.AddComponent<SideMember>().Side = new Side(side.Value);
            }

            return damageable;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }

        private sealed class Objective : IMinimapObjective
        {
            public Objective(Vector3 position, bool shows)
            {
                Position = position;
                Shows = shows;
            }

            public Vector3 Position { get; set; }

            public bool Shows { get; set; }

            public bool ShowsOnMinimap => Shows;
        }
    }
}
