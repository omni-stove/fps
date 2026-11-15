using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// 射撃の要求に付いたロックオンの相手を、撃った人から見える範囲にいるか確かめる。だめなら誘導しない。
    /// </summary>
    internal sealed class ServerLockOn
    {
        public ServerLockOn(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        /// <summary>
        /// shooterId が round で撃った request のロックオンの相手のうち、誘導してよい相手。いなければ null。
        /// </summary>
        public int? Resolve(int shooterId, RoundSpec round, FireRequestMessage request)
        {
            ArgumentNullException.ThrowIfNull(round);
            ArgumentNullException.ThrowIfNull(request);
            if (round.Homing == null || !request.HasLockTarget)
            {
                return null;
            }

            var targetId = request.LockTargetId;
            var reason = Reject(shooterId, round, request, targetId);
            Combat.Log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net lock-on shooter={1} target={2} {3}",
                ServerCombat.LogTag,
                shooterId,
                targetId,
                reason == null ? "accepted" : "rejected reason=" + reason));
            return reason == null ? targetId : (int?)null;
        }

        [SuppressMessage("Performance", "CA1822", Justification = "Lock checks read the combat state on the instance")]
        public void Tick(float deltaSeconds)
        {
        }

        // 撃った人は少し前の相手を見て捉えているので、射撃の当たりと同じく見ていた tick の位置で確かめる。
        private string Reject(int shooterId, RoundSpec round, FireRequestMessage request, int targetId)
        {
            if (targetId == shooterId)
            {
                return "self";
            }

            if (!Combat.IsHittable(targetId))
            {
                return "not-hittable";
            }

            var tick = new ShotTime(request.ClientTick, request.InterpolationAlpha).ToTick();
            if (!Combat.Hitboxes.TryRewindCenter(targetId, tick, out var point))
            {
                return "unknown";
            }

            var eye = request.Origin;
            var offset = point - eye;
            var distance = offset.Length();
            if (distance > round.Range)
            {
                return "out-of-range";
            }

            if (!LockOnTracker.IsInCone(round.Homing, eye, request.Direction, point))
            {
                return "outside-cone";
            }

            // 人は視線を遮らない (相手の手前にいる別の人越しにも捉えられる) ので、世界と煙だけで確かめる。
            if (distance > 1e-4f
                && (Combat.Context.World.Raycast(eye, offset / distance, distance, ServerHitboxes.RaycastLayers, out _)
                    || Combat.SightBlockers.Blocks(eye, point)))
            {
                return "blocked";
            }

            return null;
        }
    }
}
