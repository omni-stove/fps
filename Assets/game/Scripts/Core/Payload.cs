using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class Payload
    {
        // 小刻みな Tick を足し合わせると中継点や終点にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        public event Action<int> CheckpointReached;

        private readonly HashSet<int> _nearCart = new HashSet<int>();
        private double _distance;
        private double _idleSeconds;

        public PayloadSpec Spec { get; }

        public PayloadRoute Route { get; }

        // 参加者の追加をラップせず ModeSession をそのまま見せるのは、シーンの Runner が ModeSession に参加者を加える形に合わせるため。
        public ModeSession Session { get; }

        public Side Attackers { get; }

        public Side Defenders { get; }

        public double Distance => _distance;

        public float Progress => Route.FractionAt(_distance);

        public int CheckpointsReached { get; private set; }

        public bool IsDelivered => _distance >= Route.Length;

        public bool IsContested => Pushers > 0 && CountNearCart(Defenders) > 0;

        public bool IsRollingBack { get; private set; }

        // 道のりで縛るのは、守り側が押し返したぶんを中継点の手前より前へ戻さないため。
        public double RollbackFloor => CheckpointsReached > 0 ? Route.CheckpointDistance(CheckpointsReached - 1) : 0d;

        public Payload(PayloadSpec spec, PayloadRoute route, Side attackers, float respawnSeconds)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Route = route ?? throw new ArgumentNullException(nameof(route));
            if (!attackers.IsTeam)
            {
                throw new ArgumentOutOfRangeException(nameof(attackers), attackers, "attackers must be Alpha or Bravo");
            }

            Attackers = attackers;
            Defenders = attackers == Side.Alpha ? Side.Bravo : Side.Alpha;
            Session = new ModeSession(
                new TwoTeams(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new CartRule(this) },
                new IndividualRespawn(respawnSeconds),
                new IWinCondition[] { new DefendersHoldWin(this) });
        }

        public void Reset()
        {
            _distance = 0d;
            _idleSeconds = 0d;
            CheckpointsReached = 0;
            IsRollingBack = false;
            _nearCart.Clear();
            Session.Reset();
        }

        public void SetNearCart(int playerId, bool near)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id");
            }

            if (near)
            {
                _nearCart.Add(playerId);
            }
            else
            {
                _nearCart.Remove(playerId);
            }
        }

        public bool IsNearCart(int playerId)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id");
            }

            return _nearCart.Contains(playerId);
        }

        public int CountNearCart(Side side)
        {
            var count = 0;
            foreach (var playerId in _nearCart)
            {
                if (Session.HasPlayer(playerId) && !Session.IsDown(playerId) && Session.GetSide(playerId) == side)
                {
                    count++;
                }
            }

            return count;
        }

        public int Pushers => CountNearCart(Attackers);

        public float CurrentSpeed
        {
            get
            {
                var pushers = Pushers;
                return pushers > 0 && CountNearCart(Defenders) == 0 ? Spec.SpeedFor(pushers) : 0f;
            }
        }

        private void Advance(float seconds)
        {
            if (!(seconds > 0f))
            {
                return;
            }

            var pushers = Pushers;
            var contested = pushers > 0 && CountNearCart(Defenders) > 0;
            IsRollingBack = false;

            // 攻め側が誰か台車のそばにいる間は、止められていても戻り始めるまでの秒を数え直す。
            if (pushers > 0)
            {
                _idleSeconds = 0d;
                if (!contested)
                {
                    MoveForward(Spec.SpeedFor(pushers) * (double)seconds);
                }

                return;
            }

            var before = _idleSeconds;
            _idleSeconds += seconds;
            var rolling = _idleSeconds - Math.Max(before, Spec.RollbackDelaySeconds);
            if (rolling > 0d && Spec.RollbackSpeed > 0f && _distance > RollbackFloor)
            {
                IsRollingBack = true;
                _distance = Math.Max(RollbackFloor, _distance - (Spec.RollbackSpeed * rolling));
            }
        }

        private void MoveForward(double amount)
        {
            if (!(amount > 0d))
            {
                return;
            }

            var target = Math.Min(Route.Length, _distance + amount);
            while (CheckpointsReached < Route.CheckpointCount && Route.CheckpointDistance(CheckpointsReached) <= target + _snap)
            {
                var checkpoint = CheckpointsReached;
                _distance = Math.Max(_distance, Route.CheckpointDistance(checkpoint));
                CheckpointsReached++;
                Session.ExtendTime(Spec.CheckpointBonusSeconds);
                CheckpointReached?.Invoke(checkpoint);

                // ハンドラの中で Reset されたら、戻した値の上でさらに進めない。
                if (Session.State != GameModeState.Playing)
                {
                    return;
                }
            }

            _distance = Math.Max(_distance, target);
            if (_distance >= Route.Length - _snap)
            {
                _distance = Route.Length;
                Session.Finish(Attackers);
            }
        }

        private sealed class CartRule : IScoreRule
        {
            private readonly Payload _owner;

            public CartRule(Payload owner)
            {
                _owner = owner;
            }

            public double PointsForKill(ModeSession session, int killerId, int victimId) => 0d;

            public double PointsForAssist(ModeSession session, int assisterId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side) => 0d;

            public void Advance(ModeSession session, float seconds) => _owner.Advance(seconds);
        }

        // 終点に届いたときは台車の側から Finish するので、ここでは時間切れだけを決める。
        private sealed class DefendersHoldWin : IWinCondition
        {
            private readonly Payload _owner;

            public DefendersHoldWin(Payload owner)
            {
                _owner = owner;
            }

            public double? ScoreLimit => null;

            public ModeOutcome CheckStanding(ModeSession session) => ModeOutcome.Undecided;

            public ModeOutcome CheckTimeUp(ModeSession session)
            {
                return _owner.IsDelivered ? ModeOutcome.Win(_owner.Attackers) : ModeOutcome.Win(_owner.Defenders);
            }
        }
    }
}
