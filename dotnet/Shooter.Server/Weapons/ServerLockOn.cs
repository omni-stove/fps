using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerLockOn
    {
        public const string ReasonSelf = "self";
        public const string ReasonNotHittable = "not-hittable";
        public const string ReasonUnknown = "unknown";
        public const string ReasonOutOfRange = "out-of-range";
        public const string ReasonOutsideCone = "outside-cone";
        public const string ReasonBlocked = "blocked";
        public const string ReasonLured = "lured";

        public ServerLockOn(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        public ServerLockTarget? Resolve(int shooterId, RoundSpec round, FireRequestMessage request)
        {
            ArgumentNullException.ThrowIfNull(round);
            ArgumentNullException.ThrowIfNull(request);
            if (round.Homing == null || !request.HasLockTarget)
            {
                return null;
            }

            var target = new ServerLockTarget(request.LockTargetKind, request.LockTargetId);
            var reason = Reject(shooterId, round, request, target);
            Combat.Log.WriteLine(ServerCountermeasureLog.LockOn(shooterId, target, reason));
            return reason == null ? target : (ServerLockTarget?)null;
        }

        [SuppressMessage("Performance", "CA1822", Justification = "Lock checks read the combat state on the instance")]
        public void Tick(float deltaSeconds)
        {
        }

        // 撃った人は少し前の相手を見て捉えているので、射撃の当たりと同じく見ていた tick の位置で確かめる。
        private string Reject(int shooterId, RoundSpec round, FireRequestMessage request, ServerLockTarget target)
        {
            var tick = new ShotTime(request.ClientTick, request.InterpolationAlpha).ToTick();
            var reason = target.IsVehicle
                ? LocateVehicle(shooterId, target.Id, tick, out var point)
                : LocatePlayer(shooterId, target.Id, tick, out point);
            if (reason != null)
            {
                return reason;
            }

            var eye = request.Origin;
            var offset = point - eye;
            var distance = offset.Length();
            if (distance > round.Range)
            {
                return ReasonOutOfRange;
            }

            if (!LockOnTracker.IsInCone(round.Homing, eye, request.Direction, point))
            {
                return ReasonOutsideCone;
            }

            // 人は視線を遮らない (相手の手前にいる別の人越しにも捉えられる) ので、世界と煙だけで確かめる。
            if (distance > 1e-4f
                && (Combat.Context.World.Raycast(eye, offset / distance, distance, ServerHitboxes.RaycastLayers, out _)
                    || Combat.SightBlockers.Blocks(eye, point)))
            {
                return ReasonBlocked;
            }

            // 狙う向きの近くにフレアがあれば捉えられない (Unity の LockOnTracker が捕捉を外すのと同じ)。
            var decoy = Combat.Vehicles?.DecoyOf(target);
            if (decoy != null && decoy.TryLure(eye, point, out _, out _))
            {
                return ReasonLured;
            }

            return null;
        }

        private string LocatePlayer(int shooterId, int targetId, float tick, out Vector3 point)
        {
            point = default;
            if (targetId == shooterId)
            {
                return ReasonSelf;
            }

            if (!Combat.IsHittable(targetId))
            {
                return ReasonNotHittable;
            }

            return Combat.Hitboxes.TryRewindCenter(targetId, tick, out point) ? null : ReasonUnknown;
        }

        private string LocateVehicle(int shooterId, int vehicle, float tick, out Vector3 point)
        {
            point = default;
            var vehicles = Combat.Vehicles;
            if (vehicles == null || !vehicles.IsLockable(vehicle))
            {
                return ReasonNotHittable;
            }

            // 座席の武器の弾は自分の乗り物に当たらない (ServerVehicles.Raycast) ので、自分の乗り物を追わせない。
            if (vehicles.IsAboard(shooterId, vehicle))
            {
                return ReasonSelf;
            }

            return vehicles.TryRewindCenter(vehicle, tick, out point) ? null : ReasonUnknown;
        }
    }
}
