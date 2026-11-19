using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientLootTests
    {
        // 送る知らせは使い回しなので、送ったときの箱の Id を写しておく
        private readonly List<int> _sent = new List<int>();
        private readonly List<string> _log = new List<string>();
        private bool _connected;
        private ClientLoot _loot;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _log.Clear();
            _connected = true;
            _loot = new ClientLoot(
                message =>
                {
                    if (!_connected)
                    {
                        return false;
                    }

                    _sent.Add(((InteractRequestMessage)message).ItemId);
                    return true;
                },
                _log.Add);
        }

        [Test]
        public void ItemsAreSortedIntoBoxesAndExtractionPointsByKind()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.LootBox, new Vector3(1f, 0f, 0f), false, 1.6f);
            AddItem(snapshot, 2, ObjectiveItemKind.SupplyCrate, new Vector3(2f, 0f, 0f), false, 2f);
            AddItem(snapshot, 3, ObjectiveItemKind.DeathBox, new Vector3(3f, 0f, 0f), true, 1.6f);
            AddItem(snapshot, 4, ObjectiveItemKind.ExtractionPoint, new Vector3(20f, 0f, 5f), true, 3f);
            AddItem(snapshot, 5, ObjectiveItemKind.Flag, Vector3.Zero, false, 0f);

            Assert.That(_loot.Apply(snapshot), Is.True);

            Assert.That(_loot.Boxes, Has.Count.EqualTo(3));
            Assert.That(_loot.Boxes[0].Id, Is.EqualTo(1));
            Assert.That(_loot.Boxes[0].Kind, Is.EqualTo(ObjectiveItemKind.LootBox));
            Assert.That(_loot.Boxes[1].Kind, Is.EqualTo(ObjectiveItemKind.SupplyCrate));
            Assert.That(_loot.Boxes[1].Radius, Is.EqualTo(2f));
            Assert.That(_loot.Boxes[2].Kind, Is.EqualTo(ObjectiveItemKind.DeathBox));
            Assert.That(_loot.Boxes[2].Opened, Is.True);
            Assert.That(_loot.ExtractionPoints, Has.Count.EqualTo(1));
            Assert.That(_loot.ExtractionPoints[0].Id, Is.EqualTo(4));
            Assert.That(_loot.ExtractionPoints[0].Position, Is.EqualTo(new Vector3(20f, 0f, 5f)));
            Assert.That(_loot.ExtractionPoints[0].Opened, Is.True);
            Assert.That(_loot.ExtractionPoints[0].Radius, Is.EqualTo(3f));
        }

        [Test]
        public void TheNearestUnopenedBoxInReachIsChosen()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.LootBox, new Vector3(1.5f, 0f, 0f), false, 1.6f);
            AddItem(snapshot, 2, ObjectiveItemKind.SupplyCrate, new Vector3(0f, 0f, 1f), false, 1.6f);
            AddItem(snapshot, 3, ObjectiveItemKind.DeathBox, new Vector3(0.5f, 0f, 0f), true, 1.6f);
            _loot.Apply(snapshot);

            Assert.That(_loot.TryFindNearestClosedBox(Vector3.Zero, out var box), Is.True);
            Assert.That(box.Id, Is.EqualTo(2), "the opened box is nearer but is skipped");
        }

        [Test]
        public void ReachIsMeasuredOnTheGround()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.LootBox, new Vector3(0f, 10f, 1.5f), false, 1.6f);
            _loot.Apply(snapshot);

            Assert.That(_loot.TryFindNearestClosedBox(Vector3.Zero, out var box), Is.True, "height does not matter");
            Assert.That(box.HorizontalDistanceTo(Vector3.Zero), Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void ABoxOutOfReachIsNotChosenAndNothingIsSent()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 1, ObjectiveItemKind.LootBox, new Vector3(2f, 0f, 0f), false, 1.6f);
            AddItem(snapshot, 2, ObjectiveItemKind.ExtractionPoint, Vector3.Zero, true, 3f);
            _loot.Apply(snapshot);

            Assert.That(_loot.TryFindNearestClosedBox(Vector3.Zero, out _), Is.False);
            Assert.That(_loot.OpenNearest(Vector3.Zero), Is.False);
            Assert.That(_sent, Is.Empty);
            Assert.That(_log, Is.Empty, "Interact is shared with other actions, so pressing it away from a box logs nothing");
        }

        [Test]
        public void OpeningTheNearestBoxSendsAnInteractRequestForIt()
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 7, ObjectiveItemKind.LootBox, new Vector3(1f, 0f, 0f), false, 1.6f);
            AddItem(snapshot, 9, ObjectiveItemKind.LootBox, new Vector3(0f, 0f, 1.2f), false, 1.6f);
            _loot.Apply(snapshot);

            Assert.That(_loot.OpenNearest(Vector3.Zero), Is.True);

            Assert.That(_sent, Is.EqualTo(new[] { 7 }));
            Assert.That(_loot.PendingItemId, Is.EqualTo(7));
            Assert.That(_log, Does.Contain("[Loot] net open request item=7 kind=LootBox distance=1.00 sent=True"));
        }

        [Test]
        public void WhileWaitingForTheAnswerTheRequestIsNotSentAgain()
        {
            ApplyOneBox(opened: false);

            Assert.That(_loot.OpenNearest(Vector3.Zero), Is.True);
            Assert.That(_loot.OpenNearest(Vector3.Zero), Is.False);

            Assert.That(_sent, Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void TheBoxTurningOpenedIsLoggedAsOpened()
        {
            ApplyOneBox(opened: false);
            _loot.OpenNearest(Vector3.Zero);

            ApplyOneBox(opened: true);

            Assert.That(_loot.HasPending, Is.False);
            Assert.That(_log, Does.Contain("[Loot] net opened item=5 kind=SupplyCrate"));
            Assert.That(_loot.OpenNearest(Vector3.Zero), Is.False, "an opened box is not chosen again");
        }

        [Test]
        public void ABoxThatStaysClosedIsLoggedAsRefusedAfterTheWait()
        {
            var loot = new ClientLoot(_ => true, _log.Add, answerSeconds: 1f);
            var snapshot = OneBox(opened: false);
            loot.Apply(snapshot);
            loot.OpenNearest(Vector3.Zero);

            loot.Tick(0.6f);
            loot.Apply(snapshot);
            Assert.That(loot.HasPending, Is.True);

            loot.Tick(0.6f);

            Assert.That(loot.HasPending, Is.False);
            Assert.That(_log, Does.Contain("[Loot] net open refused item=5 kind=SupplyCrate reason=not-opened waited=1.20s"));
            Assert.That(loot.OpenNearest(Vector3.Zero), Is.True, "after a refusal the box can be asked for again");
        }

        [Test]
        public void ABoxThatDisappearsIsLoggedAsRefused()
        {
            ApplyOneBox(opened: false);
            _loot.OpenNearest(Vector3.Zero);

            _loot.Apply(new SnapshotMessage());

            Assert.That(_loot.HasPending, Is.False);
            Assert.That(_loot.Boxes, Is.Empty);
            Assert.That(_log, Does.Contain("[Loot] net open refused item=5 kind=SupplyCrate reason=gone"));
        }

        [Test]
        public void ARequestWhileDisconnectedIsNotAwaited()
        {
            ApplyOneBox(opened: false);
            _connected = false;

            Assert.That(_loot.OpenNearest(Vector3.Zero), Is.False);

            Assert.That(_loot.HasPending, Is.False);
            Assert.That(_log, Does.Contain("[Loot] net open request item=5 kind=SupplyCrate distance=0.50 sent=False"));
        }

        [Test]
        public void ResetForgetsTheItemsAndTheWaitingRequest()
        {
            ApplyOneBox(opened: false);
            _loot.OpenNearest(Vector3.Zero);

            _loot.Reset();

            Assert.That(_loot.Boxes, Is.Empty);
            Assert.That(_loot.HasPending, Is.False);
            Assert.That(_loot.PendingItemId, Is.EqualTo(-1));
        }

        [Test]
        public void AnAnswerWaitThatIsNotPositiveIsRejected()
        {
            Assert.That(() => new ClientLoot(_ => true, null, 0f), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => new ClientLoot(null), Throws.ArgumentNullException);
        }

        private void ApplyOneBox(bool opened)
        {
            _loot.Apply(OneBox(opened));
        }

        private static SnapshotMessage OneBox(bool opened)
        {
            var snapshot = new SnapshotMessage();
            AddItem(snapshot, 5, ObjectiveItemKind.SupplyCrate, new Vector3(0.5f, 0f, 0f), opened, 1.6f);
            return snapshot;
        }

        private static void AddItem(SnapshotMessage snapshot, int id, ObjectiveItemKind kind, Vector3 position, bool opened, float radius)
        {
            ref var row = ref snapshot.ObjectiveItems.Add();
            row.ItemId = id;
            row.Present = ObjectiveItemFields.All;
            row.Kind = kind;
            row.Position = position;
            row.Opened = opened;
            row.Radius = radius;
        }
    }
}
