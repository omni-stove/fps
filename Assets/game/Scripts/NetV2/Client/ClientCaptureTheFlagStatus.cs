using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 届いた CTF の状態を、オフラインの CaptureTheFlagRunner が出すのと同じ形の ModeStatus (Detail は CaptureTheFlagStatus) にする。
    // 毎 tick 届くスナップショットのたびに作り直さないよう、値が変わったときだけ作る。
    // スナップショットは先に戻る旗の秒数 (MatchSnapshot.ObjectiveRemainingSeconds) しか運ばないので、両方の旗が落ちているときの後から落ちた旗は、
    // 落ちたのを見た時点の試合の残り時間の差を足して数える。落ちた時点を見ていない (途中で入った) ときは差を 0 とする
    public sealed class ClientCaptureTheFlagStatus
    {
        private const int _flagCount = 2;

        private readonly int _localPlayerId;
        private readonly FlagState[] _states = new FlagState[_flagCount];
        private readonly int?[] _carriers = new int?[_flagCount];
        private readonly float?[] _returnSeconds = new float?[_flagCount];
        private readonly int?[] _droppedAtRemaining = new int?[_flagCount];
        private readonly bool[] _seenDropped = new bool[_flagCount];

        // スナップショットのたびに確保しないよう、読みかけの旗の様子はここに置く
        private readonly bool[] _found = new bool[_flagCount];
        private readonly FlagState[] _nextStates = new FlagState[_flagCount];
        private readonly int?[] _nextCarriers = new int?[_flagCount];

        private bool _known;
        private bool _flagsChanged;
        private bool _hasFlags;
        private int _remaining;
        private int _alpha;
        private int _bravo;
        private Side? _ownSide;
        private bool _ended;
        private Side? _winner;

        public ClientCaptureTheFlagStatus(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        // CTF の試合でないあいだは null
        public ModeStatus Status { get; private set; }

        // Status が変わったら true
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var match = snapshot.Match;
            if (match.Objective != MatchObjective.CaptureTheFlag)
            {
                return Clear();
            }

            var remaining = Math.Max(0, match.RemainingSeconds);
            var hasFlags = ReadFlags(snapshot, remaining);
            var own = snapshot.Players.IndexOf(_localPlayerId);
            var ownSide = own >= 0 ? Side.FromTeam(snapshot.Players[own].Team) : (Side?)null;
            var ended = match.State == GameModeState.Ended;
            var winner = ended && match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null;
            var alpha = Math.Max(0, match.AlphaPoints);
            var bravo = Math.Max(0, match.BravoPoints);

            if (_known && !_flagsChanged && hasFlags == _hasFlags && remaining == _remaining && alpha == _alpha && bravo == _bravo
                && ownSide == _ownSide && ended == _ended && winner == _winner)
            {
                return false;
            }

            _known = true;
            _flagsChanged = false;
            _hasFlags = hasFlags;
            _remaining = remaining;
            _alpha = alpha;
            _bravo = bravo;
            _ownSide = ownSide;
            _ended = ended;
            _winner = winner;

            var a = Side.Alpha.Id;
            var b = Side.Bravo.Id;
            var detail = hasFlags
                ? new CaptureTheFlagStatus(
                    _states[a], _carriers[a], _returnSeconds[a],
                    _states[b], _carriers[b], _returnSeconds[b],
                    _localPlayerId, ownSide)
                : null;

            // ModeStatus は出した後も UI が持つので、得点の並びは使い回さずに毎回作る
            Status = new ModeStatus(
                remaining,
                new[] { new SideScore(Side.Alpha, alpha), new SideScore(Side.Bravo, bravo) },
                ownSide,
                null,
                ended,
                winner,
                detail);
            return true;
        }

        // Status が変わったら true
        public bool Clear()
        {
            for (var i = 0; i < _flagCount; i++)
            {
                _states[i] = FlagState.AtBase;
                _carriers[i] = null;
                _returnSeconds[i] = null;
            }

            ForgetDrops();
            _known = false;
            _flagsChanged = false;
            _hasFlags = false;
            if (Status == null)
            {
                return false;
            }

            Status = null;
            return true;
        }

        // 両方の旗がそろって読めたら true。片方でも欠けたり読めない様子なら、旗の欄 (Detail) を出さない
        private bool ReadFlags(SnapshotMessage snapshot, int matchRemaining)
        {
            for (var i = 0; i < _flagCount; i++)
            {
                _found[i] = false;
            }

            var items = snapshot.ObjectiveItems;
            for (var i = 0; i < items.Count; i++)
            {
                ref var item = ref items[i];
                if (item.Kind != ObjectiveItemKind.Flag || (item.Team != Team.Alpha && item.Team != Team.Bravo))
                {
                    continue;
                }

                var index = Side.FromTeam(item.Team).Id;
                if (_found[index] || !TryToFlagState(item.State, out var state))
                {
                    ForgetDrops();
                    return false;
                }

                _found[index] = true;
                _nextStates[index] = state;
                _nextCarriers[index] = item.HasCarrier ? item.CarrierId : (int?)null;
            }

            for (var i = 0; i < _flagCount; i++)
            {
                if (!_found[i])
                {
                    ForgetDrops();
                    return false;
                }
            }

            for (var i = 0; i < _flagCount; i++)
            {
                var dropped = _nextStates[i] == FlagState.Dropped;
                if (dropped && !_seenDropped[i])
                {
                    // 前のスナップショットで旗の様子を知っていたときだけ、今落ちたと分かる
                    _droppedAtRemaining[i] = _hasFlags ? matchRemaining : (int?)null;
                }
                else if (!dropped)
                {
                    _droppedAtRemaining[i] = null;
                }

                _seenDropped[i] = dropped;
            }

            var soonest = Math.Max(0, snapshot.Match.ObjectiveRemainingSeconds);
            var bothDropped = _nextStates[0] == FlagState.Dropped && _nextStates[1] == FlagState.Dropped;
            for (var i = 0; i < _flagCount; i++)
            {
                float? returns = null;
                if (_nextStates[i] == FlagState.Dropped)
                {
                    var other = 1 - i;
                    var gap = 0;
                    if (bothDropped && _droppedAtRemaining[i].HasValue && _droppedAtRemaining[other].HasValue)
                    {
                        // 残り時間は減っていくので、後から落ちた旗ほど落ちた時点の残り時間が小さい
                        gap = Math.Max(0, _droppedAtRemaining[other].Value - _droppedAtRemaining[i].Value);
                    }

                    returns = soonest + gap;
                }

                if (_nextStates[i] != _states[i] || _nextCarriers[i] != _carriers[i] || returns != _returnSeconds[i])
                {
                    _flagsChanged = true;
                }

                _states[i] = _nextStates[i];
                _carriers[i] = _nextCarriers[i];
                _returnSeconds[i] = returns;
            }

            return true;
        }

        private void ForgetDrops()
        {
            for (var i = 0; i < _flagCount; i++)
            {
                _droppedAtRemaining[i] = null;
                _seenDropped[i] = false;
            }
        }

        private static bool TryToFlagState(ObjectiveItemState state, out FlagState flag)
        {
            switch (state)
            {
                case ObjectiveItemState.Home:
                    flag = FlagState.AtBase;
                    return true;
                case ObjectiveItemState.Carried:
                    flag = FlagState.Carried;
                    return true;
                case ObjectiveItemState.Dropped:
                    flag = FlagState.Dropped;
                    return true;
                default:
                    flag = FlagState.AtBase;
                    return false;
            }
        }
    }
}
