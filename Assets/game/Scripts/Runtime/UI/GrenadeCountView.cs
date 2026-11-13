using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 残りのグレネードの数 (<see cref="HudFormat.Grenades"/>) を、弾数の上に出す。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// <see cref="Hud.AddViews"/> では付けず、オフラインの <see cref="GameBootstrap"/> がプレイヤーの <see cref="GrenadeThrower"/> を
    /// <see cref="Show"/> する (ネット対戦にはまだグレネードが無い)。ルールでグレネードを使わない間は隠す。
    /// HUD の量では弾数と同じ扱いで、弾数を出さない量 (<see cref="HudVisibility.Ammo"/> が false) では隠す (<see cref="Hud"/> が決める)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrenadeCountView : HudElementView
    {
        /// <summary>Hud.uxml の残りのグレネードの数の Label。</summary>
        public const string UxmlName = "grenade-count";

        private GrenadeThrower _source;

        public override string ElementName => UxmlName;

        /// <summary>出しているグレネード。出していなければ null。</summary>
        public GrenadeThrower Source => _source;

        /// <summary>出している文字列。グレネードを使わない間は空文字。</summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>
        /// hud に (無ければ) このビューを付けて返す。付けたばかりなら Hud に木から要素を渡し直させる。
        /// UXML に要素が無ければエラーをログに出し、要素を持たないまま返す (文字列は <see cref="Text"/> で読める)。
        /// </summary>
        public static GrenadeCountView AttachTo(Hud hud)
        {
            if (hud == null)
            {
                throw new ArgumentNullException(nameof(hud));
            }

            var view = hud.GetComponent<GrenadeCountView>();
            if (view != null)
            {
                return view;
            }

            view = hud.gameObject.AddComponent<GrenadeCountView>();
            try
            {
                hud.RebuildView();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{Hud.LogTag} HUD にグレネードの数の欄を出せません: {e.Message}", hud);
            }

            return view;
        }

        /// <summary>thrower の残りを出し始める。前に出していたものは手放す。null なら出すのをやめる。</summary>
        public void Show(GrenadeThrower thrower)
        {
            if (_source != null)
            {
                _source.Changed -= OnChanged;
            }

            _source = thrower;
            if (_source != null)
            {
                _source.Changed += OnChanged;
            }

            Refresh();
        }

        /// <summary>出しているグレネードの今の残りを読み直して Label に写す。</summary>
        public void Refresh()
        {
            var shown = _source != null && _source.UsesGrenades;
            Text = shown ? HudFormat.Grenades(_source.Remaining) : string.Empty;

            if (Element is Label label)
            {
                label.text = Text;
            }

            if (Element != null)
            {
                Element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        protected override void OnBound(VisualElement element)
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            element.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            if (_source != null)
            {
                _source.Changed -= OnChanged;
                _source = null;
            }
        }

        private void OnChanged(GrenadeThrower thrower)
        {
            Refresh();
        }
    }
}
