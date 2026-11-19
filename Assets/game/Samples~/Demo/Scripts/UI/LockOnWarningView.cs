using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LockOnWarningView : HudElementView
    {
        public const string UxmlName = "lock-warning";

        public const string TrackingClass = "lock-warning--tracking";

        public const string LockedClass = "lock-warning--locked";

        public const string DimClass = "lock-warning--dim";

        public const string ArrowClass = "lock-warning__arrow";

        public const string TextClass = "lock-warning__text";

        public const float DefaultBlinkSeconds = 0.25f;

        [SerializeField]
        private float _blinkSeconds = DefaultBlinkSeconds;

        [SerializeField]
        private Texture2D _arrowImage;

        private LockOnWarningStatus? _status;
        private VisualElement _arrow;
        private Label _label;
        private string _text = string.Empty;
        private float _blinkElapsed;
        private bool _dimmed;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public LockOnWarningStatus? Status => _status;

        public bool IsShown => _status.HasValue && _status.Value.IsWarning;

        public string Text => _text;

        public bool IsDimmed => _dimmed;

        public float BlinkSeconds
        {
            get => _blinkSeconds;
            set
            {
                _blinkSeconds = value;
                Apply();
            }
        }

        public Texture2D ArrowImage
        {
            get => _arrowImage;
            set
            {
                _arrowImage = value;
                ApplyArrowImage();
            }
        }

        public void Show(LockOnWarningStatus? status)
        {
            var previousStage = _status.HasValue ? _status.Value.Stage : LockOnWarningStage.None;
            _status = status;
            var stage = status.HasValue ? status.Value.Stage : LockOnWarningStage.None;

            // 段階が変わったら点いた状態から始めるのは、捕捉が始まった瞬間に消えている側で出て見落とされないようにするため。
            if (stage != previousStage)
            {
                _blinkElapsed = 0f;
                _dimmed = false;
            }

            Refresh();
        }

        public void Refresh()
        {
            _text = string.Empty;
            if (IsShown)
            {
                var message = LockOnWarningText.Message(_status.Value);
                _text = message.IsEmpty ? string.Empty : GameText.Resolve(message);
            }

            Apply();
        }

        // 点滅の進みを時間で受け取るのは、Edit Mode のテストで Update を回さずに段を確かめられるようにするため。
        public void Tick(float deltaSeconds)
        {
            if (!IsBlinking())
            {
                if (_dimmed)
                {
                    _dimmed = false;
                    Apply();
                }

                _blinkElapsed = 0f;
                return;
            }

            var step = deltaSeconds > 0f && !float.IsInfinity(deltaSeconds) ? deltaSeconds : 0f;
            _blinkElapsed += step;
            var dimmed = Mathf.FloorToInt(_blinkElapsed / _blinkSeconds) % 2 == 1;
            _blinkElapsed %= _blinkSeconds * 2f;
            if (dimmed != _dimmed)
            {
                _dimmed = dimmed;
                Apply();
            }
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            _arrow = new VisualElement { pickingMode = PickingMode.Ignore };
            _arrow.AddToClassList(ArrowClass);
            _arrow.generateVisualContent += DrawArrow;
            element.Add(_arrow);
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(TextClass);
            element.Add(_label);
            ApplyArrowImage();
            Subscribe();
            Show(GameSignals.LockOnWarning.Value);
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.Clear();
            _arrow = null;
            _label = null;
            element.RemoveFromClassList(TrackingClass);
            element.RemoveFromClassList(LockedClass);
            element.RemoveFromClassList(DimClass);
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

        // ポーズ中も警告が止まって見えないよう、時間の止まらない経過で点滅させる。
        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private bool IsBlinking()
        {
            return IsShown && _status.Value.Stage == LockOnWarningStage.Tracking && _blinkSeconds > 0f;
        }

        private void Apply()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var stage = IsShown ? _status.Value.Stage : LockOnWarningStage.None;
            element.EnableInClassList(TrackingClass, stage == LockOnWarningStage.Tracking);
            element.EnableInClassList(LockedClass, stage == LockOnWarningStage.Locked);
            element.EnableInClassList(DimClass, _dimmed && IsBlinking());

            if (_label != null && _label.text != _text)
            {
                _label.text = _text;
            }

            if (_arrow != null)
            {
                var bearing = IsShown ? _status.Value.BearingDegrees : 0f;
                _arrow.style.rotate = new Rotate(new Angle(bearing, AngleUnit.Degree));
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyArrowImage()
        {
            if (_arrow == null)
            {
                return;
            }

            _arrow.style.backgroundImage = _arrowImage != null ? new StyleBackground(_arrowImage) : new StyleBackground(StyleKeyword.Null);
            _arrow.MarkDirtyRepaint();
        }

        private void DrawArrow(MeshGenerationContext context)
        {
            // 画像があるときは背景に任せ、三角を重ねない。
            if (_arrowImage != null)
            {
                return;
            }

            var rect = context.visualElement.contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var painter = context.painter2D;
            painter.fillColor = context.visualElement.resolvedStyle.color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.center.x, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.center.x, rect.yMax - rect.height * 0.3f));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.LockOnWarning.Changed += HandleLockOnWarningChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.LockOnWarning.Changed -= HandleLockOnWarningChanged;
            _subscribed = false;
        }

        private void HandleLockOnWarningChanged(LockOnWarningStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.LockOnWarning.Changed -= HandleLockOnWarningChanged;
                return;
            }

            Show(status);
        }
    }
}
