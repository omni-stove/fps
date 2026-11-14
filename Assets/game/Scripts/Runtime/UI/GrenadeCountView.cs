using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 残りのグレネードの数 (<see cref="HudFormat.Grenades"/>) を、弾数の上に出す。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// <see cref="Hud.AddViews"/> では付けず、オフラインの <see cref="GameBootstrap"/> が <see cref="IHud.AttachGrenadeCount"/> で付けてプレイヤーの <see cref="GrenadeThrower"/> を
    /// <see cref="Show"/> し、ネット対戦の HUD (<c>Fps.Net.NetHud</c>) がサーバーから届いた数を <see cref="ShowCount"/> する。
    /// ルールでグレネードを使わない間は隠す。
    /// HUD の量では弾数と同じ扱いで、弾数を出さない量 (<see cref="HudVisibility.Ammo"/> が false) では隠す (<see cref="Hud"/> が決める)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrenadeCountView : HudElementView, IGrenadeCount
    {
        /// <summary>Hud.uxml の残りのグレネードの数の Label。</summary>
        public const string UxmlName = "grenade-count";

        private GrenadeThrower _source;

        // ShowCount で渡された値。_source があればそちらを出す
        private bool _countShown;
        private int _count;

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
                Debug.LogError($"{Hud.LogTag} could not show the grenade count on the HUD: {e.Message}", hud);
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
            _countShown = false;
            _count = 0;
            if (_source != null)
            {
                _source.Changed += OnChanged;
            }

            Refresh();
        }

        /// <summary>
        /// <see cref="GrenadeThrower"/> を持たない画面 (ネット対戦) で、残りの数を直接出す。usesGrenades が false なら隠す。
        /// 出していた <see cref="GrenadeThrower"/> は手放す。負の数は 0 として出す。
        /// </summary>
        public void ShowCount(bool usesGrenades, int remaining)
        {
            if (_source != null)
            {
                _source.Changed -= OnChanged;
                _source = null;
            }

            _countShown = usesGrenades;
            _count = Math.Max(0, remaining);
            Refresh();
        }

        /// <summary>出しているグレネードの今の残りを読み直して Label に写す。</summary>
        public void Refresh()
        {
            bool shown;
            int remaining;
            if (_source != null)
            {
                shown = _source.UsesGrenades;
                remaining = _source.Remaining;
            }
            else
            {
                shown = _countShown;
                remaining = _count;
            }

            Text = shown ? HudFormat.Grenades(remaining) : string.Empty;

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

        protected override void OnLanguageChanged()
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
