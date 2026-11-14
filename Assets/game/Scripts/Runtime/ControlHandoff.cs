using System;
using System.Collections.Generic;

namespace Fps.Runtime
{
    /// <summary>
    /// 操作を手放す口。乗り物の座席などが「今はこの人が動かす」と <see cref="Claim"/> している間、これを見る部品
    /// (<see cref="PlayerController.Handoff"/>・<see cref="WeaponController.Handoff"/>) はプレイヤーの入力を読まない。
    /// 同時に複数が宣言でき、すべてが <see cref="Release"/> するまで手放したまま。
    /// 宣言したものが破棄された Unity の Object なら、Release し忘れてもその宣言は無かったものとする。
    /// </summary>
    public sealed class ControlHandoff
    {
        private readonly List<object> _holders = new List<object>(1);

        /// <summary>誰かが操作を引き取っているか。</summary>
        public bool IsHandedOff
        {
            get
            {
                PruneDestroyed();
                return _holders.Count > 0;
            }
        }

        /// <summary>最後に引き取ったもの。誰も引き取っていなければ null。</summary>
        public object Holder
        {
            get
            {
                PruneDestroyed();
                return _holders.Count > 0 ? _holders[_holders.Count - 1] : null;
            }
        }

        /// <summary>holder が操作を引き取っているか。</summary>
        public bool IsHeldBy(object holder)
        {
            if (holder == null)
            {
                throw new ArgumentNullException(nameof(holder));
            }

            PruneDestroyed();
            return _holders.Contains(holder);
        }

        /// <summary>holder が操作を引き取る。既に引き取っていれば何もせず false。</summary>
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

        /// <summary>holder が引き取った操作を返す。引き取っていなければ false。</summary>
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
