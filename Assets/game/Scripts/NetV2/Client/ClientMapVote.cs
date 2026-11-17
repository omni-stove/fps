using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 自分の票を手元で覚えるのは、MapVoteStateMessage が全員に同じ中身で送られ、誰が何に入れたかを載せないため。
    public sealed class ClientMapVote
    {
        private readonly Func<IMessage, bool> _send;
        private readonly MapVoteRequestMessage _request = new MapVoteRequestMessage();
        private readonly List<MapVoteCandidate> _candidates = new List<MapVoteCandidate>();
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private string _myVote = string.Empty;
        private string _decidedMapId = string.Empty;
        private float _remainingSeconds;

        public ClientMapVote(Func<IMessage, bool> send)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
        }

        public MapVoteStatus? Status { get; private set; }

        public bool IsOpen => Status.HasValue && !Status.Value.IsDecided;

        /// <returns>中身が壊れていて投票を閉じたときは false。</returns>
        public bool Apply(MapVoteStateMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!IsWellFormed(message))
            {
                Reset();
                return false;
            }

            if (message.CandidateCount == 0)
            {
                Reset();
                return true;
            }

            // 前の投票が決まった後や候補が入れ替わった後に届いたものは新しい投票なので、前の票を持ち越さない
            if (!IsOpen || !SameCandidates(message))
            {
                _myVote = string.Empty;
            }

            _candidates.Clear();
            for (var i = 0; i < message.CandidateCount; i++)
            {
                _candidates.Add(new MapVoteCandidate(message.MapId(i), message.Votes(i)));
            }

            _remainingSeconds = message.RemainingSeconds;
            _decidedMapId = message.ChosenIndex == MapVoteStateMessage.NotChosen ? string.Empty : message.MapId(message.ChosenIndex);
            Rebuild();
            return true;
        }

        // 送った時点の残りで出し直すと、手元で数えている表示が届いた時の秒へ巻き戻るので、届いてからの経過を引いておく
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds) || !IsOpen)
            {
                return;
            }

            _remainingSeconds = Math.Max(0f, _remainingSeconds - deltaSeconds);
        }

        /// <param name="candidateIndex"><see cref="MapVoteStatus.Candidates"/> の並びの番号。</param>
        /// <param name="sent">サーバーへ送れたか。送らなかった結果では常に false。</param>
        public MapVoteCast Vote(int candidateIndex, out bool sent)
        {
            sent = false;
            if (!IsOpen)
            {
                return MapVoteCast.Closed;
            }

            // UI が古い一覧の番号で呼ぶことがあるので、例外にせず候補でないとして返す
            if (candidateIndex < 0 || candidateIndex >= _candidates.Count)
            {
                return MapVoteCast.NotCandidate;
            }

            var mapId = _candidates[candidateIndex].MapId;
            if (string.Equals(mapId, _myVote, StringComparison.Ordinal))
            {
                return MapVoteCast.Unchanged;
            }

            _request.MapIndex = candidateIndex;
            sent = _send(_request);
            var result = _myVote.Length == 0 ? MapVoteCast.Cast : MapVoteCast.Changed;
            if (sent)
            {
                _myVote = mapId;
                Rebuild();
            }

            return result;
        }

        public void Reset()
        {
            _candidates.Clear();
            _myVote = string.Empty;
            _decidedMapId = string.Empty;
            _remainingSeconds = 0f;
            Status = null;
        }

        // MapVoteStatus は空の Id や同じ Id の重なりを例外で拒むので、届いた値は先に確かめて投票ごと閉じる
        private bool IsWellFormed(MapVoteStateMessage message)
        {
            var seconds = message.RemainingSeconds;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                return false;
            }

            if (message.ChosenIndex >= message.CandidateCount)
            {
                return false;
            }

            _seen.Clear();
            for (var i = 0; i < message.CandidateCount; i++)
            {
                var mapId = message.MapId(i);
                if (string.IsNullOrWhiteSpace(mapId) || !_seen.Add(mapId))
                {
                    return false;
                }
            }

            return true;
        }

        private bool SameCandidates(MapVoteStateMessage message)
        {
            if (message.CandidateCount != _candidates.Count)
            {
                return false;
            }

            for (var i = 0; i < _candidates.Count; i++)
            {
                if (!string.Equals(message.MapId(i), _candidates[i].MapId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private void Rebuild()
        {
            Status = new MapVoteStatus(_candidates, _myVote, _remainingSeconds, _decidedMapId);
        }
    }
}
