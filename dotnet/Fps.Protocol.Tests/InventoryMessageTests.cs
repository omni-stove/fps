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

        private const string _rifleItemId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";
        private const string _rifleId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";
        private const string _scopeId = "0d1e2f3a-4b5c-4d6e-9f8a-9b0c1d2e3f4a";
        private const string _gripId = "1e2f3a4b-5c6d-4e7f-8a9b-0c1d2e3f4a5b";

        private static readonly string[] _opticAndUnder = { "optic", "underbarrel" };
        private static readonly string[] _onlyScope = { _scopeId };
        private static readonly ItemSpec _rifleItem = new ItemSpec(_rifleItemId, "weapon.rifle", width: 2, weaponId: _rifleId, attachmentSlots: _opticAndUnder);
        private static readonly ItemSpec _scope = new ItemSpec(_scopeId, "item.scope", weight: 0.75f, attachment: new AttachmentSpec("optic", adsSpreadMultiplier: 0.5f));
        private static readonly ItemSpec _grip = new ItemSpec(_gripId, "item.grip", weight: 0.25f, attachment: new AttachmentSpec("underbarrel"));
        private static readonly WeaponSpec _rifle = new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, weight: 3f);

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
            writer.WriteMessage(new InventoryRequestMessage { Action = (InventoryAction)5 });

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
            // 最後の山のアイテムの番号 (後ろから 7 バイト目。後ろに付けた物の数が 1 バイト続く) を、無い番号に書き換える
            bytes[bytes.Length - 7] = 1;

            Assert.Throws<MessageReadException>(() => _client.Read(new System.ArraySegment<byte>(bytes)));
        }

        [Test]
        public void AnAttachedItemPointingPastTheItemsIsUnreadable()
        {
            var writer = new MessageWriter();
            var sent = new InventoryStateMessage();
            sent.AddItem(new InventoryStateMessage.Item(_rifleItemId, 1, 3f, 2, 1));
            sent.AddEntry(new InventoryStateMessage.Entry(1, 0, 1, 0, 0, false), new byte[] { 0 });
            writer.WriteMessage(sent);
            var bytes = writer.ToArray();
            // 最後の付けた物の番号 (最後のバイト) を、無い番号に書き換える
            bytes[bytes.Length - 1] = 1;

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

        [Test]
        public void AWeaponItemIsSentWithTheWeaponsWeightAndReadBackAsThatWeaponWithoutItsAmmo()
        {
            const string ammoId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d";
            const string rifleItemId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";
            const string rifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
            var ammo = new ItemSpec(ammoId, "item.ammo556", maxStack: 60, weight: 0.01f);
            var rifleItem = new ItemSpec(rifleItemId, "weapon.rifle", "rifle", width: 4, height: 2, weaponId: rifleId, ammoItemId: ammoId);
            var local = new ItemCatalog(new[] { ammo, rifleItem }, new InventoryRule(gridWidth: 8, gridHeight: 4));
            var inventory = new Inventory(local);
            inventory.HoldWeapons(new[] { new WeaponSpec(rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, weight: 3.5f) });
            var sent = new InventoryStateMessage();

            InventorySync.Write(inventory, sent);
            var catalog = InventorySync.ToCatalog(sent, local);

            Assert.That(sent.ItemAt(0).Weight, Is.EqualTo(3.5f));
            var got = catalog.Items.Single();
            Assert.That(got.WeaponId, Is.EqualTo(rifleId));
            Assert.That(got.AmmoItemId, Is.Null);
            Assert.That(got.IconId, Is.EqualTo("rifle"));
            Assert.That(new Inventory(catalog).WeightOf(got), Is.EqualTo(3.5f));
        }

        [Test]
        public void AttachAndDetachRequestsCarryTheItemToTakeOff()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new InventoryRequestMessage { Action = InventoryAction.Detach, Handle = 4, ItemId = _scopeId });

            var detach = (InventoryRequestMessage)_server.Read(writer.AsSegment());

            Assert.That(detach.Action, Is.EqualTo(InventoryAction.Detach));
            Assert.That(detach.Handle, Is.EqualTo(4));
            Assert.That(detach.ItemId, Is.EqualTo(_scopeId));

            var attachWriter = new MessageWriter();
            attachWriter.WriteMessage(new InventoryRequestMessage { Action = InventoryAction.Attach, Handle = 7 });
            var attach = (InventoryRequestMessage)_server.Read(attachWriter.AsSegment());
            Assert.That(attach.Action, Is.EqualTo(InventoryAction.Attach));
            Assert.That(attach.Handle, Is.EqualTo(7));
            Assert.That(attach.ItemId, Is.Empty);
        }

        [Test]
        public void TheStateCarriesWhatEachWeaponHasAttached()
        {
            var catalog = new ItemCatalog(new[] { _rifleItem, _scope, _grip }, new InventoryRule(gridWidth: 4, gridHeight: 2));
            var inventory = new Inventory(catalog);
            var attached = new[] { _scopeId, _gripId };
            inventory.HoldWeapons(new[] { WeaponAttachments.Apply(_rifle, attached, catalog) }, _ => attached);
            var sent = new InventoryStateMessage();
            InventorySync.Write(inventory, sent);
            var writer = new MessageWriter();
            writer.WriteMessage(sent);

            var got = (InventoryStateMessage)_client.Read(writer.AsSegment());

            // 武器の山 1 つに、付けた物 2 つ。付けた物は山に無くても表に載る
            Assert.That(got.EntryCount, Is.EqualTo(1));
            Assert.That(got.ItemCount, Is.EqualTo(3));
            Assert.That(got.EntryAt(0).AttachedCount, Is.EqualTo(2));
            Assert.That(got.ItemAt(got.AttachedItemAt(0, 0)).ItemId, Is.EqualTo(_scopeId));
            Assert.That(got.ItemAt(got.AttachedItemAt(0, 1)).ItemId, Is.EqualTo(_gripId));
            Assert.That(got.ItemAt(got.EntryAt(0).ItemIndex).Weight, Is.EqualTo(4f).Within(1e-4f));

            var received = InventorySync.ToCatalog(got, catalog);
            var copy = new Inventory(received);
            copy.Replace(InventorySync.ToEntries(got, received));
            copy.ReplaceAttachments(InventorySync.ToAttachments(got, received));

            Assert.That(copy.AttachmentsOf(_rifleId), Is.EqualTo(attached));
            Assert.That(received.TryFindAttachment(_scopeId, out var scope), Is.True);
            Assert.That(scope.Attachment.AdsSpreadMultiplier, Is.EqualTo(0.5f));
            Assert.That(received.SlotsOf(_rifleId), Is.EqualTo(_opticAndUnder));
        }

        [Test]
        public void AttachmentsTheClientDoesNotKnowAreLeftOut()
        {
            var catalog = new ItemCatalog(new[] { _rifleItem, _scope, _grip }, new InventoryRule(gridWidth: 4, gridHeight: 2));
            var inventory = new Inventory(catalog);
            var attached = new[] { _scopeId, _gripId };
            inventory.HoldWeapons(new[] { _rifle }, _ => attached);
            var sent = new InventoryStateMessage();
            InventorySync.Write(inventory, sent);
            var writer = new MessageWriter();
            writer.WriteMessage(sent);
            var got = (InventoryStateMessage)_client.Read(writer.AsSegment());

            // クライアントの表には握りが無い
            var received = InventorySync.ToCatalog(got, new ItemCatalog(new[] { _rifleItem, _scope }, InventoryRule.Default));
            var attachments = InventorySync.ToAttachments(got, received);

            Assert.That(attachments.Single().Key, Is.EqualTo(_rifleId));
            Assert.That(attachments.Single().Value, Is.EqualTo(_onlyScope));
        }
    }
}
