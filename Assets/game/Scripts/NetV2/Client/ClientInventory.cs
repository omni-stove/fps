using System;
using System.Text;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public sealed class ClientInventory
    {
        public const string LogTag = "[Inventory]";

        private readonly Func<IMessage, bool> _send;
        private readonly ItemCatalog _local;
        private readonly Action<string> _log;
        private readonly InventoryRequestMessage _request = new InventoryRequestMessage();

        public ClientInventory(Func<IMessage, bool> send, ItemCatalog local, Action<string> log = null)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _local = local ?? ItemCatalog.Empty;
            _log = log;
        }

        public event Action Changed;

        public Inventory Inventory { get; private set; }

        public void Receive(InventoryStateMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var catalog = InventorySync.ToCatalog(message, _local);
            var entries = InventorySync.ToEntries(message, catalog);
            var attachments = InventorySync.ToAttachments(message, catalog);
            if (Inventory == null || !SameLayout(Inventory.Catalog, catalog))
            {
                if (Inventory != null)
                {
                    Inventory.Changed -= OnInventoryChanged;
                }

                Inventory = new Inventory(catalog);
                Inventory.Replace(entries);
                Inventory.ReplaceAttachments(attachments);
                Inventory.Changed += OnInventoryChanged;
                _log?.Invoke($"{LogTag} received items={catalog.Items.Count} stacks={entries.Count} grid={catalog.Rule.GridWidth}x{catalog.Rule.GridHeight}");
                Changed?.Invoke();
                return;
            }

            Inventory.Replace(entries);
            Inventory.ReplaceAttachments(attachments);
        }

        public void Move(int handle, int x, int y, bool rotated)
        {
            if (Inventory == null || x < 0 || y < 0 || x > byte.MaxValue || y > byte.MaxValue)
            {
                return;
            }

            // 置き直しは届くのを待たずに手元で動かし、次に届いた中身で正す (ドラッグした物が一瞬戻って見えないように)
            if (Inventory.TryMove(handle, x, y, rotated) != InventoryMoveResult.Moved)
            {
                return;
            }

            Send(InventoryAction.Move, handle, x, y, rotated, 0);
        }

        public void Use(int handle)
        {
            Send(InventoryAction.Use, handle, 0, 0, false, 0);
        }

        public void Drop(int handle, int count)
        {
            Send(InventoryAction.Drop, handle, 0, 0, false, Math.Max(0, Math.Min(count, ushort.MaxValue)));
        }

        public void Attach(int handle)
        {
            Send(InventoryAction.Attach, handle, 0, 0, false, 0);
        }

        public void Detach(int weaponHandle, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || Encoding.UTF8.GetByteCount(itemId) > MatchProtocol.MaxContentIdBytes)
            {
                return;
            }

            Send(InventoryAction.Detach, weaponHandle, 0, 0, false, 0, itemId);
        }

        private void Send(InventoryAction action, int handle, int x, int y, bool rotated, int count, string itemId = "")
        {
            if (handle <= 0 || handle > Inventory.MaxHandle)
            {
                return;
            }

            _request.Action = action;
            _request.Handle = (ushort)handle;
            _request.X = (byte)x;
            _request.Y = (byte)y;
            _request.Rotated = rotated;
            _request.Count = (ushort)count;
            _request.ItemId = itemId;
            if (!_send(_request))
            {
                _log?.Invoke($"{LogTag} could not send {action} handle={handle}; not connected");
            }
        }

        private void OnInventoryChanged()
        {
            Changed?.Invoke();
        }

        private static bool SameLayout(ItemCatalog current, ItemCatalog received)
        {
            var a = current.Rule;
            var b = received.Rule;
            if (a.MaxStacks != b.MaxStacks || a.MaxWeight != b.MaxWeight || a.GridWidth != b.GridWidth || a.GridHeight != b.GridHeight
                || a.AllowRotation != b.AllowRotation || current.Items.Count != received.Items.Count)
            {
                return false;
            }

            for (var i = 0; i < received.Items.Count; i++)
            {
                var x = current.Items[i];
                var y = received.Items[i];
                if (x.Id != y.Id || x.Width != y.Width || x.Height != y.Height || x.Weight != y.Weight || x.MaxStack != y.MaxStack)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
