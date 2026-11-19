using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 狙う側の LockOnAim でなく狙われる側に置くのは、出す値が「この画面のプレイヤーが」狙われているかで、場にいくつ狙う側がいても 1 つにまとめるため。
    [DisallowMultipleComponent]
    public sealed class LockOnWarningPublisher : MonoBehaviour
    {
        public const string LogTag = "[Combat]";

        private LockOnWarningStatus? _published;
        private LockOnWarningStage _loggedStage;
        private Transform _riderOwner;
        private PlayerVehicleRider _ownersRider;

        // テストや、LocalPlayer を出さない画面が差し込む。null なら GameSignals.LocalPlayer のプレイヤーを使う。
        public Transform Self { get; set; }

        // null なら Self に付いた PlayerVehicleRider を使う。
        public PlayerVehicleRider Rider { get; set; }

        // null なら場で有効な LockOnAim (LockOnAim.Active) を使う。
        public Func<IReadOnlyList<LockOnAim>> Sources { get; set; }

        // 自分の向き (北から時計回りの度)。null なら GameSignals.LocalHeadingDegrees、それも無ければ乗り物か自分の正面を使う。
        public Func<float> HeadingSource { get; set; }

        public LockOnWarningStatus? Published => _published;

        public LockOnWarningStatus? Refresh()
        {
            var self = ResolveSelf();
            var status = self != null ? Evaluate(self, VehicleOf(self)) : null;
            if (!status.HasValue)
            {
                Unpublish();
                Report(null);
                return null;
            }

            _published = status;
            // 同じ中身を出し直さないのは、止まっている間に UI が毎フレーム描き直さないようにするため。
            if (!(GameSignals.LockOnWarning.Value is LockOnWarningStatus current) || current != status.Value)
            {
                GameSignals.LockOnWarning.Publish(status);
            }

            Report(status);
            return status;
        }

        public static bool IsAimedAt(LockOnAim aim, Transform self, Transform vehicle)
        {
            if (aim == null || self == null || aim.Stage == LockOnWarningStage.None)
            {
                return false;
            }

            // 自分の武器や同じ乗り物に載った武器の捕捉は、自分への脅威ではない。
            var shooter = aim.transform;
            if (shooter.IsChildOf(self) || (vehicle != null && shooter.IsChildOf(vehicle)))
            {
                return false;
            }

            var target = aim.Target;
            if (target == null)
            {
                return false;
            }

            var body = target.transform;
            return body.IsChildOf(self) || (vehicle != null && body.IsChildOf(vehicle));
        }

        private LockOnWarningStatus? Evaluate(Transform self, Transform vehicle)
        {
            var sources = Sources != null ? Sources() : LockOnAim.Active;
            if (sources == null)
            {
                return null;
            }

            var origin = self.position;
            LockOnAim best = null;
            var bestProgress = 0f;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < sources.Count; i++)
            {
                var aim = sources[i];
                if (!IsAimedAt(aim, self, vehicle))
                {
                    continue;
                }

                var progress = aim.Progress ?? 0f;
                var distance = (aim.transform.position - origin).sqrMagnitude;
                if (best != null && !IsAhead(aim.Stage, progress, distance, best.Stage, bestProgress, bestDistance))
                {
                    continue;
                }

                best = aim;
                bestProgress = progress;
                bestDistance = distance;
            }

            if (best == null)
            {
                return null;
            }

            var bearing = 0f;
            if (MinimapProjection.TryBearingDegrees(origin.ToNumerics(), best.transform.position.ToNumerics(), out var world))
            {
                bearing = MinimapProjection.RelativeDegrees(Heading(self, vehicle), world);
            }

            return new LockOnWarningStatus(best.Stage, bearing);
        }

        // 段階が進んだ方、同じ段階ならロックに近い方、それも同じなら近くにいる方を先に知らせる。
        private static bool IsAhead(
            LockOnWarningStage stage, float progress, float distance, LockOnWarningStage bestStage, float bestProgress, float bestDistance)
        {
            if (stage != bestStage)
            {
                return stage > bestStage;
            }

            if (!Mathf.Approximately(progress, bestProgress))
            {
                return progress > bestProgress;
            }

            return distance < bestDistance;
        }

        private float Heading(Transform self, Transform vehicle)
        {
            var source = HeadingSource ?? GameSignals.LocalHeadingDegrees.Value;
            if (source != null)
            {
                var degrees = source();
                if (!float.IsNaN(degrees) && !float.IsInfinity(degrees))
                {
                    return degrees;
                }
            }

            var facing = vehicle != null ? vehicle : self;
            return MinimapProjection.TryHeadingDegrees(facing.forward.ToNumerics(), out var heading) ? heading : 0f;
        }

        private Transform ResolveSelf()
        {
            if (Self != null)
            {
                return Self;
            }

            var local = GameSignals.LocalPlayer.Value;
            var player = local != null ? local.Player : null;
            return player != null ? player.transform : null;
        }

        private Transform VehicleOf(Transform self)
        {
            var rider = Rider;
            if (rider == null)
            {
                if (self != _riderOwner || _ownersRider == null)
                {
                    _riderOwner = self;
                    _ownersRider = self.GetComponent<PlayerVehicleRider>();
                }

                rider = _ownersRider;
            }

            var vehicle = rider != null && rider.IsRiding ? rider.Vehicle : null;
            return vehicle != null ? vehicle.transform : null;
        }

        private void Report(LockOnWarningStatus? status)
        {
            var stage = status.HasValue ? status.Value.Stage : LockOnWarningStage.None;
            if (stage == _loggedStage)
            {
                return;
            }

            _loggedStage = stage;
            Debug.Log(LockOnWarningLog.StageChanged(status), this);
        }

        private void OnDisable()
        {
            Unpublish();
            Report(null);
        }

        // 狙う側の LockOnAim が Update で捕捉を進めた後に読むよう、Update でなく LateUpdate にする。
        private void LateUpdate()
        {
            Refresh();
        }

        private void Unpublish()
        {
            if (_published.HasValue)
            {
                // 後から別の部品が出した値までは消さない。
                GameSignals.LockOnWarning.ClearIfCurrent(_published);
            }

            _published = null;
        }
    }
}
