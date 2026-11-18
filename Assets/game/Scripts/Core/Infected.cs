using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 生存者と感染者を陣営で分けるのは、ModeSession の得点・味方の判定・出現点の選び方をそのまま使うため。
    public sealed class Infected
    {
        public static readonly Side SurvivorSide = Side.Alpha;

        public static readonly Side InfectedSide = Side.Bravo;

        // 小刻みな Tick を足し合わせると始まる時刻にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        private const long _multiplier = 48271;
        private const long _modulus = 2147483647;

        public event Action<InfectedTurn> Turned;

        private readonly Func<int, int> _pickIndex;
        private readonly List<int> _firstInfected = new List<int>();
        private long _randomState;
        private double _untilOutbreak;
        private bool _brokeOut;

        public InfectedSpec Spec { get; }

        // 参加者の追加とキルの報告をラップせず ModeSession をそのまま見せるのは、シーンの Runner が ModeSession に対して行う形に合わせるため。
        public ModeSession Session { get; }

        public bool HasBrokenOut => _brokeOut;

        public float SecondsUntilOutbreak => _brokeOut ? 0f : (float)Math.Max(0d, _untilOutbreak);

        public IReadOnlyList<int> FirstInfectedIds => _firstInfected;

        public int SurvivorCount => Session.CountMembers(SurvivorSide);

        public int InfectedCount => Session.CountMembers(InfectedSide);

        // pickIndex (0 から count 未満の添字を返す) を渡せるのは、最初の感染者の選び方をテストで決まったものにするため。
        public Infected(InfectedSpec spec, Func<int, int> pickIndex = null)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _randomState = ((Environment.TickCount & int.MaxValue) % (_modulus - 1)) + 1;
            if (pickIndex == null)
            {
                pickIndex = NextIndex;
            }

            _pickIndex = pickIndex;
            _untilOutbreak = spec.StartDelaySeconds;

            var rules = new Rules(this);
            Session = new ModeSession(
                new TwoTeams(),
                spec.RoundSeconds,
                new IScoreRule[] { rules },
                new IndividualRespawn(spec.InfectedRespawnSeconds),
                new IWinCondition[] { rules });
        }

        public bool IsInfected(int playerId)
        {
            RequirePlayer(playerId, nameof(playerId));
            return Session.GetSide(playerId) == InfectedSide;
        }

        // 倒された生存者を感染者にする。キルは先に ModeSession へ報告しておく (感染させた点は報告の時点の陣営で数える)。
        public bool Infect(int playerId, int? infectorId)
        {
            RequirePlayer(playerId, nameof(playerId));
            if (infectorId.HasValue)
            {
                RequirePlayer(infectorId.Value, nameof(infectorId));
            }

            if (!_brokeOut || Session.State != GameModeState.Playing || Session.GetSide(playerId) != SurvivorSide)
            {
                return false;
            }

            Session.ChangeSide(playerId, InfectedSide);
            Turned?.Invoke(new InfectedTurn(playerId, infectorId, false));
            return true;
        }

        public void Reset()
        {
            _brokeOut = false;
            _untilOutbreak = Spec.StartDelaySeconds;
            _firstInfected.Clear();
            foreach (var playerId in Session.GetPlayerIds())
            {
                Session.ChangeSide(playerId, SurvivorSide);
            }

            Session.Reset();
        }

        private void Advance(float seconds)
        {
            if (_brokeOut || !(seconds > 0f))
            {
                return;
            }

            _untilOutbreak -= seconds;
            if (_untilOutbreak <= _snap)
            {
                BreakOut();
            }
        }

        // 生存者を 1 人は残すのは、始まった瞬間に感染者の勝ちで終わらないようにするため。
        private void BreakOut()
        {
            _brokeOut = true;
            _untilOutbreak = 0d;

            var survivors = new List<int>();
            foreach (var playerId in Session.GetPlayerIds())
            {
                if (Session.GetSide(playerId) == SurvivorSide)
                {
                    survivors.Add(playerId);
                }
            }

            var needed = Math.Min(Spec.InitialInfected - InfectedCount, survivors.Count - 1);
            for (var i = 0; i < needed; i++)
            {
                var left = survivors.Count - i;
                var offset = _pickIndex(left);
                if (offset < 0 || offset >= left)
                {
                    throw new InvalidOperationException($"pickIndex returned {offset} for {left} survivor(s); it must be from 0 to {left - 1}");
                }

                var pick = i + offset;
                var swap = survivors[i];
                survivors[i] = survivors[pick];
                survivors[pick] = swap;
                _firstInfected.Add(survivors[i]);
            }

            _firstInfected.Sort();

            // 全員を移してから知らせ、ハンドラが途中の人数を見ないようにする。
            foreach (var playerId in _firstInfected)
            {
                Session.ChangeSide(playerId, InfectedSide);
            }

            foreach (var playerId in _firstInfected)
            {
                Turned?.Invoke(new InfectedTurn(playerId, null, true));

                // ハンドラの中で Reset されたら、もういない最初の感染者を知らせない。
                if (Session.State != GameModeState.Playing)
                {
                    break;
                }
            }
        }

        // System.Random は実装 (Mono と .NET) で列が変わりうるので使わず、MapVote と同じ Lehmer の乗算合同法 (MINSTD) で選ぶ。
        private int NextIndex(int count)
        {
            _randomState = _randomState * _multiplier % _modulus;
            return (int)(_randomState % count);
        }

        private void RequirePlayer(int playerId, string parameterName)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(parameterName, playerId, "no player with this id");
            }
        }

        private sealed class Rules : IScoreRule, IWinCondition
        {
            private readonly Infected _owner;

            public Rules(Infected owner)
            {
                _owner = owner;
            }

            public double? ScoreLimit => null;

            public double PointsForKill(ModeSession session, int killerId, int victimId)
            {
                var infects = _owner._brokeOut
                    && session.GetSide(killerId) == InfectedSide
                    && session.GetSide(victimId) == SurvivorSide;
                return infects ? _owner.Spec.ScorePerInfection : 0d;
            }

            public double PointsForAssist(ModeSession session, int assisterId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side) => 0d;

            public void Advance(ModeSession session, float seconds) => _owner.Advance(seconds);

            public ModeOutcome CheckStanding(ModeSession session)
            {
                return _owner._brokeOut && _owner.InfectedCount > 0 && _owner.SurvivorCount == 0
                    ? ModeOutcome.Win(InfectedSide)
                    : ModeOutcome.Undecided;
            }

            // 生き残った点をここで足すのは、ModeSession が勝敗を決めて終わった後は AddScore が点を足さないため。
            public ModeOutcome CheckTimeUp(ModeSession session)
            {
                if (_owner.SurvivorCount == 0)
                {
                    return ModeOutcome.Win(InfectedSide);
                }

                session.AddScore(SurvivorSide, _owner.Spec.ScorePerSurvivedRound);
                return ModeOutcome.Win(SurvivorSide);
            }
        }
    }
}
