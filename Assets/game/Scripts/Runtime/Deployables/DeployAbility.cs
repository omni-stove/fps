using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 押している間は見本を出して狙い、離したら置く。押した時点で置くと、置ける所かを見てから決められない。
    // 回数は押した時点で枠が減らすので、置かずに終えたとき (置けない所で離した・止められた) は返す。
    public sealed class DeployAbility : IAbility
    {
        private readonly DeployAbilityDefinition _definition;
        private readonly DeploySpec _spec;
        private DeployPreview _preview;
        private DeployVerdict? _shown;
        private DeployVerdict? _refusal;
        private bool _placed;

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

            Debug.Log(DeployLog.LimitReached(owner.OwnerId, kind, owner.ActiveCount(kind), _spec.MaxActive), context.Owner);
            return false;
        }

        public void Begin(AbilityContext context)
        {
            _placed = false;
            _shown = null;
            _refusal = null;
            DestroyPreview();
            _preview = _definition.CreatePreview(_spec);
        }

        public bool Tick(AbilityContext context, float deltaSeconds)
        {
            DeploySurveyor.ResolveAim(context.Owner, out var origin, out var direction);
            var verdict = DeploySurveyor.Survey(context.Owner.transform, origin, direction, _spec, out var pose);
            Show(context, verdict, pose);

            if (!context.IsEffective || context.IsHeld)
            {
                return true;
            }

            if (verdict != DeployVerdict.Ok)
            {
                _refusal = verdict;
                return false;
            }

            Place(context, pose);
            return false;
        }

        public void End(AbilityContext context, bool interrupted)
        {
            DestroyPreview();
            if (_placed)
            {
                return;
            }

            context.Slot.Refund();
            var ownerId = DeployOwner.GetOrAdd(context.Owner).OwnerId;
            var charges = context.Slot.Charges;
            var max = context.Slot.Spec.MaxCharges;
            Debug.Log(
                _refusal.HasValue && !interrupted
                    ? DeployLog.Refused(ownerId, _definition.Id, _refusal.Value, charges, max)
                    : DeployLog.Cancelled(ownerId, _definition.Id, charges, max),
                context.Owner);
        }

        // 毎フレーム出すとログが埋まるので、判定が変わったときだけ出す。
        private void Show(AbilityContext context, DeployVerdict verdict, Pose pose)
        {
            if (_preview != null)
            {
                _preview.Show(pose, verdict == DeployVerdict.Ok);
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
                _definition.NotifyPlaced(deployable, context);
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
