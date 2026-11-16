using System.Linq;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    public sealed class InventoryMessageTests
    {
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _caseId = "2f4e6d8c-0b1a-4c3e-8d5f-7a9b1c3d5e7f";

        private static readonly ItemSpec _herb = new ItemSpec(_herbId, "item.herb", "herb", maxStack: 5, weight: 0.5f, useHealth: 25);
        private static readonly ItemSpec _case = new ItemSpec(_caseId, "item.case", weight: 4f, width: 3, height: 1);

        private static readonly int[] _onlySecond = { 2 };

        private readonly MessageTable _server = MatchMessages.CreateServerTable();
        private readonly MessageTable _client = MatchMessages.CreateClientTable();

        [Test]
        public void TheRequestRoundTripsToTheServerOnly()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new InventoryRequestMessage { Action = InventoryAction.Move, Handle = 513, X = 3, Y = 7, Rotated = true, Count = 2 });

            var got = (InventoryRequestMessage)_server.Read(writer.AsSegment());

            Assert.That(got.Action, Is.EqualTo(InventoryAction.Move));
            Assert.That(got.Handle, Is.EqualTo(513));
            Assert.That(got.X, Is.EqualTo(3));
            Assert.That(got.Y, Is.EqualTo(7));
            Assert.That(got.Rotated, Is.True);
            Assert.That(got.Count, Is.EqualTo(2));
            Assert.That(_client.TryGet(MessageIds.InventoryRequest, out _), Is.False);
        }

        [Test]
        public void AnUnknownActionIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new InventoryRequestMessage { Action = (InventoryAction)3 });

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void TheStateCarriesTheRuleItemsAndEntriesToTheClientOnly()
        {
            var inventory = new Inventory(new ItemCatalog(new[] { _herb, _case }, new InventoryRule(maxStacks: 9, maxWeight: 20f, gridWidth: 4, gridHeight: 3, allowRotation: false)));
            inventory.Add(_caseId, 1);
            inventory.Add(_herbId, 7);
            var sent = new InventoryStateMessage();
            InventorySync.Write(inventory, sent);
            var writer = new MessageWriter();
            writer.WriteMessage(sent);

            var got = (InventoryStateMessage)_client.Read(writer.AsSegment());

            Assert.That(got.MaxStacks, Is.EqualTo(9));
            Assert.That(got.MaxWeight, Is.EqualTo(20f));
            Assert.That(got.GridWidth, Is.EqualTo(4));
            Assert.That(got.GridHeight, Is.EqualTo(3));
            Assert.That(got.AllowRotation, Is.False);
            Assert.That(got.ItemCount, Is.EqualTo(2));
            Assert.That(got.EntryCount, Is.EqualTo(3));
            Assert.That(_server.TryGet(MessageIds.InventoryState, out _), Is.False);

            // クライアントの表には箱が無く、ハーブの名前と効果だけを知っている
            var local = new ItemCatalog(new[] { _herb }, InventoryRule.Default);
            var catalog = InventorySync.ToCatalog(got, local);
            var copy = new Inventory(catalog);
            copy.Replace(InventorySync.ToEntries(got, catalog));

            Assert.That(catalog.Rule.GridWidth, Is.EqualTo(4));
            Assert.That(catalog.Rule.AllowRotation, Is.False);
            Assert.That(catalog.TryFind(_herbId, out var herb), Is.True);
            Assert.That(herb.NameKey, Is.EqualTo("item.herb"));
            Assert.That(herb.UseHealth, Is.EqualTo(25));
            Assert.That(catalog.TryFind(_caseId, out var box), Is.True);
            Assert.That(box.NameKey, Is.Empty);
            Assert.That(box.Width, Is.EqualTo(3));
            Assert.That(copy.SameAs(inventory.Stacks), Is.True);
            Assert.That(copy.TotalWeight, Is.EqualTo(inventory.TotalWeight).Within(1e-4f));
        }

        [Test]
        public void AnEntryPointingPastTheItemsIsUnreadable()
        {
            var writer = new MessageWriter();
            var sent = new InventoryStateMessage();
            sent.AddItem(new InventoryStateMessage.Item(_herbId, 5, 0.5f, 1, 1));
            sent.AddEntry(new InventoryStateMessage.Entry(1, 0, 2, 0, 0, false));
            writer.WriteMessage(sent);
            var bytes = writer.ToArray();
            // 最後の山のアイテムの番号 (後ろから 6 バイト目) を、無い番号に書き換える
            bytes[bytes.Length - 6] = 1;

            Assert.Throws<MessageReadException>(() => _client.Read(new System.ArraySegment<byte>(bytes)));
        }

        [Test]
        public void EntriesWithUnreadableItemsAreLeftOut()
        {
            var sent = new InventoryStateMessage();
            sent.AddItem(new InventoryStateMessage.Item("not-a-uuid", 1, 0f, 1, 1));
            sent.AddItem(new InventoryStateMessage.Item(_herbId, 5, 0.5f, 1, 1));
            sent.AddEntry(new InventoryStateMessage.Entry(1, 0, 1, 0, 0, false));
            sent.AddEntry(new InventoryStateMessage.Entry(2, 1, 3, 1, 0, false));

            var catalog = InventorySync.ToCatalog(sent, null);
            var entries = InventorySync.ToEntries(sent, catalog);

            Assert.That(entries.Select(e => e.Handle), Is.EqualTo(_onlySecond));
        }
    }
}
