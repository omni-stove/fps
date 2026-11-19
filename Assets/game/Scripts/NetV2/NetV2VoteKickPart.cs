using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // ClientVoteKick のある Shooter.NetV2.Client は Unity にも Shooter.Runtime にも依存しないので、GameSignals に出すのはこの部品が受け持つ。
    public sealed class NetV2VoteKickPart : INetV2ClientPart
    {
        private readonly ClientVoteKick _voteKick = new ClientVoteKick();
        private readonly Func<IMessage, bool> _send;
        private readonly Action<string> _log;
        private readonly Sender _sender;
        private VoteKickStatus? _published;
        private bool _senderPublished;
        private bool _disposed;

        public NetV2VoteKickPart(Func<IMessage, bool> send, Action<string> log = null)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _log = log ?? (line => Debug.Log(line));
            _sender = new Sender(this);
        }

        public ClientVoteKick VoteKick => _voteKick;

        public IVoteKickSender VoteKickSender => _sender;

        public bool Handle(IMessage message)
        {
            if (!(message is VoteKickStateMessage state))
            {
                return false;
            }

            if (_disposed)
            {
                return true;
            }

            if (!_voteKick.Receive(state))
            {
                _log(VoteKickLog.Malformed());
            }
            else if (state.Outcome != VoteKickOutcome.Undecided)
            {
                _log(VoteKickLog.Decided(state.TargetPlayerId, state.Outcome));
            }

            Show(_voteKick.Status);
            return true;
        }

        // 部品はつなぐ前に作られ、試合に入ったことは最初のスナップショットで分かるので、そこで送る口を出す (NetV2ReportPart と同じ)。
        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null || _senderPublished)
            {
                return;
            }

            _senderPublished = true;
            GameSignals.VoteKickSender.Publish(_sender);
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
            _voteKick.Reset();
            Show(null);
            if (_senderPublished)
            {
                _senderPublished = false;
                GameSignals.VoteKickSender.ClearIfCurrent(_sender);
            }
        }

        private void Call(int targetId)
        {
            if (_disposed)
            {
                _log(VoteKickLog.CallAfterLeaving(targetId));
                return;
            }

            var sent = _send(_voteKick.CallRequest(targetId));
            _log(VoteKickLog.Called(targetId, sent));
        }

        private void Vote(bool inFavor)
        {
            if (_disposed)
            {
                _log(VoteKickLog.VoteAfterLeaving(inFavor));
                return;
            }

            var request = _voteKick.VoteRequest(inFavor);
            if (request == null)
            {
                _log(VoteKickLog.NoVote(inFavor));
                return;
            }

            var sent = _send(request);
            _log(VoteKickLog.Voted(request.TargetPlayerId, inFavor, sent));
        }

        // 他の側が後から出した値まで消さないよう、消すときは自分が出した値のときだけ消す
        private void Show(VoteKickStatus? status)
        {
            if (status.HasValue)
            {
                if (_published.HasValue && _published.Value.Equals(status.Value))
                {
                    return;
                }

                _published = status;
                _log(VoteKickLog.Changed(status));
                GameSignals.VoteKick.Publish(status);
                return;
            }

            if (!_published.HasValue)
            {
                return;
            }

            var previous = _published;
            _published = null;
            _log(VoteKickLog.Changed(null));
            GameSignals.VoteKick.ClearIfCurrent(previous);
        }

        // 部品そのものを渡すと UI から Handle や Dispose まで呼べてしまうので、送る口だけの包みを渡す。
        private sealed class Sender : IVoteKickSender
        {
            private readonly NetV2VoteKickPart _part;

            public Sender(NetV2VoteKickPart part)
            {
                _part = part;
            }

            public void Call(int targetId)
            {
                _part.Call(targetId);
            }

            public void Vote(bool inFavor)
            {
                _part.Vote(inFavor);
            }
        }
    }
}
