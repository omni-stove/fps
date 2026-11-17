using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientGrenadeTests
    {
        private const string _frag = "ff94f232-2647-4b77-a077-5693d8f954d1";
        private const string _smoke = "ecaf6e57-cb77-4707-9321-6ea5b659408f";
        private static readonly int[] _onlyGrenadeFive = { 5 };
        private static readonly string[] _startUpdateEnd = { "start 3 smoke", "update 3", "end 3" };

        [Test]
        public void AnAreaIsStartedUpdatedAndEnded()
        {
            var areas = new ClientAreaEffects();
            var log = new List<string>();
            areas.Started += s => log.Add("start " + s.AreaId + " " + s.KindId);
            areas.Updated += s => log.Add("update " + s.AreaId);
            areas.Ended += s => log.Add("end " + s.AreaId);

            areas.Receive(Area(3, 10f));
            Assert.That(areas.TryGet(3, out var state), Is.True);
            Assert.That(state.Radius, Is.EqualTo(5f));
            Assert.That(state.Center, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            areas.Receive(Area(3, 4f));
            areas.Receive(Area(3, 0f));

            Assert.That(log, Is.EqualTo(_startUpdateEnd));
            Assert.That(areas.Count, Is.Zero);
        }

        [Test]
        public void AnEndForAnUnknownAreaIsIgnored()
        {
            var areas = new ClientAreaEffects();
            var ended = 0;
            areas.Ended += _ => ended++;

            areas.Receive(Area(7, 0f));

            Assert.That(ended, Is.Zero);
        }

        [Test]
        public void ClearEndsEveryArea()
        {
            var areas = new ClientAreaEffects();
            var ended = 0;
            areas.Ended += _ => ended++;
            areas.Receive(Area(1, 5f));
            areas.Receive(Area(2, 5f));

            areas.Clear();

            Assert.That(ended, Is.EqualTo(2));
            Assert.That(areas.Count, Is.Zero);
        }

        [Test]
        public void TheFirstCarriedTypeIsThrownUntilAnotherIsSelected()
        {
            var grenades = new ClientGrenades();
            var pouch = Pouch(_frag, _smoke);

            Assert.That(grenades.Current(pouch), Is.EqualTo(_frag));
            Assert.That(grenades.SelectNext(pouch), Is.True);
            Assert.That(grenades.Current(pouch), Is.EqualTo(_smoke));
            Assert.That(grenades.SelectNext(pouch), Is.True);
            Assert.That(grenades.Current(pouch), Is.EqualTo(_frag));
        }

        [Test]
        public void ASelectionThatIsNoLongerCarriedFallsBackToTheFirst()
        {
            var grenades = new ClientGrenades();
            grenades.SelectNext(Pouch(_frag, _smoke));

            Assert.That(grenades.Current(Pouch(_frag)), Is.EqualTo(_frag));
            Assert.That(grenades.SelectNext(Pouch(_frag)), Is.False);
            Assert.That(grenades.Current(new GrenadePouchSync()), Is.Empty);
            Assert.That(grenades.Current(null), Is.Empty);
        }

        [Test]
        public void AFlyingGrenadeIsShownBetweenTheSnapshotsAroundTheRenderTick()
        {
            var world = World();
            var removed = new List<int>();
            world.ApplyGrenades(Snapshot(10, new Vector3(0f, 1f, 0f)), removed);
            world.ApplyGrenades(Snapshot(12, new Vector3(2f, 1f, 0f)), removed);

            Assert.That(world.TryShowGrenade(5, 11f, null, out var position, out _), Is.True);
            Assert.That(position.X, Is.EqualTo(1f).Within(1e-4f));
            world.TryShowGrenade(5, 20f, null, out position, out _);
            Assert.That(position.X, Is.EqualTo(2f).Within(1e-4f), "it waits at the newest snapshot");
            Assert.That(removed, Is.Empty);
        }

        [Test]
        public void AGrenadeStuckToSomeoneIsShownWhereTheyAreShownAndTurnsWithThem()
        {
            var world = World();
            var removed = new List<int>();
            var snapshot = Snapshot(10, new Vector3(2.5f, 1f, 0f));
            ref var grenade = ref snapshot.Grenades[0];
            grenade.IsStuck = true;
            grenade.HasStuckTo = true;
            grenade.StuckToId = 2;
            world.ApplyGrenades(snapshot, removed);

            var shownAt = new Vector3(3f, 0f, 10f);
            var shownYaw = 0f;
            bool Carrier(int playerId, out Vector3 at, out float yaw)
            {
                at = shownAt;
                yaw = shownYaw;
                return playerId == 2;
            }

            world.TryShowGrenade(5, 10f, Carrier, out var position, out _);
            Assert.That(Vector3.Distance(position, new Vector3(2.5f, 1f, 10f)), Is.LessThan(1e-4f));

            shownYaw = MathF.PI;
            world.TryShowGrenade(5, 10f, Carrier, out position, out _);
            Assert.That(Vector3.Distance(position, new Vector3(3.5f, 1f, 10f)), Is.LessThan(1e-4f));

            world.TryShowGrenade(5, 10f, null, out position, out _);
            Assert.That(position, Is.EqualTo(new Vector3(2.5f, 1f, 0f)), "without the carrier shown it stays where the server put it");
        }

        [Test]
        public void AGrenadeThatLeavesTheSnapshotIsForgotten()
        {
            var world = World();
            var removed = new List<int>();
            world.ApplyGrenades(Snapshot(10, Vector3.Zero), removed);

            var empty = Snapshot(11, Vector3.Zero);
            empty.Grenades.Clear();
            world.ApplyGrenades(empty, removed);

            Assert.That(removed, Is.EquivalentTo(_onlyGrenadeFive));
            Assert.That(world.GrenadeIds, Is.Empty);
            Assert.That(world.TryShowGrenade(5, 11f, null, out _, out _), Is.False);
        }

        private static ClientWorld World()
        {
            return new ClientWorld("Test", new WorldMap(Array.Empty<WorldCollider>()), NetMapLadders.None);
        }

        private static SnapshotMessage Snapshot(int tick, Vector3 grenadeAt)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.Tick = tick;
            ref var player = ref snapshot.Players.Add();
            player.PlayerId = 2;
            player.Position = new Vector3(3f, 0f, 0f);
            player.Yaw = 0f;
            ref var grenade = ref snapshot.Grenades.Add();
            grenade.GrenadeId = 5;
            grenade.Position = grenadeAt;
            grenade.Rotation = Quaternion.Identity;
            return snapshot;
        }

        private static AreaEffectMessage Area(int id, float remaining)
        {
            return new AreaEffectMessage { AreaId = id, KindId = "smoke", Center = new Vector3(1f, 0f, 2f), Radius = 5f, RemainingSeconds = remaining };
        }

        private static GrenadePouchSync Pouch(params string[] typeIds)
        {
            var pouch = new GrenadePouchSync { Enabled = true };
            foreach (var id in typeIds)
            {
                pouch.Add(id, 2);
            }

            return pouch;
        }
    }
}
