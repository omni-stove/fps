using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 押している間は見本を出して狙い、離したら置く。押した時点で置くと、置ける所かを見てから決められない。
    // 回数は押した時点で枠が減らすので、置かずに終えたとき (置けない所で離した・止められた・起爆した) は返す。
    public sealed class DeployAbility : IAbility, IAbilityFollowUp
    {
        private readonly DeployAbilityDefinition _definition;
        private readonly DeploySpec _spec;
        private DeployPreview _preview;
        private DeployVerdict? _shown;
        private DeployVerdict? _refusal;
        private bool _placed;
        private bool _aiming;
        private float _heldSeconds;
        private bool _detonated;
        private bool _overLimit;
        private Collider _surface;

        public DeployAbility(DeployAbilityDefinition definition, DeploySpec spec)
        {
            _definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public DeploySpec Spec => _spec;

        public DeployPreview Preview => _preview;

        public DeployVerdict? Verdict => _shown;

        public bool CanUse(AbilityContext context)
        {
            var owner = DeployOwner.GetOrAdd(context.Owner);
            var kind = _definition.Id;
            if (owner.CanPlace(kind, _spec))
            {
                return true;
            }

            // 置ける数まで置いていても、短く押して起爆できるようにする。
            if (CanDetonate(owner))
            {
                return true;
            }

            Debug.Log(DeployLog.LimitReached(owner.OwnerId, kind, owner.ActiveCount(kind), _spec.MaxActive), context.Owner);
            return false;
        }

        public void Begin(AbilityContext context)
        {
            _placed = false;
            _shown = null;
            _refusal = null;
            _detonated = false;
            _overLimit = false;
            _surface = null;
            _heldSeconds = 0f;
            _aiming = !CanDetonate(DeployOwner.GetOrAdd(context.Owner));
            DestroyPreview();
            _preview = _definition.CreatePreview(_spec);

            if (!_aiming && _preview != null)
            {
                _preview.Hide();
            }
        }

        public bool Tick(AbilityContext context, float deltaSeconds)
        {
            if (!_aiming)
            {
                var step = DeployReuse.Decide(context.IsHeld, _heldSeconds, _definition.DetonateTapSeconds);
                if (step == DeployReuseStep.Detonate)
                {
                    Detonate(context);
                    return false;
                }

                if (step == DeployReuseStep.Wait)
                {
                    _heldSeconds += deltaSeconds;
                    return true;
                }

                _aiming = true;
            }

            DeploySurveyor.ResolveAim(context.Owner, out var origin, out var direction);
            var verdict = _definition.Survey(context.Owner.transform, origin, direction, _spec, out var pose, out var surface);
            var room = DeployOwner.GetOrAdd(context.Owner).CanPlace(_definition.Id, _spec);
            Show(context, verdict, room, pose);

            if (!context.IsEffective || context.IsHeld)
            {
                return true;
            }

            if (verdict != DeployVerdict.Ok)
            {
                _refusal = verdict;
                return false;
            }

            if (!room)
            {
                _overLimit = true;
                return false;
            }

            _surface = surface;
            Place(context, pose);
            return false;
        }

        // 回数を使い切った・待ち時間の間でも、置いた物があれば押してすぐ起爆する (置けないので狙う必要がない)。
        public bool TryFollowUp(AbilityContext context)
        {
            if (!CanDetonate(DeployOwner.GetOrAdd(context.Owner)))
            {
                return false;
            }

            Detonate(context);
            return true;
        }

        public bool TryGetFollowUp(AbilityContext context, out AbilityFollowUp followUp)
        {
            followUp = default;
            var owner = context.Owner.GetComponent<DeployOwner>();
            if (owner == null || !CanDetonate(owner))
            {
                return false;
            }

            followUp = AbilityFollowUp.Detonate(_definition.Id, _definition.DisplayNameKey, owner.ActiveCount(_definition.Id));
            return true;
        }

        public void End(AbilityContext context, bool interrupted)
        {
            DestroyPreview();
            if (_placed)
            {
                return;
            }

            context.Slot.Refund();
            if (_detonated)
            {
                return;
            }

            var owner = DeployOwner.GetOrAdd(context.Owner);
            var ownerId = owner.OwnerId;
            var charges = context.Slot.Charges;
            var max = context.Slot.Spec.MaxCharges;
            if (_overLimit && !interrupted)
            {
                Debug.Log(DeployLog.LimitReached(ownerId, _definition.Id, owner.ActiveCount(_definition.Id), _spec.MaxActive), context.Owner);
                return;
            }

            Debug.Log(
                _refusal.HasValue && !interrupted
                    ? DeployLog.Refused(ownerId, _definition.Id, _refusal.Value, charges, max)
                    : DeployLog.Cancelled(ownerId, _definition.Id, charges, max),
                context.Owner);
        }

        private bool CanDetonate(DeployOwner owner)
        {
            return _definition.Detonates && owner.ActiveCount(_definition.Id) > 0;
        }

        // 起爆で消えると持ち主の一覧が縮むので、写してから渡す。
        private void Detonate(AbilityContext context)
        {
            var placed = new List<Deployable>(DeployOwner.GetOrAdd(context.Owner).Active(_definition.Id));
            _definition.Detonate(placed, context);
            _detonated = true;
        }

        // 毎フレーム出すとログが埋まるので、判定が変わったときだけ出す。
        private void Show(AbilityContext context, DeployVerdict verdict, bool room, Pose pose)
        {
            if (_preview != null)
            {
                _preview.Show(pose, verdict == DeployVerdict.Ok && room);
            }

            if (_shown != verdict)
            {
                _shown = verdict;
                Debug.Log(DeployLog.Preview(DeployOwner.GetOrAdd(context.Owner).OwnerId, _definition.Id, verdict), context.Owner);
            }
        }

        private void Place(AbilityContext context, Pose pose)
        {
            var owner = DeployOwner.GetOrAdd(context.Owner);
            var kind = _definition.Id;
            var deployable = _definition.CreateDeployed(pose, _spec);
            try
            {
                var member = SideMember.Of(context.Owner);
                deployable.Initialize(context.Owner, owner.OwnerId, kind, owner.NextSerial(), member != null ? member.Side : (Side?)null, _spec.MaxHealth);
                owner.Add(kind, _spec, deployable);
            }
            catch
            {
                DestroyObject(deployable.gameObject);
                throw;
            }

            // 持ち主の一覧に入った後なので、Remove を通して一覧からも外す (Edit Mode では OnDestroy が呼ばれず、壊すだけでは一覧に残る)。
            try
            {
                _definition.NotifyPlaced(deployable, context, _surface);
            }
            catch
            {
                deployable.Remove(DeployRemoval.Discarded);
                throw;
            }

            _placed = true;
            Debug.Log(
                DeployLog.Placed(owner.OwnerId, kind, deployable.Serial, pose.position.ToNumerics(), owner.ActiveCount(kind), _spec.MaxActive),
                deployable);
        }

        private void DestroyPreview()
        {
            if (_preview == null)
            {
                _preview = null;
                return;
            }

            var preview = _preview.gameObject;
            _preview = null;
            DestroyObject(preview);
        }

        // Edit Mode のテストからも呼ばれるので、Play 中でなければその場で壊す (Destroy は Play 中にしか使えない)。
        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
