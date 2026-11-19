using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Shooter.Runtime
{
    // 守る指示の間は、守る場所を離れないよう遮蔽点へ移らない。
    public sealed partial class EnemyAgent
    {
        private BotErrand _order = BotErrand.None;
        private Damageable _orderTarget;
        private SideMember _orderLeader;
        private SquadOrderSpec _orderSpec = SquadOrderSpec.Default;
        private bool _orderArrived;
        private BotInputSpec _orderInput = BotInputSpec.Default;
        private VehicleRoute _orderRoute;

        public BotErrand Order => _order;

        public Damageable OrderTarget => _orderTarget;

        public SideMember OrderLeader => _orderLeader;

        public bool HasArrivedForOrder => _orderArrived;

        public VehicleRoute OrderedRoute => _orderRoute;

        // 守る・狙う・付いて行く指示と別に持つのは、歩いて動く指示を消さずに、運転席に座ったときに走る道だけを決められるようにするため。
        // null を渡すと、乗り物に付いた VehicleRoute へ戻る。
        public void OrderRoute(VehicleRoute route)
        {
            if (ReferenceEquals(route, _orderRoute))
            {
                return;
            }

            _orderRoute = route;
            Debug.Log(VehicleLog.BotRouteOrdered(Id, route != null ? route.name : null), this);
            RefreshWheelRoute();
        }

        public bool OrderHold(Vector3 place, SideMember leader, SquadOrderSpec spec)
        {
            if (!CanTakeOrder(leader, spec))
            {
                return false;
            }

            var snapped = NavMesh.SamplePosition(place, out var hit, RouteSnapRadius, NavMesh.AllAreas) ? hit.position : place;
            Accept(BotErrand.Hold(snapped.ToNumerics()), null, leader, spec);
            return true;
        }

        public bool OrderAttack(Damageable target, SideMember leader, SquadOrderSpec spec)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!CanTakeOrder(leader, spec) || target.IsDead || target == _self)
            {
                return false;
            }

            Accept(BotErrand.Attack(target.GroundPoint.ToNumerics()), target, leader, spec);
            return true;
        }

        public bool OrderFollow(SideMember leader, SquadOrderSpec spec)
        {
            if (!CanTakeOrder(leader, spec))
            {
                return false;
            }

            Accept(BotErrand.Follow(leader.GroundPosition.ToNumerics()), null, leader, spec);
            return true;
        }

        public void ClearOrder(string reason)
        {
            if (!_order.IsOrder)
            {
                return;
            }

            var kind = _order.Kind;
            ResetOrder();
            _orderLeader = null;
            Debug.Log(SquadOrderLog.Done(Id, kind, reason, BotErrandKind.None), this);
        }

        private bool CanTakeOrder(SideMember leader, SquadOrderSpec spec)
        {
            if (leader == null)
            {
                throw new ArgumentNullException(nameof(leader));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return _brain != null && !_brain.IsDead;
        }

        private void Accept(BotErrand order, Damageable target, SideMember leader, SquadOrderSpec spec)
        {
            ResetOrder();
            _order = order;
            _orderTarget = target;
            _orderLeader = leader;
            _orderSpec = spec;
            Debug.Log(SquadOrderLog.Accepted(Id, order.Kind, target != null ? target.Id : null, order.Destination), this);
            SubtitleFeed.Reply(SquadOrderReply.Accepted(Id, order.Kind));
        }

        private void ResetOrder()
        {
            _order = BotErrand.None;
            _orderTarget = null;
            _orderArrived = false;
        }

        private bool TickOrder()
        {
            switch (_order.Kind)
            {
                case BotErrandKind.Attack:
                    if (_orderTarget == null || _orderTarget.IsDead)
                    {
                        FinishAttack();
                        return false;
                    }

                    _order = _order.WithDestination(_orderTarget.GroundPoint.ToNumerics());
                    if (_target != _orderTarget)
                    {
                        _target = _orderTarget;
                        Debug.Log(SquadOrderLog.Engage(Id, _orderTarget.Id), this);
                    }

                    return true;
                case BotErrandKind.Follow:
                    if (_orderLeader == null || !_orderLeader.IsAlive)
                    {
                        ClearOrder("LeaderGone");
                        return false;
                    }

                    _order = _order.WithDestination(_orderLeader.GroundPosition.ToNumerics());
                    return false;
                default:
                    return false;
            }
        }

        private void FinishAttack()
        {
            var leader = _orderLeader;
            ResetOrder();
            if (_target != null && _target.IsDead)
            {
                _target = null;
                _nextTargetSearch = 0f;
            }

            if (leader == null || !leader.IsAlive)
            {
                _orderLeader = null;
                Debug.Log(SquadOrderLog.Done(Id, BotErrandKind.Attack, "TargetGone", BotErrandKind.None), this);
                return;
            }

            _order = BotErrand.Follow(leader.GroundPosition.ToNumerics());
            Debug.Log(SquadOrderLog.Done(Id, BotErrandKind.Attack, "TargetGone", BotErrandKind.Follow), this);
            SubtitleFeed.Reply(SquadOrderReply.TargetDown(Id));
        }

        // 指示の判断は BotInputSpec の到着距離しか使わないので、ほかは既定のまま到着距離だけを敵の設定にそろえる。
        private BotInputSpec OrderInput()
        {
            if (!_orderInput.ArrivalDistance.Equals(_arrivalDistance))
            {
                var d = BotInputSpec.Default;
                _orderInput = new BotInputSpec(
                    d.MaxTurnDegreesPerSecond,
                    d.AimErrorPerMeter,
                    d.AimErrorRefreshSeconds,
                    d.FireToleranceDegrees,
                    _arrivalDistance,
                    d.SprintDistance,
                    d.SprintMaxAngleDegrees,
                    d.CrouchWhenHolding);
            }

            return _orderInput;
        }

        private void NoteOrderArrival()
        {
            if (_order.Kind != BotErrandKind.Hold || _orderArrived)
            {
                return;
            }

            if (HorizontalDistance(GroundPosition, _order.Destination.ToUnity()) <= _arrivalDistance)
            {
                _orderArrived = true;
                Debug.Log(SquadOrderLog.Arrived(Id, _order.Destination), this);
            }
        }
    }
}
