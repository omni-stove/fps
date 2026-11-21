using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 拾えたかはサーバーが決め、次のスナップショットで物が消える (落とし物) か Opened になる (出現点・弾薬箱) ことで知らせるので、
    // 手元では先に拾った扱いにしない。サーバーは断った知らせを送らないので、送った物が待つ時間のうちに変わらなければ断られたとみなす。
    // 触れて拾う物はサーバーが勝手に拾わせるので、ここで送るのは押して拾う要求だけ
    public sealed class ClientPickups
    {
        // 往復の遅れとサーバーの 1 tick を足しても十分に届く長さ (ClientLoot と同じ)
        public const float DefaultAnswerSeconds = 2f;

        // サーバーの ServerPickups.MaxVerticalReach と同じ
        public const float MaxVerticalReach = 2f;

        private readonly Func<IMessage, bool> _send;
        private readonly Action<string> _log;
        private readonly float _answerSeconds;
        private readonly PickupRequestMessage _request = new PickupRequestMessage();
        private readonly List<ClientPickupItem> _items = new List<ClientPickupItem>();
        private bool _hasPending;
        private int _pendingId;
        private ObjectiveItemKind _pendingKind;
        private float _pendingSeconds;

        public ClientPickups(Func<IMessage, bool> send, Action<string> log = null, float answerSeconds = DefaultAnswerSeconds)
        {
            if (!(answerSeconds > 0f) || float.IsInfinity(answerSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(answerSeconds), answerSeconds, "answerSeconds must be positive and finite");
            }

            _send = send ?? throw new ArgumentNullException(nameof(send));
            _log = log;
            _answerSeconds = answerSeconds;
        }

        public IReadOnlyList<ClientPickupItem> Items => _items;

        public bool HasPending => _hasPending;

        public int PendingItemId => _hasPending ? _pendingId : -1;

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

            _items.Clear();
            var rows = snapshot.ObjectiveItems;
            for (var i = 0; i < rows.Count; i++)
            {
                ref var row = ref rows[i];
                if (PickupItemKinds.IsCollectable(row.Kind))
                {
                    _items.Add(new ClientPickupItem(row.ItemId, row.Kind, row.Position, row.Opened, row.Radius, row.HasOwner ? row.OwnerId : (int?)null));
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
                _log?.Invoke(PickupClientLog.Refused(_pendingId, _pendingKind, _pendingSeconds));
                _hasPending = false;
            }
        }

        // 拾える (出直しや次に使えるのを待っていない) 物のうち、届く距離にある最も近いもの
        public bool TryFindNearest(Vector3 feet, out ClientPickupItem item)
        {
            item = default;
            var found = false;
            var best = float.PositiveInfinity;
            foreach (var candidate in _items)
            {
                if (!candidate.IsAvailable || !candidate.IsInReach(feet))
                {
                    continue;
                }

                var distance = candidate.HorizontalDistanceTo(feet);
                if (distance < best)
                {
                    best = distance;
                    item = candidate;
                    found = true;
                }
            }

            return found;
        }

        // 届く物が無ければ何も送らない。Interact は乗り物や箱と共用なので、そのときはログも出さない。
        // 答えを待つ間は送り直さない。押し続けても同じ物の要求を重ねてサーバーに断られないようにする
        public bool CollectNearest(Vector3 feet)
        {
            if (_hasPending || !TryFindNearest(feet, out var item))
            {
                return false;
            }

            _request.ItemId = item.Id;
            var sent = _send(_request);
            _log?.Invoke(PickupClientLog.Requested(item.Id, item.Kind, item.HorizontalDistanceTo(feet), sent));
            if (sent)
            {
                _hasPending = true;
                _pendingId = item.Id;
                _pendingKind = item.Kind;
                _pendingSeconds = 0f;
            }

            return sent;
        }

        // 試合から抜けたときに呼ぶ。待っている答えも捨て、次の試合で同じ Id の物を取り違えないようにする
        public void Reset()
        {
            _items.Clear();
            _hasPending = false;
        }

        private void ResolvePending()
        {
            if (!_hasPending)
            {
                return;
            }

            foreach (var item in _items)
            {
                if (item.Id != _pendingId)
                {
                    continue;
                }

                if (item.Opened)
                {
                    _log?.Invoke(PickupClientLog.Collected(item.Id, item.Kind));
                    _hasPending = false;
                }

                return;
            }

            // 落とし物は拾われると並びから消える。遠くて載らなくなったのと見分けられないが、押した時は届く距離にあったので拾えたとみなす
            _log?.Invoke(PickupClientLog.Gone(_pendingId, _pendingKind));
            _hasPending = false;
        }
    }
}
