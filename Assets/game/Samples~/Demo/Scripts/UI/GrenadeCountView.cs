using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GrenadeCountView : HudElementView
    {
        public const string UxmlName = "grenade-count";

        private GrenadeThrower _source;

        private GrenadeCountStatus? _count;
        private bool _subscribed;

        // 出しているグレネードが GameSignals から来たか。来ていれば、要素を持っていない間にプレイヤーが消されたことを、受けたときに隠して合わせる
        private bool _showsSignal;

        public override string ElementName => UxmlName;

        public GrenadeThrower Source => _source;

        public string Text { get; private set; } = string.Empty;

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

        public void Show(GrenadeThrower thrower)
        {
            _showsSignal = false;
            _count = null;
            Watch(thrower);
        }

        private void Watch(GrenadeThrower thrower)
        {
            if (!ReferenceEquals(_source, null))
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
                typeName = inventory.Count > 1 ? DisplayNames.Of(inventory.Selected) : default;
            }
            else if (_count.HasValue)
            {
                var count = _count.Value;
                shown = count.UsesGrenades;
                remaining = count.Remaining;
                typeName = count.HasType ? TypeNameOf(count.TypeId) : default;
            }
            else
            {
                shown = false;
                remaining = 0;
                typeName = default;
            }

            var text = typeName.IsEmpty ? HudText.Grenades(remaining) : HudText.Grenades(typeName, remaining);
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
            if (GameSignals.GrenadeCount.HasValue)
            {
                ShowStatus(GameSignals.GrenadeCount.Value.Value);
                return;
            }

            _count = null;
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
            _count = null;
            Watch(player?.Grenades);
            _showsSignal = true;
        }

        private void ShowStatus(GrenadeCountStatus status)
        {
            Watch(null);
            _count = status;
            _showsSignal = true;
            Refresh();
        }

        // 種類の名前は調整値の JSON から Id で引く。届いた Id が JSON に無ければ (サーバーとアセットが違う) Id をそのまま出す
        private static LocalizedText TypeNameOf(string typeId)
        {
            return KitTuning.Grenades.TryFind(typeId, out var spec) ? DisplayNames.Of(spec) : LocalizedText.Literal(typeId);
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.LocalPlayer.Changed += HandleLocalPlayerChanged;
                GameSignals.GrenadeCount.Changed += HandleGrenadeCountChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
                GameSignals.GrenadeCount.Changed -= HandleGrenadeCountChanged;
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

            // Show で直に渡されたものはプレイヤーが消されたことでは手放さず、届いた数が出ている間はそちらを出し続ける
            if ((player == null && !_showsSignal) || _count.HasValue)
            {
                return;
            }

            ShowPlayer(player);
        }

        private void HandleGrenadeCountChanged(GrenadeCountStatus? status)
        {
            if (this == null)
            {
                GameSignals.GrenadeCount.Changed -= HandleGrenadeCountChanged;
                return;
            }

            if (status.HasValue)
            {
                ShowStatus(status.Value);
                return;
            }

            if (_count.HasValue)
            {
                ShowPlayer(GameSignals.LocalPlayer.Value);
            }
        }

        private void OnChanged(GrenadeThrower thrower)
        {
            Refresh();
        }
    }
}
