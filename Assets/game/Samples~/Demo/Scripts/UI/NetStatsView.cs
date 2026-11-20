using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 色は style.color に書かず Hud.uss のクラスで替えるのは、ゲームが色をスタイルの差し替えだけで選べるようにするため。
    // 出し入れを自分の状態ではなく ControlSettings.ShowNetStats に持つのは、試合をまたいでも F3 で選んだ表示を保つため。
    // オフラインでは誰も GameSignals.NetStats を出さないので、F3 で出す側にしてあっても行は出ない。
    [DisallowMultipleComponent]
    public sealed class NetStatsView : HudElementView
    {
        public const string UxmlName = "net-stats";

        public const string WarningClass = "net-stats--warning";

        public const string BadClass = "net-stats--bad";

        public const string ToggleActionPath = InputActionPaths.NetStats;

        public const string LogTag = "[NetStats]";

        private NetStatsSignal? _stats;
        private string _text = string.Empty;
        private bool _subscribed;
        private bool _settingsSubscribed;
        private ISettingsSink _settings;
        private ControlSettingsStore _store;
        private bool _storeSet;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _toggleAction;
        private bool _resolvedToggleAction;
        private bool _enabledToggleAction;

        public override string ElementName => UxmlName;

        public NetStatsSignal? Stats => _stats;

        // テストで本物の設定を変えないよう、設定の入れ先を差し替えられる。
        public ISettingsSink Settings
        {
            get => _settings ?? AppliedControlSettings.Sink;
            set
            {
                _settings = value ?? throw new ArgumentNullException(nameof(value));
                Refresh();
            }
        }

        // null にすると F3 の切り替えを保存しない。テストで本物の保存先を書き換えないため。
        public ControlSettingsStore Store
        {
            get => _storeSet ? _store : _store ?? (_store = new ControlSettingsStore());
            set
            {
                _store = value;
                _storeSet = true;
            }
        }

        public bool IsEnabled => Settings.Current.ShowNetStats;

        public bool IsShown => IsEnabled && _stats.HasValue;

        public string Text => _text;

        public NetPingLevel Level => NetStatsFormat.PingLevel(_stats?.PingMs);

        public InputAction ToggleAction => _toggleAction;

        public void Show(NetStatsSignal? stats)
        {
            _stats = stats;
            Refresh();
        }

        public void Toggle()
        {
            SetEnabled(!IsEnabled);
        }

        public void SetEnabled(bool show)
        {
            var settings = Settings;
            var current = settings.Current;
            if (current.ShowNetStats != show)
            {
                settings.Apply(current.WithShowNetStats(show));
                Save(show);
                Debug.Log($"{LogTag} {(show ? "shown" : "hidden")} online={_stats.HasValue}", this);
            }

            Refresh();
        }

        public void Refresh()
        {
            _text = IsShown
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

        private void OnEnable()
        {
            AcquireToggleAction();
            SubscribeSettings();
        }

        private void OnDisable()
        {
            ReleaseToggleAction();
            UnsubscribeSettings();
        }

        private void Update()
        {
            if (_toggleAction != null && _toggleAction.WasPressedThisFrame())
            {
                Toggle();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnsubscribeSettings();
        }

        // 保存先は入れた設定ではなく保存済みの値の旗だけを変えるのは、設定画面で編集中のまだ保存していない値を F3 で保存しないため。
        private void Save(bool show)
        {
            var store = Store;
            if (store == null)
            {
                return;
            }

            try
            {
                store.Save(store.Load().WithShowNetStats(show));
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} could not save the net stats display to '{store.Key}': {e.Message}", this);
                Debug.LogException(e, this);
            }
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

            var shown = IsShown;
            var level = Level;
            element.EnableInClassList(WarningClass, shown && level == NetPingLevel.Warning);
            element.EnableInClassList(BadClass, shown && level == NetPingLevel.Bad);
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
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

        // 設定画面の「初期値に戻す」などで旗が変わったときも行を合わせる。
        private void SubscribeSettings()
        {
            if (_settingsSubscribed)
            {
                return;
            }

            AppliedControlSettings.Changed += HandleSettingsChanged;
            _settingsSubscribed = true;
        }

        private void UnsubscribeSettings()
        {
            if (!_settingsSubscribed)
            {
                return;
            }

            AppliedControlSettings.Changed -= HandleSettingsChanged;
            _settingsSubscribed = false;
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

        private void HandleSettingsChanged(ControlSettings settings)
        {
            if (this == null)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                return;
            }

            Refresh();
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
        private void AcquireToggleAction()
        {
            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ解決して有効にする。
            if (!Application.isPlaying || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            if (!_resolvedToggleAction)
            {
                _toggleAction = ResolveToggleAction();
                _resolvedToggleAction = true;
            }

            if (_toggleAction == null || _toggleAction.enabled)
            {
                return;
            }

            _toggleAction.Enable();
            _enabledToggleAction = true;
        }

        private void ReleaseToggleAction()
        {
            if (_enabledToggleAction && _toggleAction != null)
            {
                _toggleAction.Disable();
            }

            _enabledToggleAction = false;
        }

        private InputAction ResolveToggleAction()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    $"{LogTag} project-wide Input Actions are not set (Project Settings > Input System Package); F3 cannot toggle the net stats",
                    this);
                return null;
            }

            var action = actions.FindAction(ToggleActionPath);
            if (action == null)
            {
                Debug.LogError($"{LogTag} Input Actions '{actions.name}' has no '{ToggleActionPath}'; the net stats cannot be toggled", this);
            }

            return action;
        }
    }
}
