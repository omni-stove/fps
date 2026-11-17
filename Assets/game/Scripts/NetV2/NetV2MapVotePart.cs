using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    public sealed class NetV2MapVotePart : INetV2ClientPart
    {
        private readonly ClientMapVote _vote;
        private readonly Sender _sender;
        private readonly Action<string> _log;
        private MapVoteStatus? _published;
        private bool _senderPublished;
        private bool _disposed;

        public NetV2MapVotePart(Func<IMessage, bool> send, Action<string> log = null)
        {
            _vote = new ClientMapVote(send ?? throw new ArgumentNullException(nameof(send)));
            _sender = new Sender(this);
            _log = log ?? (line => Debug.Log(line));
        }

        public ClientMapVote Vote => _vote;

        public IMapVoteSender MapVoteSender => _sender;

        public bool Handle(IMessage message)
        {
            if (!(message is MapVoteStateMessage state))
            {
                return false;
            }

            if (_disposed)
            {
                return true;
            }

            if (!_vote.Apply(state))
            {
                _log(MapVoteLog.ClientMalformed(state.CandidateCount));
            }

            Show();
            return true;
        }

        public void Apply(SnapshotMessage snapshot)
        {
        }

        public void Tick(float deltaSeconds)
        {
            if (!_disposed)
            {
                _vote.Tick(deltaSeconds);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _vote.Reset();
            Withdraw();
        }

        private void Cast(int candidateIndex)
        {
            if (_disposed)
            {
                _log(MapVoteLog.ClientRequested(candidateIndex, null, MapVoteCast.Closed, false));
                return;
            }

            var candidates = _vote.Status.HasValue ? _vote.Status.Value.Candidates : null;
            var mapId = candidates != null && candidateIndex >= 0 && candidateIndex < candidates.Count ? candidates[candidateIndex].MapId : null;
            var result = _vote.Vote(candidateIndex, out var sent);
            _log(MapVoteLog.ClientRequested(candidateIndex, mapId, result, sent));
            if (sent)
            {
                Show();
            }
        }

        // 決まった後はシーンを読み替えるだけで選び直せないので、投票の様子も送る口も残さない
        private void Show()
        {
            if (!_vote.IsOpen)
            {
                Withdraw();
                return;
            }

            var status = _vote.Status;
            if (!_published.HasValue || !_published.Value.Equals(status.Value))
            {
                _published = status;
                _log(MapVoteLog.ClientChanged(status));
                GameSignals.MapVote.Publish(status);
            }

            if (!_senderPublished)
            {
                _senderPublished = true;
                GameSignals.MapVoteSender.Publish(_sender);
            }
        }

        // 他の側が後から出した値まで消さないよう、自分が出した値のときだけ消す
        private void Withdraw()
        {
            if (_published.HasValue)
            {
                var previous = _published;
                _published = null;
                _log(MapVoteLog.ClientChanged(null));
                GameSignals.MapVote.ClearIfCurrent(previous);
            }

            if (_senderPublished)
            {
                _senderPublished = false;
                GameSignals.MapVoteSender.ClearIfCurrent(_sender);
            }
        }

        // 部品そのものを出すと UI から Dispose や Handle まで呼べてしまうので、票を送る口だけを渡す
        private sealed class Sender : IMapVoteSender
        {
            private readonly NetV2MapVotePart _part;

            public Sender(NetV2MapVotePart part)
            {
                _part = part;
            }

            public void Vote(int candidateIndex)
            {
                _part.Cast(candidateIndex);
            }
        }
    }
}
