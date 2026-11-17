using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    public sealed class NetV2Inventory : IInventoryControl
    {
        private readonly ClientInventory _client;

        public NetV2Inventory(Func<IMessage, bool> send)
        {
            _client = new ClientInventory(send, KitTuning.Items, line => Debug.Log(line));
            _client.Changed += () => Changed?.Invoke();
        }

        public event Action Changed;

        public Inventory Inventory => _client.Inventory;

        public void Receive(InventoryStateMessage message) => _client.Receive(message);

        public void Move(int handle, int x, int y, bool rotated) => _client.Move(handle, x, y, rotated);

        public void Use(int handle) => _client.Use(handle);

        public void Drop(int handle, int count) => _client.Drop(handle, count);

        public void Attach(int handle) => _client.Attach(handle);

        public void Detach(int weaponHandle, string itemId) => _client.Detach(weaponHandle, itemId);
    }
}
