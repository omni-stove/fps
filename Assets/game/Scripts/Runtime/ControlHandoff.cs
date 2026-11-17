using System;
using System.Collections.Generic;

namespace Shooter.Runtime
{
    // 宣言したものが破棄された Unity の Object なら、Release し忘れても手放したままにならないよう、その宣言は無かったものとする。
    public sealed class ControlHandoff
    {
        private readonly List<object> _holders = new List<object>(1);

        public bool IsHandedOff
        {
            get
            {
                PruneDestroyed();
                return _holders.Count > 0;
            }
        }

        public object Holder
        {
            get
            {
                PruneDestroyed();
                return _holders.Count > 0 ? _holders[_holders.Count - 1] : null;
            }
        }

        public bool IsHeldBy(object holder)
        {
            if (holder == null)
            {
                throw new ArgumentNullException(nameof(holder));
            }

            PruneDestroyed();
            return _holders.Contains(holder);
        }

        public bool Claim(object holder)
        {
            if (holder == null)
            {
                throw new ArgumentNullException(nameof(holder));
            }

            if (holder is UnityEngine.Object unityObject && unityObject == null)
            {
                throw new ArgumentException("holder has been destroyed", nameof(holder));
            }

            if (_holders.Contains(holder))
            {
                return false;
            }

            _holders.Add(holder);
            return true;
        }

        public bool Release(object holder)
        {
            if (holder == null)
            {
                throw new ArgumentNullException(nameof(holder));
            }

            return _holders.Remove(holder);
        }

        private void PruneDestroyed()
        {
            for (var i = _holders.Count - 1; i >= 0; i--)
            {
                if (_holders[i] is UnityEngine.Object unityObject && unityObject == null)
                {
                    _holders.RemoveAt(i);
                }
            }
        }
    }
}
