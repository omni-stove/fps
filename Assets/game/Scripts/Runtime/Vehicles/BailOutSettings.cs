using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class BailOutSettings
    {
        [SerializeField, Tooltip("空中で降りたら脱出してパラシュートを開く。切ると高さにかかわらず普通に降りる")]
        private bool _enabled = true;

        [SerializeField, Min(0.01f), Tooltip("乗り物の当たり判定の底から地面までがこの高さ (m) 以上なら、降りる操作と空中で壊れたときに脱出する")]
        private float _minHeight = BailOutSpec.DefaultMinHeight;

        [SerializeField, Range(0f, 1f), Tooltip("脱出した人が引き継ぐ、乗り物の速さの割合")]
        private float _inheritedSpeedFraction = BailOutSpec.DefaultInheritedSpeedFraction;

        [SerializeField, Min(0f), Tooltip("乗り物の当たり判定の上面から、脱出した人の足元をどれだけ上に出すか (m)")]
        private float _exitHeight = BailOutSpec.DefaultExitHeight;

        [SerializeField, Min(0f), Tooltip("上が塞がっているとき、乗り物の当たり判定の横の端からどれだけ離して出すか (m)")]
        private float _exitSideDistance = BailOutSpec.DefaultExitSideDistance;

        public BailOutSpec ToSpec()
        {
            return new BailOutSpec(_minHeight, _inheritedSpeedFraction, _exitHeight, _exitSideDistance, _enabled);
        }

        public void CopyFrom(BailOutSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _enabled = spec.Enabled;
            _minHeight = spec.MinHeight;
            _inheritedSpeedFraction = spec.InheritedSpeedFraction;
            _exitHeight = spec.ExitHeight;
            _exitSideDistance = spec.ExitSideDistance;
        }
    }
}
