using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// HUD の一部を受け持つビューの土台。<see cref="Hud"/> と同じ GameObject に付き、Hud が UIDocument の木を作り直すたびに
    /// <see cref="Bind"/> で Hud.uxml の自分の要素 (<see cref="ElementName"/>) を受け取る。
    /// 何を出すかは派生クラスが決める。
    /// </summary>
    [RequireComponent(typeof(Hud))]
    public abstract class HudElementView : MonoBehaviour
    {
        /// <summary>Hud.uxml の、このビューが使う要素の name。</summary>
        public abstract string ElementName { get; }

        /// <summary>今の木の自分の要素。<see cref="Bind"/> の前か <see cref="Unbind"/> の後は null。</summary>
        public VisualElement Element { get; private set; }

        /// <summary>
        /// root の下から <see cref="ElementName"/> の要素を探して持つ。
        /// 見つからなければ <see cref="InvalidOperationException"/> を投げる (UXML と名前がずれている)。
        /// </summary>
        public void Bind(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var element = root.Q<VisualElement>(ElementName);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"the HUD layout has no element named '{ElementName}' for {GetType().Name}; check Assets/game/UI/Resources/Hud/Hud.uxml");
            }

            Unbind();
            Element = element;
            OnBound(element);
        }

        /// <summary>持っている要素を手放す。持っていなければ何もしない。</summary>
        public void Unbind()
        {
            if (Element == null)
            {
                return;
            }

            OnUnbinding(Element);
            Element = null;
        }

        /// <summary><see cref="Bind"/> で要素を持った直後に呼ばれる。</summary>
        protected virtual void OnBound(VisualElement element)
        {
        }

        /// <summary><see cref="Unbind"/> で要素を手放す直前に呼ばれる。</summary>
        protected virtual void OnUnbinding(VisualElement element)
        {
        }
    }
}
