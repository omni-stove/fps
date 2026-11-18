using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    // 塗る前の材質を覚えておき、迷彩を外したら元へ戻す。モデルを作り直さないのは、付けた物とソケットと銃口の炎の位置を失うため (ViewmodelPresenter.HideForSight と同じ)。
    [DisallowMultipleComponent]
    public sealed class CamoPaint : MonoBehaviour
    {
        private readonly Dictionary<Renderer, Material[]> _originals = new Dictionary<Renderer, Material[]>();

        public Material Paint { get; private set; }

        // 同じ材質でも塗り直すのは、付けた物を後から足したとき、その描画にも塗るため。
        public static CamoPaint Apply(GameObject model, Material paint)
        {
            if (model == null)
            {
                return null;
            }

            var painter = model.GetComponent<CamoPaint>();
            if (painter == null)
            {
                if (paint == null)
                {
                    return null;
                }

                painter = model.AddComponent<CamoPaint>();
            }

            painter.Repaint(paint);
            return painter;
        }

        public void Repaint(Material paint)
        {
            Paint = paint;
            Forget();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                // 粒子と線は銃口の炎や弾道なので、武器の地の色と一緒に塗らない
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                {
                    continue;
                }

                if (!_originals.TryGetValue(renderer, out var originals))
                {
                    originals = renderer.sharedMaterials;
                    _originals[renderer] = originals;
                }

                if (paint == null)
                {
                    renderer.sharedMaterials = originals;
                    continue;
                }

                var painted = new Material[originals.Length];
                for (var i = 0; i < painted.Length; i++)
                {
                    painted[i] = paint;
                }

                renderer.sharedMaterials = painted;
            }
        }

        private void Forget()
        {
            var gone = new List<Renderer>();
            foreach (var renderer in _originals.Keys)
            {
                if (renderer == null)
                {
                    gone.Add(renderer);
                }
            }

            foreach (var renderer in gone)
            {
                _originals.Remove(renderer);
            }
        }
    }
}
