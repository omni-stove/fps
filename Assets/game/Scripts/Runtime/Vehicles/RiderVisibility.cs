using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    // 隠す前に描いていたものだけを戻すのは、もともと隠れていたもの (他の部品が消していたもの) を勝手に描き始めないため。
    internal sealed class RiderVisibility
    {
        private readonly List<Renderer> _hidden = new List<Renderer>();

        public bool IsHiding => _hidden.Count > 0;

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
