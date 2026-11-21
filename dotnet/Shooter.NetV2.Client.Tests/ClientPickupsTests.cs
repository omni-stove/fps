using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientPickupsTests
    {
        private static readonly int[] _keptIds = { 1, 2, 3 };
        private static readonly int[] _requestedIds = { 3 };
        private readonly List<int> _sent = new List<int>();
        private readonly List<string> _log = new List<string>();
        private bool _connected;
        private ClientPickups _pickups;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _log.Clear();
            _connected = true;
            _pickups = new ClientPickups(
                message =>
                {
                    if (!_connected)
                    {
                        return false;
                    }

                    _sent.Add(((PickupRequestMessage)message).ItemId);
                    return true;
                },
                _log.Add);
        }

        [Test]
        public void OnlyPickupsAndAmmoCratesAreKept()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.WeaponPickup, new Vector3(1f, 0f, 0f), false, 1f, 7);
            AddItem(snapshot, 2, ObjectiveItemKind.HealthPickup, new Vector3(2f, 0f, 0f), true, 1.5f);
            AddItem(snapshot, 3, ObjectiveItemKind.AmmoCrate, new Vector3(3f, 0f, 0f), false, 1.6f);
            AddItem(snapshot, 4, ObjectiveItemKind.LootBox, Vector3.Zero, false, 1.6f);
            AddItem(snapshot, 5, ObjectiveItemKind.Flag, Vector3.Zero, false, 0f);

            Assert.That(_pickups.Apply(snapshot), Is.True);

            Assert.That(_pickups.Items.Select(i => i.Id), Is.EqualTo(_keptIds));
            Assert.That(_pickups.Items[0].OwnerId, Is.EqualTo(7));
            Assert.That(_pickups.Items[0].TryGetPickupKind(out var kind), Is.True);
            Assert.That(kind, Is.EqualTo(PickupKind.Weapon));
            Assert.That(_pickups.Items[1].IsAvailable, Is.False);
            Assert.That(_pickups.Items[1].OwnerId, Is.Null);
            Assert.That(_pickups.Items[2].IsAmmoCrate, Is.True);
            Assert.That(_pickups.Items[2].TryGetPickupKind(out _), Is.False);
        }

        [Test]
        public void TheNearestAvailablePickupInReachIsRequestedOnce()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.AmmoPickup, new Vector3(0.8f, 0f, 0f), false, 1f);
            AddItem(snapshot, 2, ObjectiveItemKind.HealthPickup, new Vector3(0.3f, 0f, 0f), true, 1f);
            AddItem(snapshot, 3, ObjectiveItemKind.WeaponPickup, new Vector3(0f, 0f, 0.5f), false, 1f);
            AddItem(snapshot, 4, ObjectiveItemKind.AmmoCrate, new Vector3(0f, 3f, 0f), false, 1.6f);
            _pickups.Apply(snapshot);

            Assert.That(_pickups.CollectNearest(Vector3.Zero), Is.True);
            Assert.That(_pickups.CollectNearest(Vector3.Zero), Is.False, "the answer is awaited before asking again");

            Assert.That(_sent, Is.EqualTo(_requestedIds), "a taken pickup and a crate a floor up are skipped");
            Assert.That(_pickups.PendingItemId, Is.EqualTo(3));
            Assert.That(_log.Last(), Does.Contain("[Pickup] net collect request item=3 kind=WeaponPickup distance=0.50 sent=True"));
        }

        [Test]
        public void NothingInReachSendsNothingAndLogsNothing()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.AmmoPickup, new Vector3(5f, 0f, 0f), false, 1f);
            _pickups.Apply(snapshot);

            Assert.That(_pickups.TryFindNearest(Vector3.Zero, out _), Is.False);
            Assert.That(_pickups.CollectNearest(Vector3.Zero), Is.False);
            Assert.That(_sent, Is.Empty);
            Assert.That(_log, Is.Empty);
        }

        [Test]
        public void AFailedSendDoesNotWait()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.AmmoCrate, Vector3.Zero, false, 1.6f);
            _pickups.Apply(snapshot);
            _connected = false;

            Assert.That(_pickups.CollectNearest(Vector3.Zero), Is.False);
            Assert.That(_pickups.HasPending, Is.False);
        }

        [Test]
        public void ATakenPickupOrAUsedCrateAnswersTheRequest()
        {
            var before = new SnapshotMessage();
            AddItem(before, 1, ObjectiveItemKind.AmmoCrate, Vector3.Zero, false, 1.6f);
            _pickups.Apply(before);
            _pickups.CollectNearest(Vector3.Zero);

            var after = new SnapshotMessage();
            AddItem(after, 1, ObjectiveItemKind.AmmoCrate, Vector3.Zero, true, 1.6f);
            _pickups.Apply(after);

            Assert.That(_pickups.HasPending, Is.False);
            Assert.That(_log.Last(), Does.Contain("[Pickup] net collected item=1 kind=AmmoCrate"));
        }

        [Test]
        public void ADropThatLeavesTheSnapshotAnswersTheRequest()
        {
            var before = new SnapshotMessage();
            AddItem(before, 9, ObjectiveItemKind.WeaponPickup, Vector3.Zero, false, 1f, 2);
            _pickups.Apply(before);
            _pickups.CollectNearest(Vector3.Zero);

            _pickups.Apply(new SnapshotMessage());

            Assert.That(_pickups.HasPending, Is.False);
            Assert.That(_log.Last(), Does.Contain("[Pickup] net collected item=9 kind=WeaponPickup reason=gone"));
        }

        [Test]
        public void AnUnansweredRequestIsGivenUpAfterTheWait()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.HealthPickup, Vector3.Zero, false, 1f);
            _pickups.Apply(snapshot);
            _pickups.CollectNearest(Vector3.Zero);

            _pickups.Apply(snapshot);
            _pickups.Tick(1f);
            Assert.That(_pickups.HasPending, Is.True);
            _pickups.Tick(1.1f);

            Assert.That(_pickups.HasPending, Is.False);
            Assert.That(_log.Last(), Does.Contain("[Pickup] net collect refused item=1 kind=HealthPickup reason=not-collected waited=2.10s"));
            Assert.That(_pickups.CollectNearest(Vector3.Zero), Is.True, "the player can ask again");
        }

        [Test]
        public void ResetForgetsTheItemsAndTheWait()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.HealthPickup, Vector3.Zero, false, 1f);
            _pickups.Apply(snapshot);
            _pickups.CollectNearest(Vector3.Zero);

            _pickups.Reset();

            Assert.That(_pickups.Items, Is.Empty);
            Assert.That(_pickups.HasPending, Is.False);
        }

        [Test]
        public void TheConstructorChecksItsArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientPickups(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientPickups(_ => true, null, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientPickups(_ => true, null, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => _pickups.Apply(null));
        }

        private static void AddItem(SnapshotMessage snapshot, int id, ObjectiveItemKind kind, Vector3 position, bool opened, float radius, int? owner = null)
        {
            ref var row = ref snapshot.ObjectiveItems.Add();
            row.ItemId = id;
            row.Present = ObjectiveItemFields.All;
            row.Kind = kind;
            row.Position = position;
            row.Opened = opened;
            row.Radius = radius;
            row.HasOwner = owner.HasValue;
            row.OwnerId = owner ?? 0;
        }
    }
}
