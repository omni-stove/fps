using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientInventoryTests
    {
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _caseId = "2f4e6d8c-0b1a-4c3e-8d5f-7a9b1c3d5e7f";

        private static readonly ItemSpec _herb = new ItemSpec(_herbId, "item.herb", maxStack: 5, weight: 0.5f, useHealth: 25);
        private static readonly ItemSpec _case = new ItemSpec(_caseId, "item.case", weight: 4f, width: 2, height: 1);
        private static readonly InventoryAction[] _useThenDrop = { InventoryAction.Use, InventoryAction.Drop };
        private static readonly InventoryRule _grid = new InventoryRule(gridWidth: 4, gridHeight: 2);

        private const string _rifleItemId = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e";
        private const string _rifleId = "5e6f7a8b-9c0d-4e1f-8a3b-4c5d6e7f8a9b";
        private const string _scopeId = "0d1e2f3a-4b5c-4d6e-9f8a-9b0c1d2e3f4a";
        private static readonly string[] _opticSlot = { "optic" };
        private static readonly string[] _scopeOnly = { _scopeId };
        private static readonly InventoryAction[] _attachThenDetach = { InventoryAction.Attach, InventoryAction.Detach };
        private static readonly ItemSpec _rifleItem = new ItemSpec(_rifleItemId, "weapon.rifle", width: 2, weaponId: _rifleId, attachmentSlots: _opticSlot);
        private static readonly ItemSpec _scope = new ItemSpec(_scopeId, "item.scope", weight: 0.5f, attachment: new AttachmentSpec("optic"));

        private readonly List<InventoryRequestMessage> _sent = new List<InventoryRequestMessage>();
        private bool _connected = true;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _connected = true;
        }

        [Test]
        public void NothingIsHeldBeforeTheFirstState()
        {
            var client = Create();

            Assert.That(client.Inventory, Is.Null);
        }

        [Test]
        public void TheStateIsCopiedWithLocalNamesAndEffects()
        {
            var client = Create();
            var changes = 0;
            client.Changed += () => changes++;

            client.Receive(State(Server(), herbs: 7, cases: 1));

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(client.Inventory.CountOf(_herbId), Is.EqualTo(7));
            Assert.That(client.Inventory.CountOf(_caseId), Is.EqualTo(1));
            Assert.That(client.Inventory.Rule.GridWidth, Is.EqualTo(4));
            Assert.That(client.Inventory.Catalog.TryFind(_herbId, out var herb), Is.True);
            Assert.That(herb.NameKey, Is.EqualTo("item.herb"));
            Assert.That(herb.UseHealth, Is.EqualTo(25));
        }

        [Test]
        public void TheSameLayoutKeepsTheInventoryAndOnlyReplacesTheStacks()
        {
            var client = Create();
            client.Receive(State(Server(), herbs: 7, cases: 1));
            var first = client.Inventory;

            client.Receive(State(Server(), herbs: 3, cases: 1));

            Assert.That(client.Inventory, Is.SameAs(first));
            Assert.That(client.Inventory.CountOf(_herbId), Is.EqualTo(3));
        }

        [Test]
        public void ANewItemTypeRebuildsTheInventory()
        {
            var client = Create();
            client.Receive(State(Server(), herbs: 2, cases: 0));
            var first = client.Inventory;

            client.Receive(State(Server(), herbs: 2, cases: 1));

            Assert.That(client.Inventory, Is.Not.SameAs(first));
            Assert.That(client.Inventory.CountOf(_caseId), Is.EqualTo(1));
        }

        [Test]
        public void AMoveIsShownAtOnceAndSentToTheServer()
        {
            var client = Create();
            client.Receive(State(Server(), herbs: 0, cases: 1));
            var handle = client.Inventory.Stacks[0].Handle;

            client.Move(handle, 2, 1, false);

            Assert.That(client.Inventory.Stacks[0].X, Is.EqualTo(2));
            Assert.That(client.Inventory.Stacks[0].Y, Is.EqualTo(1));
            var sent = _sent.Single();
            Assert.That(sent.Action, Is.EqualTo(InventoryAction.Move));
            Assert.That(sent.Handle, Is.EqualTo(handle));
            Assert.That(sent.X, Is.EqualTo(2));
            Assert.That(sent.Y, Is.EqualTo(1));
        }

        [Test]
        public void AMoveThatCannotBePlacedIsNotSent()
        {
            var client = Create();
            client.Receive(State(Server(), herbs: 0, cases: 1));

            client.Move(client.Inventory.Stacks[0].Handle, 3, 0, false);

            Assert.That(_sent, Is.Empty);
            Assert.That(client.Inventory.Stacks[0].X, Is.Zero);
        }

        [Test]
        public void UseAndDropAreSentWithoutChangingTheCopy()
        {
            var client = Create();
            client.Receive(State(Server(), herbs: 4, cases: 0));
            var handle = client.Inventory.Stacks[0].Handle;

            client.Use(handle);
            client.Drop(handle, 2);

            Assert.That(_sent.Select(m => m.Action), Is.EqualTo(_useThenDrop));
            Assert.That(_sent[1].Count, Is.EqualTo(2));
            Assert.That(client.Inventory.CountOf(_herbId), Is.EqualTo(4), "the server's next state decides");
        }

        [Test]
        public void ARequestThatCannotBeSentIsLogged()
        {
            var lines = new List<string>();
            var client = new ClientInventory(Send, new ItemCatalog(new[] { _herb }, InventoryRule.Default), lines.Add);
            _connected = false;

            client.Use(1);

            Assert.That(lines, Has.Some.Contains("[Inventory] could not send Use handle=1"));
        }

        [Test]
        public void AStateWithoutTheAttachmentTakesItOff()
        {
            var catalog = new ItemCatalog(new[] { _rifleItem, _scope }, _grid);
            var client = new ClientInventory(Send, catalog);
            var server = new Inventory(catalog);
            server.HoldWeapons(new[] { new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, weight: 3f) }, _ => _scopeOnly);
            var message = new InventoryStateMessage();
            InventorySync.Write(server, message);
            var changes = 0;

            client.Receive(message);
            client.Changed += () => changes++;
            server.HoldWeapons(new[] { new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, weight: 3f) }, null);
            InventorySync.Write(server, message);
            client.Receive(message);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(client.Inventory.AttachmentsOf(_rifleId), Is.Empty);
        }

        [Test]
        public void TheFirstStateFillsTheAttachments()
        {
            var catalog = new ItemCatalog(new[] { _rifleItem, _scope }, _grid);
            var client = new ClientInventory(Send, catalog);
            var server = new Inventory(catalog);
            server.HoldWeapons(new[] { new WeaponSpec(_rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, weight: 3f) }, _ => _scopeOnly);
            var message = new InventoryStateMessage();
            InventorySync.Write(server, message);

            client.Receive(message);

            Assert.That(client.Inventory.AttachmentsOf(_rifleId), Is.EqualTo(_scopeOnly));
        }

        [Test]
        public void AttachAndDetachAreSentWithoutChangingTheCopy()
        {
            var client = Create();
            client.Receive(State(Server(), herbs: 1, cases: 0));

            client.Attach(3);
            client.Detach(5, _scopeId);
            client.Detach(5, "");

            Assert.That(_sent.Select(m => m.Action), Is.EqualTo(_attachThenDetach));
            Assert.That(_sent[0].Handle, Is.EqualTo(3));
            Assert.That(_sent[0].ItemId, Is.Empty);
            Assert.That(_sent[1].Handle, Is.EqualTo(5));
            Assert.That(_sent[1].ItemId, Is.EqualTo(_scopeId));
        }

        private ClientInventory Create()
        {
            return new ClientInventory(Send, new ItemCatalog(new[] { _herb }, InventoryRule.Default));
        }

        private bool Send(IMessage message)
        {
            if (!_connected)
            {
                return false;
            }

            var request = (InventoryRequestMessage)message;
            _sent.Add(new InventoryRequestMessage
            {
                Action = request.Action,
                Handle = request.Handle,
                X = request.X,
                Y = request.Y,
                Rotated = request.Rotated,
                Count = request.Count,
                ItemId = request.ItemId,
            });
            return true;
        }

        private static Inventory Server()
        {
            return new Inventory(new ItemCatalog(new[] { _herb, _case }, _grid));
        }

        private static InventoryStateMessage State(Inventory server, int herbs, int cases)
        {
            server.Add(_caseId, cases);
            server.Add(_herbId, herbs);
            var message = new InventoryStateMessage();
            InventorySync.Write(server, message);
            return message;
        }
    }
}
