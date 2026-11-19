using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // 1 度に 1 件だけ数える。並べて数えると、同じ人たちが何人も一度に外せてしまうため。
    // 票を入れられる人は言い出したときに試合にいた人 (対象と Bot を除く) に限る。途中から入った人まで数えると、入り直しや別アカウントで票を足せるため。
    public sealed class VoteKick
    {
        // float の積 (0.6f × 5 = 3.0000001) をそのまま切り上げると 1 票多く要ることになるので、わずかに引いてから切り上げる。
        private const double _fractionTolerance = 1e-4;

        private readonly HashSet<int> _voters = new HashSet<int>();
        private readonly Dictionary<int, bool> _ballots = new Dictionary<int, bool>();
        private readonly Dictionary<string, double> _nextCallTimes = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<string> _expiredCallers = new List<string>();

        // 何日も動くサーバーで 1 tick ずつ足していくと float では桁が足りず、続けて起こせない秒がずれるので double で数える。
        private double _clock;
        private float _requiredYesFraction = VoteKickSpec.DefaultRequiredYesFraction;

        public bool IsOpen { get; private set; }

        public int TargetId { get; private set; }

        public int CallerId { get; private set; }

        public float RemainingSeconds { get; private set; }

        public int VoterCount => _voters.Count;

        public int YesVotes => CountBallots(true);

        public int NoVotes => CountBallots(false);

        public int RequiredVotes => IsOpen ? RequiredFor(_voters.Count, _requiredYesFraction) : 0;

        public VoteKickResult? Result { get; private set; }

        public VoteKickStatus? Status => IsOpen
            ? new VoteKickStatus(TargetId, CallerId, YesVotes, RequiredVotes, RemainingSeconds)
            : (VoteKickStatus?)null;

        // 票を入れられる人が 0 人でも 1 票は要るとするのは、誰も賛成していない投票を成立させないため。
        public static int RequiredFor(int voterCount, float requiredYesFraction)
        {
            if (voterCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(voterCount), voterCount, "voterCount must not be negative");
            }

            if (!(requiredYesFraction > 0f) || !(requiredYesFraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredYesFraction), requiredYesFraction, "requiredYesFraction must be more than 0 and at most 1");
            }

            var required = (int)Math.Ceiling((voterCount * (double)requiredYesFraction) - _fractionTolerance);
            return Math.Max(1, required);
        }

        public bool HasVoted(int playerId)
        {
            return _ballots.ContainsKey(playerId);
        }

        public bool CanVote(int playerId)
        {
            return IsOpen && _voters.Contains(playerId);
        }

        // 続けて起こせない秒を Id でなく鍵で数えるのは、つなぎ直すと Id が変わり、待たずにまた起こせてしまうため。鍵が無い人だけ Id で数える。
        public float CooldownRemaining(int callerId, string callerKey)
        {
            if (!_nextCallTimes.TryGetValue(CooldownKey(callerId, callerKey), out var next))
            {
                return 0f;
            }

            return (float)Math.Max(0d, next - _clock);
        }

        public VoteKickCall Call(VoteKickSpec spec, int callerId, string callerKey, int targetId, IEnumerable<int> players)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            if (!spec.Enabled)
            {
                return VoteKickCall.Disabled;
            }

            if (IsOpen)
            {
                return VoteKickCall.InProgress;
            }

            if (callerId == targetId)
            {
                return VoteKickCall.Self;
            }

            if (BotRoster.IsBotId(targetId))
            {
                return VoteKickCall.Bot;
            }

            ForgetExpiredCooldowns();
            if (CooldownRemaining(callerId, callerKey) > 0f)
            {
                return VoteKickCall.CoolingDown;
            }

            var hasTarget = false;
            var hasCaller = false;
            var voters = new HashSet<int>();
            foreach (var playerId in players)
            {
                if (BotRoster.IsBotId(playerId))
                {
                    continue;
                }

                if (playerId == targetId)
                {
                    hasTarget = true;
                    continue;
                }

                hasCaller |= playerId == callerId;
                voters.Add(playerId);
            }

            if (!hasTarget)
            {
                return VoteKickCall.TargetNotPresent;
            }

            if (!hasCaller)
            {
                return VoteKickCall.CallerCannotVote;
            }

            _voters.Clear();
            _voters.UnionWith(voters);
            _ballots.Clear();
            _ballots.Add(callerId, true);
            _requiredYesFraction = spec.RequiredYesFraction;
            _nextCallTimes[CooldownKey(callerId, callerKey)] = _clock + spec.CooldownSeconds;
            TargetId = targetId;
            CallerId = callerId;
            RemainingSeconds = spec.VoteSeconds;
            Result = null;
            IsOpen = true;

            // 締め切りを待たずにここで決めるのは、票を入れられる人が言い出した人だけなら、待っても票が増えないため
            Settle();
            return VoteKickCall.Started;
        }

        public VoteKickBallot Vote(int voterId, int targetId, bool inFavor)
        {
            if (!IsOpen)
            {
                return VoteKickBallot.NoVote;
            }

            if (targetId != TargetId)
            {
                return VoteKickBallot.OtherTarget;
            }

            if (!_voters.Contains(voterId))
            {
                return VoteKickBallot.NotVoter;
            }

            VoteKickBallot ballot;
            if (_ballots.TryGetValue(voterId, out var previous))
            {
                if (previous == inFavor)
                {
                    return VoteKickBallot.Unchanged;
                }

                _ballots[voterId] = inFavor;
                ballot = VoteKickBallot.Changed;
            }
            else
            {
                _ballots.Add(voterId, inFavor);
                ballot = VoteKickBallot.Cast;
            }

            Settle();
            return ballot;
        }

        // 抜けた人の票は消し、要る数も残った人数で数え直す。抜けた人の賛成が残ると、いない人の票で外せてしまうため。
        public bool Leave(int playerId)
        {
            if (!IsOpen)
            {
                return false;
            }

            if (playerId == TargetId)
            {
                Close(VoteKickEnd.TargetLeft);
                return true;
            }

            if (!_voters.Remove(playerId))
            {
                return false;
            }

            _ballots.Remove(playerId);
            Settle();
            return true;
        }

        // 締め切った後も時計を進めるのは、続けて起こせない秒を投票の無い間も数えるため。
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            _clock += deltaSeconds;
            if (!IsOpen)
            {
                return false;
            }

            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
            if (RemainingSeconds > 0f)
            {
                return false;
            }

            Close(VoteKickEnd.Expired);
            return true;
        }

        public bool Cancel()
        {
            if (!IsOpen)
            {
                return false;
            }

            Close(VoteKickEnd.Cancelled);
            return true;
        }

        private void Settle()
        {
            var required = RequiredVotes;
            var yes = YesVotes;
            if (yes >= required)
            {
                Close(VoteKickEnd.Passed);
                return;
            }

            var undecided = _voters.Count - _ballots.Count;
            if (yes + undecided < required)
            {
                Close(VoteKickEnd.Defeated);
            }
        }

        private void Close(VoteKickEnd end)
        {
            Result = new VoteKickResult(TargetId, CallerId, YesVotes, NoVotes, RequiredVotes, end);
            IsOpen = false;
            RemainingSeconds = 0f;
            _voters.Clear();
            _ballots.Clear();
        }

        private int CountBallots(bool inFavor)
        {
            var count = 0;
            foreach (var ballot in _ballots.Values)
            {
                if (ballot == inFavor)
                {
                    count++;
                }
            }

            return count;
        }

        // 長く動くサーバーで抜けた人の分が積もらないよう、待ちの済んだ分を言い出しのたびに捨てる
        private void ForgetExpiredCooldowns()
        {
            _expiredCallers.Clear();
            foreach (var pair in _nextCallTimes)
            {
                if (pair.Value <= _clock)
                {
                    _expiredCallers.Add(pair.Key);
                }
            }

            foreach (var key in _expiredCallers)
            {
                _nextCallTimes.Remove(key);
            }
        }

        // 鍵と Id の前に違う印を付けるのは、"7" という鍵を持つ人と Id 7 の鍵の無い人を同じ人と数えないため。
        private static string CooldownKey(int callerId, string callerKey)
        {
            return string.IsNullOrEmpty(callerKey)
                ? "id:" + callerId.ToString(CultureInfo.InvariantCulture)
                : "key:" + callerKey;
        }
    }
}
