using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientStashTests
    {
        private const string _ammo = ClientExtractionStatusTests.Ammo;
        private const string _bandage = ClientExtractionStatusTests.Bandage;

        [Test]
        public void TheStashIsTheSameAsTheOfflineStatus()
        {
            var offline = new Stash(4, Catalog());
            offline.Add(_ammo, 45);
            offline.Add(_bandage, 3);
            var client = new ClientStash(4);

            var changed = client.Receive(Message(offline));

            Assert.That(changed, Is.True);
            Assert.That(client.Status, Is.EqualTo((StashStatus?)offline.Status));
            Assert.That(client.Status.Value.Capacity, Is.EqualTo(4));
            Assert.That(client.Status.Value.Stacks, Has.Count.EqualTo(3), "45 ammo is a full stack of 30 and one of 15");
            Assert.That(client.Status.Value.FreeSlots, Is.EqualTo(1));
            Assert.That(client.SkippedStacks, Is.Zero);
        }

        [Test]
        public void MoreStacksThanTheCapacityAreShownLikeOffline()
        {
            var offline = Stash.Restore(
                1, Catalog(), new[] { new StashEntry(_ammo, 30), new StashEntry(_bandage, 2) });
            var client = new ClientStash(1);

            client.Receive(Message(offline));

            Assert.That(client.Status, Is.EqualTo((StashStatus?)offline.Status));
            Assert.That(client.Status.Value.Capacity, Is.EqualTo(2), "the stack count is shown as the capacity");
        }

        [Test]
        public void TheSameStashKeepsTheStatus()
        {
            var client = new ClientStash(4);
            var message = new StashStateMessage();
            message.Stash.Add(_ammo, 10);
            client.Receive(message);

            Assert.That(client.Receive(message), Is.False);

            message.Stash.Clear();
            message.Stash.Add(_ammo, 11);
            Assert.That(client.Receive(message), Is.True);
            Assert.That(client.Status.Value.Stacks[0].Count, Is.EqualTo(11));
        }

        [Test]
        public void AnIdThatIsNotAnItemIdIsLeftOut()
        {
            var client = new ClientStash(4);
            var message = new StashStateMessage();
            message.Stash.Add("not-a-uuid", 1);
            message.Stash.Add(_ammo, 10);

            client.Receive(message);

            Assert.That(client.SkippedStacks, Is.EqualTo(1));
            Assert.That(client.Status.Value.Stacks, Has.Count.EqualTo(1));
            Assert.That(client.Status.Value.Stacks[0].ItemId, Is.EqualTo(_ammo));
        }

        [Test]
        public void ClearForgetsTheStash()
        {
            var client = new ClientStash(4);
            client.Receive(new StashStateMessage());

            Assert.That(client.Clear(), Is.True);
            Assert.That(client.Status, Is.Null);
            Assert.That(client.Clear(), Is.False);
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientStash(0));
            Assert.Throws<ArgumentNullException>(() => new ClientStash(4).Receive(null));
        }

        private static ItemCatalog Catalog()
        {
            return new ItemCatalog(
                new[] { new ItemSpec(_ammo, maxStack: 30), new ItemSpec(_bandage, maxStack: 5) },
                InventoryRule.Default);
        }

        private static StashStateMessage Message(Stash stash)
        {
            var message = new StashStateMessage();
            foreach (var stack in stash.Stacks)
            {
                message.Stash.Add(stack.ItemId, stack.Count);
            }

            return message;
        }
    }
}
