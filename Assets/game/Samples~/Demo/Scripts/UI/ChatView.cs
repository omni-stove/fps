using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ChatView : HudElementView
    {
        public const string LogTag = "[Chat]";

        public const string UxmlName = "chat";

        public const string StyleSheetResourcePath = "Hud/Chat";

        public const string LinesName = "chat-lines";

        public const string InputRowName = "chat-input";

        public const string PromptName = "chat-prompt";

        public const string FieldName = "chat-field";

        public const string RowClass = "chat__row";

        public const string TeamRowClass = "chat__row--team";

        public const string FadedRowClass = "chat__row--faded";

        public const string TeamInputClass = "chat__input--team";

        public const string LineKey = "chat.line";

        public const string TeamLineKey = "chat.teamLine";

        public const string AllPromptKey = "chat.prompt.all";

        public const string TeamPromptKey = "chat.prompt.team";

        public const int DefaultVisibleLines = 6;

        public const float DefaultFadeSeconds = 10f;

        private static StyleSheet _styleSheet;
        private static bool _warnedMissingStyleSheet;

        [SerializeField]
        private int _visibleLines = DefaultVisibleLines;

        [SerializeField]
        private float _fadeSeconds = DefaultFadeSeconds;

        // 入力の宛先を止めないアクションは無い。Pause も止め、入力中の Esc でポーズ画面を開かせない。
        private readonly PauseState _state = new PauseState(PauseTimeMode.KeepRunning, Array.Empty<string>());
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Label> _rows = new List<Label>();
        private readonly List<string> _lines = new List<string>();
        private readonly List<string> _enabledScratch = new List<string>();
        private double _clock;
        private ChatChannel _channel;
        private int _openedFrame = -1;
        private int _closedFrame = -1;
        private bool _subscribed;

        private InputActionAsset _actions;
        // PauseMenu と同じく、再コンパイルで null の InputAction が空のアクションとして戻らないようにする。
        [NonSerialized]
        private InputAction _chatAction;
        [NonSerialized]
        private InputAction _teamChatAction;
        private bool _enabledChatAction;
        private bool _enabledTeamChatAction;

        private VisualElement _linesElement;
        private VisualElement _inputRow;
        private Label _prompt;
        private TextField _field;

        public override string ElementName => UxmlName;

        public Action<ChatChannel, string> Sender { get; set; }

        public InputActionAsset Actions
        {
            get => _actions;
            set
            {
                if (ReferenceEquals(value, _actions))
                {
                    return;
                }

                Cancel();
                ReleaseActions();
                AcquireActions(value);
            }
        }

        public int VisibleLines
        {
            get => Math.Max(1, _visibleLines);
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "visible lines must be at least 1");
                }

                _visibleLines = value;
                Render();
            }
        }

        public float FadeSeconds
        {
            get => _fadeSeconds >= 0f && !float.IsInfinity(_fadeSeconds) ? _fadeSeconds : DefaultFadeSeconds;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "fade seconds must be finite and not negative");
                }

                _fadeSeconds = value;
                RefreshFade();
            }
        }

        public bool IsOpen => _state.IsOpen;

        public ChatChannel Channel => _channel;

        public TextField Field => _field;

        public IReadOnlyList<string> SuspendedActions => _state.SuspendedActions;

        public IReadOnlyList<string> Lines => _lines;

        public static LocalizedText LineText(ChatLine line, IParticipants participants)
        {
            var name = participants != null && participants.TryGet(line.SenderId, out var participant) && participant.Name.Length > 0
                ? participant.Name
                : KillFeedText.Unknown(line.SenderId);
            return new LocalizedText(line.Channel == ChatChannel.Team ? TeamLineKey : LineKey, name, line.Text);
        }

        public bool IsTeamLine(int index)
        {
            return ShownEntry(index).Line.Channel == ChatChannel.Team;
        }

        public bool IsFadedLine(int index)
        {
            return IsFaded(ShownEntry(index));
        }

        public void Show(IReadOnlyList<ChatLine> lines)
        {
            var previous = _entries.ToArray();
            var used = new bool[previous.Length];
            _entries.Clear();
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    var receivedAt = _clock;
                    for (var i = 0; i < previous.Length; i++)
                    {
                        if (!used[i] && previous[i].Line == line)
                        {
                            used[i] = true;
                            receivedAt = previous[i].ReceivedAt;
                            break;
                        }
                    }

                    _entries.Add(new Entry(line, receivedAt));
                }
            }

            Render();
        }

        public void Advance(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "delta must be finite and not negative");
            }

            _clock += deltaSeconds;
            RefreshFade();
        }

        public bool Open(ChatChannel channel)
        {
            if (!Enum.IsDefined(typeof(ChatChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "channel must be a defined ChatChannel");
            }

            if (Sender == null || _field == null || _state.IsOpen)
            {
                return false;
            }

            _state.Open(Time.timeScale, out _);
            _channel = channel;
            _openedFrame = Time.frameCount;
            SuspendPlayerInput();

            _field.SetValueWithoutNotify(string.Empty);
            RefreshPrompt();
            _inputRow.EnableInClassList(TeamInputClass, channel == ChatChannel.Team);
            _inputRow.style.display = DisplayStyle.Flex;
            RefreshFade();
            RefreshDisplay();
            var field = _field;
            field.Focus();

            // 表示を変えた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度置く
            if (field.panel != null && field.focusController?.focusedElement != field)
            {
                field.schedule.Execute(() =>
                {
                    if (this != null && _state.IsOpen && _field == field)
                    {
                        field.Focus();
                    }
                });
            }

            Debug.Log($"{LogTag} opened channel={channel} suspended={_state.SuspendedActions.Count}", this);
            return true;
        }

        public bool Submit()
        {
            if (!_state.IsOpen)
            {
                return false;
            }

            var text = (_field != null ? _field.value : null)?.Trim() ?? string.Empty;
            var sender = Sender;
            if (text.Length == 0 || sender == null)
            {
                Close();
                return false;
            }

            try
            {
                sender(_channel, text);
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} could not send to {_channel}: {e.Message}; the text is kept so it can be sent again", this);
                Debug.LogException(e, this);
                return false;
            }

            // 本文はプレイヤーが書いたものなので、ログには長さだけを出す (ChatLine.ToString と同じ)。
            Debug.Log($"{LogTag} sent channel={_channel} length={text.Length}", this);
            Close();
            return true;
        }

        public void Cancel()
        {
            if (_state.IsOpen)
            {
                Close();
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying && _actions == null)
            {
                var actions = InputSystem.actions;
                if (actions == null)
                {
                    Debug.LogError(
                        $"{LogTag} project-wide Input Actions are not set (Project Settings > Input System Package); chat cannot be opened",
                        this);
                    return;
                }

                AcquireActions(actions);
            }
        }

        private void OnDisable()
        {
            // 開いたまま消える (シーンを離れる・HUD を片付ける) と入力が止まったままになる。
            Cancel();
            ReleaseActions();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            _visibleLines = Math.Max(1, _visibleLines);
            if (!(_fadeSeconds >= 0f) || float.IsInfinity(_fadeSeconds))
            {
                _fadeSeconds = DefaultFadeSeconds;
            }
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
            if (_state.IsOpen)
            {
                SuspendPlayerInput();
                return;
            }

            // 閉じた Enter で同じフレームに開き直さない。設定画面で割り当ての入力を待っている間のキーも拾わない。
            if (Time.frameCount == _closedFrame || PlayerBindings.IsCapturingInput)
            {
                return;
            }

            if (_chatAction != null && _chatAction.WasPressedThisFrame())
            {
                Open(ChatChannel.All);
            }
            else if (_teamChatAction != null && _teamChatAction.WasPressedThisFrame())
            {
                Open(ChatChannel.Team);
            }
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            _rows.Clear();
            var styleSheet = LoadStyleSheet();
            if (styleSheet != null && !element.styleSheets.Contains(styleSheet))
            {
                element.styleSheets.Add(styleSheet);
            }

            _linesElement = new VisualElement { name = LinesName, pickingMode = PickingMode.Ignore };
            _linesElement.AddToClassList("chat__lines");
            element.Add(_linesElement);

            _inputRow = new VisualElement { name = InputRowName };
            _inputRow.AddToClassList("chat__input");
            _inputRow.style.display = DisplayStyle.None;
            _prompt = new Label { name = PromptName, pickingMode = PickingMode.Ignore };
            _prompt.AddToClassList("chat__prompt");
            _field = new TextField { name = FieldName };
            _field.AddToClassList("chat__field");
            _field.RegisterCallback<KeyDownEvent>(HandleFieldKeyDown, TrickleDown.TrickleDown);
            _field.RegisterCallback<FocusOutEvent>(HandleFieldFocusOut, TrickleDown.TrickleDown);
            _inputRow.Add(_prompt);
            _inputRow.Add(_field);
            element.Add(_inputRow);

            Subscribe();
            Show(GameSignals.Chat.Value);
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Cancel();
            Unsubscribe();
            _field?.UnregisterCallback<KeyDownEvent>(HandleFieldKeyDown, TrickleDown.TrickleDown);
            _field?.UnregisterCallback<FocusOutEvent>(HandleFieldFocusOut, TrickleDown.TrickleDown);
            element.Clear();
            _rows.Clear();
            _lines.Clear();
            _linesElement = null;
            _inputRow = null;
            _prompt = null;
            _field = null;
            element.style.display = DisplayStyle.None;
        }

        protected override void OnLanguageChanged()
        {
            RefreshPrompt();
            Render();
        }

        private void HandleFieldFocusOut(FocusOutEvent evt)
        {
            // フォーカスが外れると Enter / Esc が届かず、止めた入力も戻せなくなるので、入力欄の中へ移るとき以外は閉じる。
            if (evt.relatedTarget is VisualElement next && _field != null && _field.Contains(next))
            {
                return;
            }

            Cancel();
        }

        private void HandleFieldKeyDown(KeyDownEvent evt)
        {
            // 開いたフレームの Enter や U が入力欄に届くと、空のまま送ったり U が入ったりするので捨てる。
            if (Time.frameCount == _openedFrame)
            {
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.character == '\n' || evt.character == '\r')
            {
                evt.StopImmediatePropagation();
                if (evt.keyCode != KeyCode.None)
                {
                    Submit();
                }
            }
            else if (evt.keyCode == KeyCode.Escape || evt.character == '\u001b')
            {
                evt.StopImmediatePropagation();
                if (evt.keyCode != KeyCode.None)
                {
                    Cancel();
                }
            }
        }

        private void Close()
        {
            if (!_state.Close(out _, out var actionsToEnable))
            {
                return;
            }

            _closedFrame = Time.frameCount;
            ResumeActions(actionsToEnable);
            if (_field != null)
            {
                _field.SetValueWithoutNotify(string.Empty);
                _field.Blur();
            }

            if (_inputRow != null)
            {
                _inputRow.style.display = DisplayStyle.None;
            }

            RefreshFade();
            RefreshDisplay();
            Debug.Log($"{LogTag} closed resumed={actionsToEnable.Count}", this);
        }

        private void AcquireActions(InputActionAsset actions)
        {
            _actions = actions;
            if (actions == null)
            {
                return;
            }

            _chatAction = actions.FindAction(InputActionPaths.Chat);
            _teamChatAction = actions.FindAction(InputActionPaths.TeamChat);
            if (_chatAction == null || _teamChatAction == null)
            {
                Debug.LogWarning(
                    $"{LogTag} Input Actions '{actions.name}' has no '{(_chatAction == null ? InputActionPaths.Chat : InputActionPaths.TeamChat)}'; that key does not open chat",
                    this);
            }

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            _enabledChatAction = EnableIfDisabled(_chatAction);
            _enabledTeamChatAction = EnableIfDisabled(_teamChatAction);
        }

        private void ReleaseActions()
        {
            if (_enabledChatAction && _chatAction != null)
            {
                _chatAction.Disable();
            }

            if (_enabledTeamChatAction && _teamChatAction != null)
            {
                _teamChatAction.Disable();
            }

            _enabledChatAction = false;
            _enabledTeamChatAction = false;
            _chatAction = null;
            _teamChatAction = null;
            _actions = null;
        }

        private static bool EnableIfDisabled(InputAction action)
        {
            if (action == null || action.enabled)
            {
                return false;
            }

            action.Enable();
            return true;
        }

        private void SuspendPlayerInput()
        {
            var map = (_chatAction ?? _teamChatAction)?.actionMap;
            if (map == null)
            {
                return;
            }

            _enabledScratch.Clear();
            foreach (var action in map.actions)
            {
                if (action.enabled)
                {
                    _enabledScratch.Add(PathOf(action));
                }
            }

            if (_enabledScratch.Count == 0)
            {
                return;
            }

            // PauseMenu と同じく、返り値は閉じたときに戻す記録にだけ使い、止めるのは今有効なもの全部にする。
            _state.Suspend(_enabledScratch);
            foreach (var action in map.actions)
            {
                if (action.enabled && !_state.KeepsEnabled(PathOf(action)))
                {
                    action.Disable();
                }
            }
        }

        private void ResumeActions(IReadOnlyList<string> paths)
        {
            if (_actions == null)
            {
                return;
            }

            foreach (var path in paths)
            {
                var action = _actions.FindAction(path);
                if (action == null)
                {
                    Debug.LogWarning($"{LogTag} could not find '{path}' to enable again after typing", this);
                    continue;
                }

                if (!action.enabled)
                {
                    action.Enable();
                }
            }
        }

        // 行は数行で、変わるのは届いたときと言語・名前の表が変わったときだけなので、差分を取らずに作り直す。
        private void Render()
        {
            _lines.Clear();
            var participants = GameSignals.Participants.Value;
            var first = FirstShownIndex();
            for (var i = first; i < _entries.Count; i++)
            {
                _lines.Add(Localizer.Resolve(LineText(_entries[i].Line, participants)));
            }

            if (_linesElement == null)
            {
                return;
            }

            _linesElement.Clear();
            _rows.Clear();
            for (var i = 0; i < _lines.Count; i++)
            {
                // 本文はプレイヤーが書いたものなので、タグを色や大きさとして読ませない。
                var row = new Label(_lines[i]) { pickingMode = PickingMode.Ignore, enableRichText = false };
                row.AddToClassList(RowClass);
                row.EnableInClassList(TeamRowClass, _entries[first + i].Line.Channel == ChatChannel.Team);
                _linesElement.Add(row);
                _rows.Add(row);
            }

            RefreshFade();
            RefreshDisplay();
        }

        private void RefreshFade()
        {
            var first = FirstShownIndex();
            for (var i = 0; i < _rows.Count && first + i < _entries.Count; i++)
            {
                _rows[i].EnableInClassList(FadedRowClass, IsFaded(_entries[first + i]));
            }
        }

        private void RefreshDisplay()
        {
            if (Element != null)
            {
                Element.style.display = _rows.Count > 0 || _state.IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void RefreshPrompt()
        {
            if (_prompt != null)
            {
                _prompt.text = Localizer.Get(_channel == ChatChannel.Team ? TeamPromptKey : AllPromptKey);
            }
        }

        private bool IsFaded(Entry entry)
        {
            return !_state.IsOpen && _clock - entry.ReceivedAt >= FadeSeconds;
        }

        private int FirstShownIndex()
        {
            return Math.Max(0, _entries.Count - VisibleLines);
        }

        private Entry ShownEntry(int index)
        {
            if (index < 0 || index >= _lines.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"index must be between 0 and {_lines.Count - 1}");
            }

            return _entries[FirstShownIndex() + index];
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.Chat.Changed += HandleChatChanged;
                GameSignals.Participants.Changed += HandleParticipantsChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Chat.Changed -= HandleChatChanged;
                GameSignals.Participants.Changed -= HandleParticipantsChanged;
                _subscribed = false;
            }
        }

        private void HandleChatChanged(IReadOnlyList<ChatLine> lines)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Chat.Changed -= HandleChatChanged;
                GameSignals.Participants.Changed -= HandleParticipantsChanged;
                return;
            }

            Show(lines);
        }

        private void HandleParticipantsChanged(IParticipants participants)
        {
            if (this == null)
            {
                GameSignals.Chat.Changed -= HandleChatChanged;
                GameSignals.Participants.Changed -= HandleParticipantsChanged;
                return;
            }

            Render();
        }

        private static StyleSheet LoadStyleSheet()
        {
            if (_styleSheet == null)
            {
                _styleSheet = Resources.Load<StyleSheet>(StyleSheetResourcePath);
            }

            if (_styleSheet == null && !_warnedMissingStyleSheet)
            {
                _warnedMissingStyleSheet = true;
                Debug.LogWarning($"{LogTag} style sheet 'Resources/{StyleSheetResourcePath}.uss' was not found; chat is unstyled");
            }

            return _styleSheet;
        }

        private static string PathOf(InputAction action)
        {
            return action.actionMap.name + "/" + action.name;
        }

        private readonly struct Entry
        {
            public Entry(ChatLine line, double receivedAt)
            {
                Line = line;
                ReceivedAt = receivedAt;
            }

            public ChatLine Line { get; }

            public double ReceivedAt { get; }
        }
    }
}
