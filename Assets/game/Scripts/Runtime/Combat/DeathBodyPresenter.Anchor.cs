using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class DeathBodyPresenter
    {
        private static readonly List<DeathBodyPresenter> AnchoredPresenters = new List<DeathBodyPresenter>();

        // 車体が爆発の間だけコライダーを切って戻すと IgnoreCollision が消える。戻した後に、その車体へ固定した抜け殻の無視を掛け直す。
        public static void ReapplyAnchoredIgnores(Transform anchor)
        {
            foreach (var presenter in AnchoredPresenters)
            {
                if (presenter != null && presenter._anchor == anchor && presenter.IsAnchored)
                {
                    presenter.IgnoreAnchorCollisions();
                }
            }
        }

        private void IgnoreAnchorCollisions()
        {
            // 座席の抜け殻は車体と重なっている。kinematic の抜け殻は動かないので、重なりを解く力が車体に掛かり続けて揺らす。
            var shellColliders = _shell.GetComponentsInChildren<Collider>(true);
            foreach (var anchorCollider in _anchor.GetComponentsInChildren<Collider>(true))
            {
                if (anchorCollider == null || anchorCollider.transform.IsChildOf(_shell.transform))
                {
                    continue;
                }

                foreach (var shellCollider in shellColliders)
                {
                    if (shellCollider != null)
                    {
                        Physics.IgnoreCollision(anchorCollider, shellCollider, true);
                    }
                }
            }
        }

        private void AnchorShell()
        {
            if (!HasShell || _anchor == null || _shell.transform.parent == _anchor)
            {
                return;
            }

            // 親にする前に、車体のコライダーとの衝突を切る。
            IgnoreAnchorCollisions();
            if (!AnchoredPresenters.Contains(this))
            {
                AnchoredPresenters.Add(this);
            }

            _shell.transform.SetParent(_anchor, true);
            if (_ragdoll != null)
            {
                _ragdoll.Freeze();
            }

            foreach (var body in _shell.GetComponentsInChildren<Rigidbody>())
            {
                // kinematic にしてから速度を書くと Unity が警告を出すので、先に止める。
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                }
            }

            Debug.Log($"{LogTag} anchored id={Id} to={_anchor.name}", this);
        }
    }
}
