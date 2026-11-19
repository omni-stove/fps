using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // ClientEconomy のある Shooter.NetV2.Client は Unity にも Shooter.Runtime にも依存しないので、GameSignals に出すのはこの部品が受け持つ。
    // 送る口 (BuySender) は財布と一緒に出し、経済の無い試合では出さない (オフラインの RoundEconomy と同じ)
    public sealed class NetV2BuyPart : INetV2ClientPart
    {
        private readonly ClientEconomy _economy;
        private readonly Func<IMessage, bool> _send;
        private readonly Action<string> _log;
        private readonly Sender _sender;
        private WalletStatus? _published;
        private bool _senderPublished;
        private bool _disposed;

        // 上限はスナップショットに載らないので、オフラインと同じ調整値 (KitTuning.Economy) の上限を使う
        public NetV2BuyPart(Func<IMessage, bool> send, Action<string> log = null)
            : this(send, KitTuning.Economy.Spec.MaxMoney, log)
        {
        }

        public NetV2BuyPart(Func<IMessage, bool> send, int maxMoney, Action<string> log = null)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _economy = new ClientEconomy(maxMoney);
            _log = log ?? (line => Debug.Log(line));
            _sender = new Sender(this);
        }

        public ClientEconomy Economy => _economy;

        public IBuySender BuySender => _sender;

        // 買った結果は届くお金の変化で分かるので、受け取るメッセージは無い
        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            if (_economy.Apply(snapshot))
            {
                Show(_economy.Status);
            }
        }

        public void Tick(float deltaSeconds)
        {
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _economy.Reset();
            Show(null);
        }

        private void Buy(string itemId)
        {
            if (_disposed || !_economy.Status.HasValue)
            {
                _log(EconomyLog.NetBuyRefused(itemId, _disposed ? "left the match" : "no economy in this match"));
                return;
            }

            var request = ClientEconomy.BuyRequest(itemId, out var problem);
            if (request == null)
            {
                _log(EconomyLog.NetBuyRefused(itemId, problem));
                return;
            }

            _log(EconomyLog.NetBuy(itemId, _send(request)));
        }

        // 他の側が後から出した値まで消さないよう、消すときは自分が出した値のときだけ消す
        private void Show(WalletStatus? status)
        {
            if (status.HasValue)
            {
                _published = status;
                _log(EconomyLog.NetWallet(status));
                GameSignals.Wallet.Publish(status);
                if (!_senderPublished)
                {
                    _senderPublished = true;
                    GameSignals.BuySender.Publish(_sender);
                }

                return;
            }

            if (_senderPublished)
            {
                _senderPublished = false;
                GameSignals.BuySender.ClearIfCurrent(_sender);
            }

            if (!_published.HasValue)
            {
                return;
            }

            var previous = _published;
            _published = null;
            _log(EconomyLog.NetWallet(null));
            GameSignals.Wallet.ClearIfCurrent(previous);
        }

        // 部品そのものを渡すと UI から Apply や Dispose まで呼べてしまうので、送る口だけの包みを渡す。
        private sealed class Sender : IBuySender
        {
            private readonly NetV2BuyPart _part;

            public Sender(NetV2BuyPart part)
            {
                _part = part;
            }

            public void Buy(string itemId)
            {
                _part.Buy(itemId);
            }
        }
    }
}
