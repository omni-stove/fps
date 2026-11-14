using System.Collections.Generic;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 座席に座っている間だけ描かないものを隠し、降りたら元に戻す。隠す前に描いていたものだけを戻すので、
    /// もともと隠れていたもの (他の部品が消していたもの) を勝手に描き始めない。
    /// </summary>
    internal sealed class RiderVisibility
    {
        private readonly List<Renderer> _hidden = new List<Renderer>();

        public bool IsHiding => _hidden.Count > 0;

        /// <summary>root の配下で描いている Renderer を隠す。前に隠したものは戻さずに足す。root が null なら何もしない。</summary>
        public void Hide(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.enabled && !_hidden.Contains(renderer))
                {
                    renderer.enabled = false;
                    _hidden.Add(renderer);
                }
            }
        }

        /// <summary>隠したものをすべて描き直す。破棄されたものは飛ばす。</summary>
        public void Restore()
        {
            foreach (var renderer in _hidden)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            _hidden.Clear();
        }
    }
}
