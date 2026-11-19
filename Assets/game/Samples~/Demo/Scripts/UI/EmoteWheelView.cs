using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class EmoteWheelView : HudElementView
    {
        public const string UxmlName = "emote-wheel";

        public const string LogTag = "[EmoteWheel]";

        public const string HoldActionPath = InputActionPaths.Emote;

        public const string NameKeyPrefix = "emote.name.";

        public const string CancelKey = "emote.wheel.cancel";

        public const string SlotClass = "emote-wheel__slot";

        public const string SelectedSlotClass = "emote-wheel__slot--selected";

        public const string SlotNameClass = "emote-wheel__slot-name";

        public const string CenterClass = "emote-wheel__center";

        public const float DefaultHoldSeconds = 0.25f;

        public const float DefaultDeadZone = 0.35f;

        public const float DefaultPointerRadius = 120f;

        // 枠の位置を USS で書かずここで持つのは、枠の数がエモートの数で変わるため。WheelHalfSize は Hud.uss の .emote-wheel の幅の半分と揃える。
        public const float WheelHalfSize = 210f;

        public const float SlotRadius = 150f;

        // 開いている間に視点が回ったり撃ったりしないよう止めるアクション。向きはこれらでなく機器から直に読む。
        private static readonly string[] _suspendedActionPaths =
        {
            InputActionPaths.Look,
            InputActionPaths.Attack,
            InputActionPaths.Aim,
            InputActionPaths.Previous,
            InputActionPaths.Next,
        };

        [SerializeField]
        [Min(0f)]
        private float _holdSeconds = DefaultHoldSeconds;

        [SerializeField]
        [Range(0f, 0.95f)]
        private float _deadZone = DefaultDeadZone;

        [SerializeField]
        [Min(1f)]
        private float _pointerRadius = DefaultPointerRadius;

        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<VisualElement> _slotElements = new List<VisualElement>();
        private readonly List<InputAction> _suspended = new List<InputAction>();
        private IEmoteControl _control;
        private bool _open;
        private bool _wasHeld;
        private bool _awaitsRelease;
        private float _heldSeconds;
        private Vector2 _pointer;
        private int _selectedIndex = -1;
        private string _centerText = string.Empty;
        private Label _center;
        [NonSerialized]
        private InputAction _holdAction;
        private bool _resolvedHoldAction;
        private bool _enabledHoldAction;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public float HoldSeconds
        {
            get => _holdSeconds;
            set => _holdSeconds = Mathf.Max(0f, value);
        }

        public float DeadZone
        {
            get => _deadZone;
            set => _deadZone = Mathf.Clamp(value, 0f, 0.95f);
        }

        // 本物のカーソルを使わずマウスの動きを溜めるのは、一人称の視点がカーソルをロックしたまま開くため。
        public float PointerRadius
        {
            get => _pointerRadius;
            set => _pointerRadius = Mathf.Max(1f, value);
        }

        public bool IsOpen => _open;

        public IReadOnlyList<Slot> Slots => _slots;

        public int SelectedIndex => _selectedIndex;

        public string SelectedEmoteId => _selectedIndex >= 0 && _selectedIndex < _slots.Count ? _slots[_selectedIndex].EmoteId : null;

        public string CenterText => _centerText;

        public static int IndexAt(Vector2 direction, int count, float deadZone)
        {
            if (count <= 0 || float.IsNaN(direction.x) || float.IsNaN(direction.y) || direction.magnitude <= deadZone)
            {
                return -1;
            }

            var step = 360f / count;
            var degrees = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            if (degrees < 0f)
            {
                degrees += 360f;
            }

            return Mathf.FloorToInt((degrees + step * 0.5f) / step) % count;
        }

        public static Vector2 SlotOffset(int index, int count, float radius)
        {
            if (count <= 0)
            {
                return Vector2.zero;
            }

            var radians = index * (2f * Mathf.PI / count);
            return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)) * radius;
        }

        public static string NameKeyOf(string emoteId)
        {
            return NameKeyPrefix + (emoteId ?? string.Empty);
        }

        // 機器を読まずに 1 フレーム分の入力を受け取るのは、機器から読む Update とテストが同じ道を通るため。
        public void Advance(bool held, Vector2 pointerDelta, Vector2 stick, float deltaSeconds)
        {
            if (_open && IsBlockedByOtherScreen())
            {
                Cancel();
            }

            if (!_open && _suspended.Count > 0 && !IsBlockedByOtherScreen())
            {
                ResumeActions();
            }

            if (!held)
            {
                if (_open)
                {
                    Close(SelectedEmoteId);
                }

                _wasHeld = false;
                _awaitsRelease = false;
                _heldSeconds = 0f;
                return;
            }

            if (!_wasHeld)
            {
                _wasHeld = true;
                _heldSeconds = 0f;
            }

            // 押したまま開き直さないのは、ポーズ画面を閉じた直後などに、離して決めるつもりのない輪が勝手に出ないようにするため。
            if (_awaitsRelease)
            {
                return;
            }

            if (!_open)
            {
                _heldSeconds += Mathf.Max(0f, deltaSeconds);
                if (_heldSeconds >= _holdSeconds)
                {
                    TryOpen();
                }

                return;
            }

            Point(pointerDelta, stick);
        }

        protected override void OnBound(VisualElement element)
        {
            Subscribe();
            Draw();
        }

        protected override void OnLanguageChanged()
        {
            if (_open)
            {
                RefreshNames();
                Draw();
            }
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Close(null);
            Unsubscribe();
            element.Clear();
            element.style.display = DisplayStyle.None;
            _slotElements.Clear();
            _center = null;
        }

        private void OnEnable()
        {
            AcquireHoldAction();
        }

        private void Start()
        {
            // Hud が木を作った後に付けられた (テストのシーンで足した) ときは Hud の Bind が来ないので、自分で今の木に付く。
            if (Element == null)
            {
                var document = GetComponent<UIDocument>();
                var root = document != null ? document.rootVisualElement : null;
                if (root != null && root.Q<VisualElement>(UxmlName) != null)
                {
                    Bind(root);
                }
            }
        }

        private void OnDisable()
        {
            ReleaseHoldAction();
            Close(null);
            ResumeActions();
            _wasHeld = false;
            _awaitsRelease = false;
            _heldSeconds = 0f;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ResumeActions();
        }

        private void Update()
        {
            if (_holdAction == null)
            {
                return;
            }

            var mouse = Mouse.current;
            var gamepad = Gamepad.current;
            Advance(
                _holdAction.IsPressed(),
                mouse != null ? mouse.delta.ReadValue() : Vector2.zero,
                gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero,
                Time.unscaledDeltaTime);
        }

        private static bool IsBlockedByOtherScreen()
        {
            return GameUi.IsPauseOpen || GameUi.IsLoadoutSelectOpen || GameUi.IsInventoryOpen || PlayerBindings.IsCapturingInput;
        }

        private void TryOpen()
        {
            if (IsBlockedByOtherScreen())
            {
                return;
            }

            var control = GameSignals.Emotes.Value;
            if (!GameUi.IsAlive(control))
            {
                return;
            }

            var ids = control.EmoteIds;
            if (ids == null || ids.Count == 0)
            {
                return;
            }

            _control = control;
            _slots.Clear();
            foreach (var id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    _slots.Add(new Slot(id, NameOf(id)));
                }
            }

            if (_slots.Count == 0)
            {
                _control = null;
                return;
            }

            _open = true;
            _pointer = Vector2.zero;
            _selectedIndex = -1;
            RefreshCenter();
            SuspendActions();
            Debug.Log($"{LogTag} opened emotes={_slots.Count}", this);
            Draw();
        }

        private void Point(Vector2 pointerDelta, Vector2 stick)
        {
            var next = _selectedIndex;
            if (stick.magnitude > _deadZone)
            {
                // スティックは離すと中央に戻るので、倒している間だけ選び直し、戻ったときは最後に指した枠を残す。
                next = IndexAt(stick, _slots.Count, _deadZone);
            }
            else if (pointerDelta != Vector2.zero)
            {
                _pointer = Vector2.ClampMagnitude(_pointer + pointerDelta, _pointerRadius);
                next = IndexAt(_pointer / _pointerRadius, _slots.Count, _deadZone);
            }

            if (next == _selectedIndex)
            {
                return;
            }

            _selectedIndex = next;
            RefreshCenter();
            ApplySelection();
        }

        private void Cancel()
        {
            if (!_open)
            {
                return;
            }

            _awaitsRelease = _wasHeld;
            Close(null);
        }

        private void Close(string emoteId)
        {
            if (!_open)
            {
                return;
            }

            var control = _control;
            _open = false;
            _control = null;
            _selectedIndex = -1;
            _slots.Clear();
            _centerText = string.Empty;
            // 他の画面が開いている間に戻すと、その画面が止めたつもりのアクションを有効にしてしまう (その画面は今有効なものだけを記録するため、この輪が止めた分は記録に無く戻らない)。閉じるまで持ち越す。
            if (!IsBlockedByOtherScreen())
            {
                ResumeActions();
            }

            Draw();

            if (emoteId == null)
            {
                Debug.Log($"{LogTag} closed without an emote", this);
                return;
            }

            if (!GameUi.IsAlive(control))
            {
                Debug.LogWarning($"{LogTag} the emote control went away before '{emoteId}' was played", this);
                return;
            }

            var played = control.Play(emoteId);
            Debug.Log($"{LogTag} play id={emoteId} played={played}", this);
        }

        private string NameOf(string emoteId)
        {
            // 表に無い Id (ゲームが足したばかりのエモート) でも枠が空にならないよう、Id をそのまま出す。
            return Localizer.TryGet(NameKeyOf(emoteId), out var text) ? text : emoteId;
        }

        private void RefreshNames()
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                _slots[i] = new Slot(_slots[i].EmoteId, NameOf(_slots[i].EmoteId));
            }

            RefreshCenter();
        }

        private void RefreshCenter()
        {
            _centerText = _selectedIndex >= 0 && _selectedIndex < _slots.Count
                ? _slots[_selectedIndex].Name
                : Localizer.Get(CancelKey);
        }

        // 開け閉めのときにだけ作り直し、選び直しでは印と中央の文字だけを替える。
        private void Draw()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.Clear();
            _slotElements.Clear();
            _center = null;
            element.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_open)
            {
                return;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                var offset = SlotOffset(i, _slots.Count, SlotRadius);
                var slot = new VisualElement { pickingMode = PickingMode.Ignore, name = _slots[i].EmoteId };
                slot.AddToClassList(SlotClass);
                slot.style.left = WheelHalfSize + offset.x;
                slot.style.top = WheelHalfSize + offset.y;
                var label = new Label(_slots[i].Name) { pickingMode = PickingMode.Ignore };
                label.AddToClassList(SlotNameClass);
                slot.Add(label);
                element.Add(slot);
                _slotElements.Add(slot);
            }

            _center = new Label(_centerText) { pickingMode = PickingMode.Ignore };
            _center.AddToClassList(CenterClass);
            element.Add(_center);
            ApplySelection();
        }

        private void ApplySelection()
        {
            for (var i = 0; i < _slotElements.Count; i++)
            {
                _slotElements[i].EnableInClassList(SelectedSlotClass, i == _selectedIndex);
            }

            if (_center != null)
            {
                _center.text = _centerText;
            }
        }

        private void SuspendActions()
        {
            if (!Application.isPlaying || InputSystem.actions == null)
            {
                return;
            }

            foreach (var path in _suspendedActionPaths)
            {
                var action = InputSystem.actions.FindAction(path);
                if (action == null || !action.enabled)
                {
                    continue;
                }

                action.Disable();
                _suspended.Add(action);
            }
        }

        private void ResumeActions()
        {
            foreach (var action in _suspended)
            {
                if (action != null && !action.enabled)
                {
                    action.Enable();
                }
            }

            _suspended.Clear();
        }

        private void AcquireHoldAction()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_resolvedHoldAction)
            {
                _holdAction = InputSystem.actions != null ? InputSystem.actions.FindAction(HoldActionPath) : null;
                _resolvedHoldAction = true;
                if (_holdAction == null)
                {
                    Debug.LogError($"{LogTag} the project actions have no '{HoldActionPath}'; the emote wheel cannot be opened", this);
                }
            }

            if (_holdAction == null || _holdAction.enabled)
            {
                return;
            }

            _holdAction.Enable();
            _enabledHoldAction = true;
        }

        private void ReleaseHoldAction()
        {
            if (_enabledHoldAction && _holdAction != null)
            {
                _holdAction.Disable();
            }

            _enabledHoldAction = false;
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.Emotes.Changed += HandleEmotesChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Emotes.Changed -= HandleEmotesChanged;
                _subscribed = false;
            }
        }

        private void HandleEmotesChanged(IEmoteControl control)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Emotes.Changed -= HandleEmotesChanged;
                return;
            }

            // 枠を作り直さずに閉じるのは、別の口は Id の並びが違いうるのに、開いた後に指していた枠のまま決めてしまわないため。
            if (_open && !ReferenceEquals(control, _control))
            {
                Cancel();
            }
        }

        public readonly struct Slot
        {
            public Slot(string emoteId, string name)
            {
                EmoteId = emoteId;
                Name = name;
            }

            public string EmoteId { get; }

            public string Name { get; }
        }
    }
}
