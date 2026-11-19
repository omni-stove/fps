using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 開けたかはサーバーが決めて次のスナップショットの Opened で知らせるので、手元では先に開けた扱いにしない。
    // サーバーは断った知らせを送らないので、送った箱が待つ時間のうちに開かなければ断られたとみなす。
    public sealed class ClientLoot
    {
        // 往復の遅れとサーバーの 1 tick を足しても十分に届く長さ。これより短いと、遅い回線で開けたのに断られたと出る
        public const float DefaultAnswerSeconds = 2f;

        private readonly Func<IMessage, bool> _send;
        private readonly Action<string> _log;
        private readonly float _answerSeconds;
        private readonly InteractRequestMessage _request = new InteractRequestMessage();
        private readonly List<ClientLootItem> _boxes = new List<ClientLootItem>();
        private readonly List<ClientLootItem> _extractionPoints = new List<ClientLootItem>();
        private bool _hasPending;
        private int _pendingId;
        private ObjectiveItemKind _pendingKind;
        private float _pendingSeconds;

        public ClientLoot(Func<IMessage, bool> send, Action<string> log = null, float answerSeconds = DefaultAnswerSeconds)
        {
            if (!(answerSeconds > 0f) || float.IsInfinity(answerSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(answerSeconds), answerSeconds, "answerSeconds must be positive and finite");
            }

            _send = send ?? throw new ArgumentNullException(nameof(send));
            _log = log;
            _answerSeconds = answerSeconds;
        }

        public IReadOnlyList<ClientLootItem> Boxes => _boxes;

        public IReadOnlyList<ClientLootItem> ExtractionPoints => _extractionPoints;

        public bool HasPending => _hasPending;

        public int PendingItemId => _hasPending ? _pendingId : -1;

        public static bool IsBox(ObjectiveItemKind kind)
        {
            return kind == ObjectiveItemKind.LootBox || kind == ObjectiveItemKind.SupplyCrate || kind == ObjectiveItemKind.DeathBox;
        }

        // 一覧を作り直したら true。欠けたスナップショットでは物の抜けを消えたと取り違えるので、そろったものだけを使う
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!snapshot.IsComplete)
            {
                return false;
            }

            _boxes.Clear();
            _extractionPoints.Clear();
            var items = snapshot.ObjectiveItems;
            for (var i = 0; i < items.Count; i++)
            {
                ref var row = ref items[i];
                var item = new ClientLootItem(row.ItemId, row.Kind, row.Position, row.Opened, row.Radius);
                if (IsBox(row.Kind))
                {
                    _boxes.Add(item);
                }
                else if (row.Kind == ObjectiveItemKind.ExtractionPoint)
                {
                    _extractionPoints.Add(item);
                }
            }

            ResolvePending();
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!_hasPending || !(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _pendingSeconds += deltaSeconds;
            if (_pendingSeconds >= _answerSeconds)
            {
                _log?.Invoke(LootClientLog.Refused(_pendingId, _pendingKind, _pendingSeconds));
                _hasPending = false;
            }
        }

        // 開いていない箱のうち、届く距離にある最も近いもの
        public bool TryFindNearestClosedBox(Vector3 position, out ClientLootItem box)
        {
            box = default;
            var found = false;
            var best = float.PositiveInfinity;
            foreach (var candidate in _boxes)
            {
                if (candidate.Opened || !candidate.IsInReach(position))
                {
                    continue;
                }

                var distance = candidate.HorizontalDistanceTo(position);
                if (distance < best)
                {
                    best = distance;
                    box = candidate;
                    found = true;
                }
            }

            return found;
        }

        // 届く箱が無ければ何も送らない。Interact は乗り物などと共用なので、そのときはログも出さない。
        // 答えを待つ間は送り直さない。押し続けても同じ箱の要求を重ねてサーバーに断られないようにする
        public bool OpenNearest(Vector3 position)
        {
            if (_hasPending || !TryFindNearestClosedBox(position, out var box))
            {
                return false;
            }

            _request.ItemId = box.Id;
            var sent = _send(_request);
            _log?.Invoke(LootClientLog.Requested(box.Id, box.Kind, box.HorizontalDistanceTo(position), sent));
            if (sent)
            {
                _hasPending = true;
                _pendingId = box.Id;
                _pendingKind = box.Kind;
                _pendingSeconds = 0f;
            }

            return sent;
        }

        // 試合から抜けたときに呼ぶ。待っている答えも捨て、次の試合で同じ Id の箱を取り違えないようにする
        public void Reset()
        {
            _boxes.Clear();
            _extractionPoints.Clear();
            _hasPending = false;
        }

        private void ResolvePending()
        {
            if (!_hasPending)
            {
                return;
            }

            foreach (var box in _boxes)
            {
                if (box.Id != _pendingId)
                {
                    continue;
                }

                if (box.Opened)
                {
                    _log?.Invoke(LootClientLog.Opened(box.Id, box.Kind));
                    _hasPending = false;
                }

                return;
            }

            _log?.Invoke(LootClientLog.Gone(_pendingId, _pendingKind));
            _hasPending = false;
        }
    }
}
