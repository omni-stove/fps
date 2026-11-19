using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // GameSignals.Drone に値がある間 (置いたドローンが飛んでいる間) だけ出す。
    [DisallowMultipleComponent]
    public sealed class DroneHudView : HudElementView
    {
        public const string UxmlName = "drone-hud";

        public const string BatteryName = "drone-battery";

        public const string OutOfRangeName = "drone-out-of-range";

        public const string RecallName = "drone-recall";

        public const string LineClass = "drone-hud__line";

        public const string WarningClass = "drone-hud__warning";

        public const string PilotingClass = "drone-hud--piloting";

        [SerializeField, Range(0f, 1f), Tooltip("Show the out-of-range warning when the remaining reach is at or below this fraction")]
        private float _rangeWarningFraction = DroneHudText.DefaultRangeWarningFraction;

        [SerializeField, Tooltip("Action name shown in the recall hint (under Player in Input Actions). Use the button of the ability slot that places the drone")]
        private string _recallActionName = DroneHudText.DefaultRecallActionName;

        private DroneStatus? _status;
        private Label _battery;
        private Label _outOfRange;
        private Label _recall;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public float RangeWarningFraction
        {
            get => _rangeWarningFraction;
            set
            {
                _rangeWarningFraction = Mathf.Clamp01(value);
                Refresh();
            }
        }

        public string RecallActionName
        {
            get => _recallActionName;
            set
            {
                _recallActionName = value;
                Refresh();
            }
        }

        public DroneStatus? Status => _status;

        public bool IsShown => _status.HasValue;

        public string BatteryText => _battery != null ? _battery.text : string.Empty;

        public bool ShowsOutOfRange => _status.HasValue && DroneHudText.IsRangeWarning(_status.Value, _rangeWarningFraction);

        public string OutOfRangeText => _outOfRange != null && ShowsOutOfRange ? _outOfRange.text : string.Empty;

        public bool ShowsRecall => _status.HasValue && DroneHudText.ShowsRecall(_status.Value);

        public string RecallText => _recall != null && ShowsRecall ? _recall.text : string.Empty;

        public void Show(DroneStatus? status)
        {
            _status = status;
            Refresh();
        }

        public void Refresh()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            if (!_status.HasValue)
            {
                element.style.display = DisplayStyle.None;
                element.RemoveFromClassList(PilotingClass);
                Write(_battery, string.Empty, false);
                Write(_outOfRange, string.Empty, false);
                Write(_recall, string.Empty, false);
                return;
            }

            var status = _status.Value;
            Write(_battery, Localizer.Resolve(DroneHudText.Battery(status)), true);
            var warning = ShowsOutOfRange;
            Write(_outOfRange, warning ? Localizer.Resolve(DroneHudText.OutOfRange()) : string.Empty, warning);
            var recall = ShowsRecall;
            Write(_recall, recall ? Localizer.Resolve(DroneHudText.Recall(InputActionLabels.LabelOf(_recallActionName))) : string.Empty, recall);
            element.EnableInClassList(PilotingClass, status.Piloting);
            element.style.display = DisplayStyle.Flex;
        }

        protected override void OnBound(VisualElement element)
        {
            _battery = Line(element, BatteryName, false);
            _outOfRange = Line(element, OutOfRangeName, true);
            _recall = Line(element, RecallName, false);
            Subscribe();
            Show(GameSignals.Drone.Value);
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.RemoveFromClassList(PilotingClass);
            element.style.display = DisplayStyle.None;
            _battery?.RemoveFromHierarchy();
            _outOfRange?.RemoveFromHierarchy();
            _recall?.RemoveFromHierarchy();
            _battery = null;
            _outOfRange = null;
            _recall = null;
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

        // レイアウトに置いた行があればそれを使い、無ければ作る。Hud.uxml の置き場所は空の枠だけを持つ。
        private static Label Line(VisualElement element, string name, bool warning)
        {
            var label = element.Q<Label>(name);
            if (label != null)
            {
                return label;
            }

            label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.AddToClassList(LineClass);
            if (warning)
            {
                label.AddToClassList(WarningClass);
            }

            element.Add(label);
            return label;
        }

        private static void Write(Label label, string text, bool shown)
        {
            if (label == null)
            {
                return;
            }

            if (label.text != text)
            {
                label.text = text;
            }

            label.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.Drone.Changed += HandleDroneChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.Drone.Changed -= HandleDroneChanged;
            _subscribed = false;
        }

        private void HandleDroneChanged(DroneStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Drone.Changed -= HandleDroneChanged;
                return;
            }

            Show(status);
        }
    }
}
