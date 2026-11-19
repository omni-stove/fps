using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 投票の秒と、投票をするかは ServerMatch から読む。試合の枠が次の試合までの待ちを投票の秒より短くしないので、
    // 2 か所で別の値を読むと、待ちより長い投票を始めてしまいうるため。
    internal sealed partial class MatchRulesModule
    {
        // 票の動きが無くても残りの秒をクライアントで数え直さずに済むよう、決まった間隔で送り直す
        private const float _mapVoteSendInterval = 1f;

        private const uint _fnvOffset = 2166136261u;
        private const uint _fnvPrime = 16777619u;

        private readonly MapVoteStateMessage _mapVoteState = new MapVoteStateMessage();

        private MapVote _mapVote;
        private float _mapVoteSendElapsed;
        private float _nextMapVoteRefusalLogTime;

        internal MapVote CurrentMapVote => _mapVote;

        // 投票をしていない間と、試合にいない人の要求は理由も出さずに捨てる (チャット・印と同じ)
        internal MapVoteCast? ServerMapVote(int playerId, int mapIndex)
        {
            if (_mapVote == null || _session == null || !_session.HasPlayer(playerId))
            {
                return null;
            }

            var candidates = _mapVote.Candidates;
            var mapId = mapIndex >= 0 && mapIndex < candidates.Count ? candidates[mapIndex] : null;
            var result = _mapVote.Cast(playerId, mapId);
            switch (result)
            {
                case MapVoteCast.Cast:
                case MapVoteCast.Changed:
                    _log.WriteLine(MapVoteLog.Cast(playerId, mapId, result));
                    _match.WriteMapVote(playerId, mapId);
                    SendMapVoteToAll();
                    break;
                case MapVoteCast.NotCandidate:
                case MapVoteCast.Closed:
                    RefuseMapVote(playerId, mapId, result);
                    break;
            }

            return result;
        }

        partial void OnMapVoteRequest(ServerPlayer player, MapVoteRequestMessage request)
        {
            ServerMapVote(player.PlayerId, request.MapIndex);
        }

        partial void OnMatchEnded(Team? winner)
        {
            OpenMapVote();
        }

        partial void OnNextMatchStarting()
        {
            _mapVote = null;
        }

        partial void OnPlayerJoined(ServerPlayer player)
        {
            if (_mapVote != null)
            {
                SendMapVote(player);
            }
        }

        partial void OnPlayerLeft(ServerPlayer player)
        {
            if (_mapVote == null)
            {
                return;
            }

            var dropped = _mapVote.Leave(player.PlayerId);
            if (dropped.Length == 0)
            {
                return;
            }

            _log.WriteLine(MapVoteLog.Left(player.PlayerId, dropped));
            SendMapVoteToAll();
        }

        private void PrepareMapVote()
        {
            _match.NextMapChoosing += HandleNextMapChoosing;
        }

        private void StopMapVote()
        {
            _match.NextMapChoosing -= HandleNextMapChoosing;
            _mapVote = null;
        }

        private void OpenMapVote()
        {
            _mapVote = null;
            if (!_match.HoldsMapVote || _match.Map == null || _match.State != GameModeState.Ended)
            {
                return;
            }

            var maps = _match.Maps;
            var ids = new string[maps.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = maps[i].Name;
            }

            _mapVote = new MapVote(ids, _match.Map.Name, _match.MapVoteSeconds, SeedOf(_match.MatchId));
            _mapVoteSendElapsed = 0f;
            _log.WriteLine(MapVoteLog.Opened(_mapVote));
            SendMapVoteToAll();
        }

        private void TickMapVote(float deltaSeconds)
        {
            if (_mapVote == null || _mapVote.IsClosed)
            {
                return;
            }

            if (_mapVote.Tick(deltaSeconds))
            {
                DecideMapVote();
                return;
            }

            _mapVoteSendElapsed += deltaSeconds;
            if (_mapVoteSendElapsed >= _mapVoteSendInterval)
            {
                _mapVoteSendElapsed -= _mapVoteSendInterval;
                SendMapVoteToAll();
            }
        }

        // 次の試合が投票の締め切りより先に来たら (待ちの秒を投票の秒と同じにしたときなど)、マップを替える前にここで締め切る
        private void HandleNextMapChoosing()
        {
            if (_mapVote != null && !_mapVote.IsClosed)
            {
                _mapVote.Close();
                DecideMapVote();
            }
        }

        private void DecideMapVote()
        {
            var decision = _mapVote.Decision;
            _log.WriteLine(MapVoteLog.Decided(decision));
            if (!decision.KeepsCurrentMap)
            {
                _match.RequestNextMap(decision.MapId);
            }

            SendMapVoteToAll();
        }

        // 外れた番号を送り続けるクライアントでログを埋めないよう、チャットと同じく間引く
        private void RefuseMapVote(int playerId, string mapId, MapVoteCast result)
        {
            if (_now < _nextMapVoteRefusalLogTime)
            {
                return;
            }

            _nextMapVoteRefusalLogTime = _now + _rejectWarningInterval;
            _log.WriteLine(MapVoteLog.Cast(playerId, mapId, result));
        }

        private void SendMapVoteToAll()
        {
            FillMapVoteState();
            SendToAll(TransportChannel.Reliable, _mapVoteState);
        }

        private void SendMapVote(ServerPlayer player)
        {
            FillMapVoteState();
            _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _mapVoteState);
        }

        private void FillMapVoteState()
        {
            _mapVoteState.ClearCandidates();
            var chosen = MapVoteStateMessage.NotChosen;
            var decided = _mapVote.IsClosed ? _mapVote.Decision.MapId : string.Empty;
            var tally = _mapVote.Tally();
            for (var i = 0; i < tally.Count; i++)
            {
                _mapVoteState.AddCandidate(tally[i].MapId, tally[i].Votes);
                if (decided.Length > 0 && string.Equals(tally[i].MapId, decided, StringComparison.Ordinal))
                {
                    chosen = i;
                }
            }

            _mapVoteState.RemainingSeconds = _mapVote.RemainingSeconds;
            _mapVoteState.ChosenIndex = chosen;

            // 投票はマップだけを決め、モードは次の項目で決まるので、どのモードのマップを選ぶのかを候補と一緒に知らせる。
            // 投票は試合の枠が次の項目へ進む前 (NextMapChoosing) に締め切るので、締め切った後に送る分もまだ次の項目を指す
            _mapVoteState.NextPlaylistEntryName = _match.Playlist?.Upcoming.Name ?? string.Empty;
        }

        // string.GetHashCode はプロセスごとに変わるので使わない。試合の Id から同じ種を作り、ログの Id で引き分けの決め方を追えるようにする
        internal static int SeedOf(string matchId)
        {
            unchecked
            {
                var hash = _fnvOffset;
                foreach (var c in matchId ?? string.Empty)
                {
                    hash = (hash ^ c) * _fnvPrime;
                }

                return (int)hash;
            }
        }
    }
}
