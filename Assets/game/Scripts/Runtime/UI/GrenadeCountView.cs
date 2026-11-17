using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 残りのグレネードの数 (<see cref="HudFormat.Grenades(int)"/>) を、弾数の上に出す。種類を切り替えて持っているときは、投げる種類の名前も添える。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// <see cref="Hud.AddViews"/> が最初から付け、出すものが来るまで隠しておく。要素を持っている間は <see cref="GameSignals.LocalPlayer"/> を購読し、
    /// 自分のプレイヤーの <see cref="GrenadeThrower"/> の残りを出し、プレイヤーが消されたら隠す。要素を受けたときは今のプレイヤーに合わせる。
    /// ネット対戦の HUD (<c>Fps.NetV2.NetV2Hud</c>) はサーバーから届いた数を <see cref="ShowCount"/> する。
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
        private LocalizedText _typeName;
        private bool _subscribed;

        // 出しているグレネードが GameSignals から来たか。来ていれば、要素を持っていない間にプレイヤーが消されたことを、受けたときに隠して合わせる
        private bool _showsSignal;

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
            _showsSignal = false;
            Watch(thrower);
        }

        private void Watch(GrenadeThrower thrower)
        {
            if (!ReferenceEquals(_source, null))
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
            ShowCount(usesGrenades, default, remaining);
        }

        /// <summary>
        /// <see cref="ShowCount(bool, int)"/> と同じく出し、typeName が空でなければ投げる種類の名前を添える。
        /// </summary>
        public void ShowCount(bool usesGrenades, LocalizedText typeName, int remaining)
        {
            _showsSignal = false;
            if (!ReferenceEquals(_source, null))
            {
                _source.Changed -= OnChanged;
                _source = null;
            }

            _countShown = usesGrenades;
            _count = Math.Max(0, remaining);
            _typeName = typeName;
            Refresh();
        }

        /// <summary>出しているグレネードの今の残りを読み直して Label に写す。</summary>
        public void Refresh()
        {
            bool shown;
            int remaining;
            LocalizedText typeName;
            if (_source != null)
            {
                shown = _source.UsesGrenades;
                remaining = _source.Remaining;

                // 1 種類しか持たないなら、種類を切り替えて持つ前と同じ表示にする
                var inventory = _source.Inventory;
                typeName = inventory.Count > 1 ? inventory.Selected.Name : default;
            }
            else
            {
                shown = _countShown;
                remaining = _count;
                typeName = _typeName;
            }

            var text = typeName.IsEmpty ? HudFormat.Grenades(remaining) : HudFormat.Grenades(typeName, remaining);
            Text = shown ? Localizer.Resolve(text) : string.Empty;

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
            Subscribe();
            var player = GameSignals.LocalPlayer.Value;
            if (player != null || _showsSignal)
            {
                ShowPlayer(player);
                return;
            }

            Refresh();
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (!ReferenceEquals(_source, null))
            {
                _source.Changed -= OnChanged;
                _source = null;
            }
        }

        private void ShowPlayer(LocalPlayerSignal player)
        {
            Watch(player?.Grenades);
            _showsSignal = true;
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.LocalPlayer.Changed += HandleLocalPlayerChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
                _subscribed = false;
            }
        }

        private void HandleLocalPlayerChanged(LocalPlayerSignal player)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
                return;
            }

            // ShowCount で出している数は、プレイヤーが消されたことでは隠さない
            if (player == null && !_showsSignal)
            {
                return;
            }

            ShowPlayer(player);
        }

        private void OnChanged(GrenadeThrower thrower)
        {
            Refresh();
        }
    }
}
