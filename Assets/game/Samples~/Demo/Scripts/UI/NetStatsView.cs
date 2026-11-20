using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 色は style.color に書かず Hud.uss のクラスで替えるのは、ゲームが色をスタイルの差し替えだけで選べるようにするため。
    [DisallowMultipleComponent]
    public sealed class NetStatsView : HudElementView
    {
        public const string UxmlName = "net-stats";

        public const string WarningClass = "net-stats--warning";

        public const string BadClass = "net-stats--bad";

        private NetStatsSignal? _stats;
        private string _text = string.Empty;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public NetStatsSignal? Stats => _stats;

        public bool IsShown => _stats.HasValue;

        public string Text => _text;

        public NetPingLevel Level => NetStatsFormat.PingLevel(_stats?.PingMs);

        public void Show(NetStatsSignal? stats)
        {
            _stats = stats;
            Refresh();
        }

        public void Refresh()
        {
            _text = _stats.HasValue
                ? Localizer.Resolve(NetStatsFormat.Text(_stats.Value.PingMs, _stats.Value.LossPercent, _stats.Value.ServerTick))
                : string.Empty;
            Write();
        }

        protected override void OnBound(VisualElement element)
        {
            Subscribe();
            Show(GameSignals.NetStats.Value);
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.RemoveFromClassList(WarningClass);
            element.RemoveFromClassList(BadClass);
            if (element is Label label)
            {
                label.text = string.Empty;
            }

            element.style.display = DisplayStyle.None;
        }

        // HUD を組んだ後に足されたときは、HUD が結び直さないので自分で結ぶ。
        private void Start()
        {
            if (Element != null)
            {
                return;
            }

            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root != null && root.childCount > 0)
            {
                Bind(root);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Write()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            if (element is Label label && label.text != _text)
            {
                label.text = _text;
            }

            var level = Level;
            element.EnableInClassList(WarningClass, IsShown && level == NetPingLevel.Warning);
            element.EnableInClassList(BadClass, IsShown && level == NetPingLevel.Bad);
            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.NetStats.Changed += HandleNetStatsChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.NetStats.Changed -= HandleNetStatsChanged;
            _subscribed = false;
        }

        private void HandleNetStatsChanged(NetStatsSignal? stats)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.NetStats.Changed -= HandleNetStatsChanged;
                return;
            }

            Show(stats);
        }
    }
}
