using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    /// <summary>
    /// 人が出す分隊の指示 (<see cref="SquadCommandController"/>) を受ける部分。指示は自分の判断や <see cref="SetErrand"/> の用事より優先し、
    /// 判断との合わせ方は <see cref="BotDriver.FollowOrder"/> が決める。
    /// <list type="bullet">
    /// <item>受けたら <c>[Squad] accepted</c> をログに出し、応答を字幕 (<see cref="SubtitleFeed.Say"/>) に出す。</item>
    /// <item>守る指示 (<see cref="OrderHold"/>) の間は遮蔽点へ移らない (守る場所を離れないため)。着いたら <c>[Squad] arrived</c>。</item>
    /// <item>狙う指示 (<see cref="OrderAttack"/>) の間は陣営で目標を選び直さず、指示の敵を目標にする (<c>[Squad] engage</c>)。
    /// 敵が倒れたら指示した人に付いていく指示に移る (<c>[Squad] done ... reason=TargetGone</c>)。</item>
    /// <item>自分が倒れたり置き直されたりしたら指示を捨てる。</item>
    /// </list>
    /// </summary>
    public sealed partial class EnemyAgent
    {
        private BotErrand _order = BotErrand.None;
        private Damageable _orderTarget;
        private SideMember _orderLeader;
        private SquadOrderSpec _orderSpec = SquadOrderSpec.Default;
        private bool _orderArrived;

        /// <summary>今の分隊の指示。無ければ <see cref="BotErrand.None"/>。行き先は狙う敵・付いていく人に合わせて毎フレーム更新する。</summary>
        public BotErrand Order => _order;

        /// <summary>狙う指示の敵。無ければ null。</summary>
        public Damageable OrderTarget => _orderTarget;

        /// <summary>最後に指示を出した人。無ければ null。</summary>
        public SideMember OrderLeader => _orderLeader;

        /// <summary>守る指示の場所に着いたか。</summary>
        public bool HasArrivedForOrder => _orderArrived;

        /// <summary>
        /// place へ行ってそこを守らせる。place は近くの NavMesh に寄せる (無ければそのまま)。倒れているか未初期化なら false。
        /// </summary>
        /// <exception cref="ArgumentNullException">leader か spec が null。</exception>
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

        /// <summary>target を狙わせる。target が倒れているか、自分が倒れているか未初期化なら false。</summary>
        /// <exception cref="ArgumentNullException">target・leader・spec のどれかが null。</exception>
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

        /// <summary>指示を取り消し、leader に付いていかせる。倒れているか未初期化なら false。</summary>
        /// <exception cref="ArgumentNullException">leader か spec が null。</exception>
        public bool OrderFollow(SideMember leader, SquadOrderSpec spec)
        {
            if (!CanTakeOrder(leader, spec))
            {
                return false;
            }

            Accept(BotErrand.Follow(leader.GroundPosition.ToNumerics()), null, leader, spec);
            return true;
        }

        /// <summary>指示を捨て、自分の判断に戻す。指示があれば <c>[Squad] done</c> を reason で出す。</summary>
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
            SubtitleFeed.Say(Id, SquadOrderText.Reply(Id, order.Kind));
        }

        private void ResetOrder()
        {
            _order = BotErrand.None;
            _orderTarget = null;
            _orderArrived = false;
        }

        /// <summary>
        /// 指示の行き先を狙う敵・付いていく人の今の位置に合わせ、狙う指示なら目標をその敵にする。
        /// 狙う指示で目標を決めたら true (陣営で目標を選び直さない)。
        /// </summary>
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

        // 片付けたら指示した人のところへ戻る。指示した人が居なくなっていれば自分の判断に戻る。
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
            SubtitleFeed.Say(Id, SquadOrderText.Line(Id, SquadOrderText.TargetDownKey));
        }

        private void NoteOrderArrival()
        {
            if (_order.Kind != BotErrandKind.Hold || _orderArrived)
            {
                return;
            }

            if (HorizontalDistance(GroundPosition, _order.Destination.ToUnity()) <= EnemyBrain.ArrivalDistance)
            {
                _orderArrived = true;
                Debug.Log(SquadOrderLog.Arrived(Id, _order.Destination), this);
            }
        }
    }
}
