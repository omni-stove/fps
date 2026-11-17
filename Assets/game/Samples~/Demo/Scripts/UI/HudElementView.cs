using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [RequireComponent(typeof(Hud))]
    public abstract class HudElementView : MonoBehaviour
    {
        public abstract string ElementName { get; }

        public VisualElement Element { get; private set; }

        public bool IsAllowed { get; private set; } = true;

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
                    $"the HUD layout has no element named '{ElementName}' for {GetType().Name}; check Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml");
            }

            Unbind();
            Element = element;
            ApplyAllowed(element);
            Localizer.Changed += HandleLanguageChanged;
            OnBound(element);
        }

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

        protected virtual void OnLanguageChanged()
        {
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            // 破棄の後に届いた通知では消えた要素に書かない。
            if (this == null || Element == null)
            {
                return;
            }

            OnLanguageChanged();
        }

        protected virtual void OnBound(VisualElement element)
        {
        }

        protected virtual void OnUnbinding(VisualElement element)
        {
        }

        protected virtual void OnAllowedChanged(bool allowed)
        {
        }

        // display は派生クラスが「今出すものがあるか」で切り替えるので、HUD の量による隠し方はそれと混ぜずに visibility で行う。
        private void ApplyAllowed(VisualElement element)
        {
            element.style.visibility = IsAllowed ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
