using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// HUD の一部を受け持つビューの土台。<see cref="Hud"/> と同じ GameObject に付き、Hud が UIDocument の木を作り直すたびに
    /// <see cref="Bind"/> で Hud.uxml の自分の要素 (<see cref="ElementName"/>) を受け取る。
    /// 何を出すかは派生クラスが決める。持っている間に言語が変わると <see cref="OnLanguageChanged"/> で派生クラスに知らせる。
    /// HUD の量 (<see cref="Fps.Core.HudVisibility"/>) で出さないとき (<see cref="IsAllowed"/> が false) は、要素の visibility を隠す。
    /// display は派生クラスが「今出すものがあるか」で切り替えるので、量による隠し方はそれと混ぜない。
    /// </summary>
    [RequireComponent(typeof(Hud))]
    public abstract class HudElementView : MonoBehaviour
    {
        /// <summary>Hud.uxml の、このビューが使う要素の name。</summary>
        public abstract string ElementName { get; }

        /// <summary>今の木の自分の要素。<see cref="Bind"/> の前か <see cref="Unbind"/> の後は null。</summary>
        public VisualElement Element { get; private set; }

        /// <summary>HUD の量で出してよいか。<see cref="SetAllowed"/> で変える。初めは true。</summary>
        public bool IsAllowed { get; private set; } = true;

        /// <summary>
        /// HUD の量で出してよいかを決める。false なら要素を隠し、<see cref="OnAllowedChanged"/> で派生クラスに出しかけのものを片付けさせる。
        /// 要素を持つ前に呼んでも、<see cref="Bind"/> したときに効く。
        /// </summary>
        public void SetAllowed(bool allowed)
        {
            if (allowed == IsAllowed)
            {
                return;
            }

            IsAllowed = allowed;
            if (Element != null)
            {
                ApplyAllowed(Element);
            }

            OnAllowedChanged(allowed);
        }

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
            ApplyAllowed(element);
            Localizer.Changed += HandleLanguageChanged;
            OnBound(element);
        }

        /// <summary>持っている要素を手放す。持っていなければ何もしない。</summary>
        public void Unbind()
        {
            if (Element == null)
            {
                return;
            }

            Localizer.Changed -= HandleLanguageChanged;
            OnUnbinding(Element);
            Element = null;
        }

        /// <summary>
        /// 要素を持っている間に言語が変わった直後に呼ばれる。コードで入れた文字を今の言語 (<see cref="Localizer"/>) で入れ直す。
        /// </summary>
        protected virtual void OnLanguageChanged()
        {
        }

        private void HandleLanguageChanged(Fps.Core.GameLanguage language)
        {
            // 破棄の後に届いた通知では消えた要素に書かない。
            if (this == null || Element == null)
            {
                return;
            }

            OnLanguageChanged();
        }

        /// <summary><see cref="Bind"/> で要素を持った直後に呼ばれる。</summary>
        protected virtual void OnBound(VisualElement element)
        {
        }

        /// <summary><see cref="Unbind"/> で要素を手放す直前に呼ばれる。</summary>
        protected virtual void OnUnbinding(VisualElement element)
        {
        }

        /// <summary><see cref="SetAllowed"/> で出してよいかが変わった直後に呼ばれる (要素の visibility は直してある)。</summary>
        protected virtual void OnAllowedChanged(bool allowed)
        {
        }

        private void ApplyAllowed(VisualElement element)
        {
            element.style.visibility = IsAllowed ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
