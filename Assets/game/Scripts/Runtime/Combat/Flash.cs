using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 長さはサーバー (ServerFlash) と同じ Core の BlindEffect で決め、ここは目の位置と向き、見通せるかだけを Unity の世界から取る。
    public static class Flash
    {
        public const string LogTag = "[Flash]";

        // 頭のてっぺんではなく目の高さから見るよう、体の上端から下げる。
        private const float _eyeInsetFromTop = 0.1f;

        public static int Detonate(Vector3 center, BlindSpec blind, GameObject thrower, string weaponId)
        {
            if (blind == null)
            {
                throw new ArgumentNullException(nameof(blind));
            }

            var blinded = 0;
            foreach (var target in Explosion.FindTargets(center, blind.Radius))
            {
                if (target.IsDead)
                {
                    continue;
                }

                View(target, out var eye, out var facing);
                var seconds = BlindEffect.Seconds(blind, center.ToNumerics(), eye.ToNumerics(), facing.ToNumerics());
                if (!(seconds > 0f))
                {
                    continue;
                }

                if (Explosion.IsLineObstructed(center, eye, target.transform) || EnemySenses.IsVisionBlocked(center, eye))
                {
                    Debug.Log(LogTag + " blocked id=" + target.Id, target);
                    continue;
                }

                target.EnsureStatusEffects().Afflict(BlindEffect.StatusEffect(seconds), thrower, weaponId);
                blinded++;
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} flashed at=({1:0.0}, {2:0.0}, {3:0.0}) blinded={4}",
                LogTag,
                center.x,
                center.y,
                center.z,
                blinded));
            return blinded;
        }

        // 人は体の向きでなく上下も含めたカメラの向きで見ているので、カメラを持つならその向きで測る。
        private static void View(Damageable target, out Vector3 eye, out Vector3 facing)
        {
            var agent = target.GetComponent<EnemyAgent>();
            if (agent != null)
            {
                eye = agent.EyePosition;
                facing = target.transform.forward;
                return;
            }

            eye = target.TryGetBounds(out var bounds)
                ? new Vector3(bounds.center.x, Mathf.Max(bounds.center.y, bounds.max.y - _eyeInsetFromTop), bounds.center.z)
                : target.transform.position;
            var camera = target.GetComponentInChildren<Camera>();
            if (camera == null && target.IsLocalPlayer)
            {
                camera = Camera.main;
            }

            facing = camera != null ? camera.transform.forward : target.transform.forward;
        }
    }
}
