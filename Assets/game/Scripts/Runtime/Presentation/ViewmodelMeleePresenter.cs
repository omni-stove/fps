using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 振りの見た目だけを受け持ち、音・当たりの手応えはゲームが自分の IMeleePresenter に差し替えて足す。
    [DisallowMultipleComponent]
    public sealed class ViewmodelMeleePresenter : MonoBehaviour, IMeleePresenter
    {
        [SerializeField, Tooltip("振りを見せるビューモデルの動き。空なら同じ物の ViewmodelMotion")]
        private ViewmodelMotion _motion;

        public ViewmodelMotion Motion
        {
            get => _motion != null ? _motion : (_motion = GetComponent<ViewmodelMotion>());
            set => _motion = value;
        }

        public void OnSwingStarted(GameObject attacker, MeleeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var motion = Motion;
            if (motion == null || !motion.enabled)
            {
                return;
            }

            motion.Swing(spec.SwingSeconds, spec.ImpactSeconds);
        }

        public void OnHit(GameObject attacker, Damageable target, Vector3 point, Vector3 direction, int damage)
        {
        }

        public void OnMissed(GameObject attacker)
        {
        }
    }
}
