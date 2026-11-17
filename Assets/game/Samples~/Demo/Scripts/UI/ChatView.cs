using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    /// <summary>
    /// 画面左下のテキストチャット。Hud.uxml の <see cref="UxmlName"/> に、最近の行 (<see cref="GameSignals.Chat"/> の後ろから
    /// <see cref="VisibleLines"/> 行) と入力欄を入れる。行は届いてから <see cref="FadeSeconds"/> 秒で <see cref="FadedRowClass"/> を付けて薄くし、
    /// 入力欄を開いている間はすべて濃く出す。チームの行には <see cref="TeamRowClass"/> を付けて色を変える。
    /// 送った人の名前は <see cref="GameSignals.Participants"/> から引き、引けなければ <c>?&lt;id&gt;</c> にする。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Play 中は Chat / TeamChat の入力 (<see cref="InputActionPaths.Chat"/>・<see cref="InputActionPaths.TeamChat"/>) で入力欄を開き、
    /// Enter で <see cref="Sender"/> に渡して閉じ、Esc で送らずに閉じる。<see cref="Sender"/> が null の間 (オフラインなど) は開かない。
    /// </para>
    /// <para>
    /// 開いている間は、ポーズ画面 (<see cref="PauseMenu"/>) と同じく Core の <see cref="PauseState"/> で、Chat と同じアクションマップ (Player) の
    /// 有効なアクションをすべて止め、閉じたら自分が止めたものだけを戻す。時間には触らない。Pause も止めるので、入力中の Esc でポーズ画面は開かない。
    /// </para>
    /// <para>
    /// <see cref="Hud.AddViews"/> はまだ付けないので、使うときは HUD の GameObject に手で足し、<see cref="Sender"/> を渡す。
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ChatView : HudElementView
    {
        public const string LogTag = "[Chat]";

        /// <summary>Hud.uxml のチャットの要素。</summary>
        public const string UxmlName = "chat";

        /// <summary>Resources からのチャットの USS (Demo サンプルの UI/Resources/Hud/Chat.uss)。</summary>
        public const string StyleSheetResourcePath = "Hud/Chat";

        /// <summary>行を並べる要素の name。</summary>
        public const string LinesName = "chat-lines";

        /// <summary>入力欄の枠の name。開いている間だけ出す。</summary>
        public const string InputRowName = "chat-input";

        /// <summary>入力欄の前に出す宛先 (全体・チーム) の Label の name。</summary>
        public const string PromptName = "chat-prompt";

        /// <summary>入力欄の TextField の name。</summary>
        public const string FieldName = "chat-field";

        /// <summary>1 行の Label に付ける USS クラス。</summary>
        public const string RowClass = "chat__row";

        /// <summary>チームの行に足す USS クラス。</summary>
        public const string TeamRowClass = "chat__row--team";

        /// <summary>届いてから <see cref="FadeSeconds"/> 秒たった行に足す USS クラス。</summary>
        public const string FadedRowClass = "chat__row--faded";

        /// <summary>チームの宛先で開いている間の入力欄の枠に足す USS クラス。</summary>
        public const string TeamInputClass = "chat__input--team";

        /// <summary>全体の行の文のキー ({0} 名前、{1} 本文)。</summary>
        public const string LineKey = "chat.line";

        /// <summary>チームの行の文のキー ({0} 名前、{1} 本文)。</summary>
        public const string TeamLineKey = "chat.teamLine";

        /// <summary>全体へ送る入力欄の宛先の文のキー。</summary>
        public const string AllPromptKey = "chat.prompt.all";

        /// <summary>チームへ送る入力欄の宛先の文のキー。</summary>
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

        /// <summary>
        /// 入力欄で Enter を押したときに、宛先と前後の空白を除いた本文を渡す口。null の間は入力欄を開かない。
        /// 投げたら入力欄を開いたまま本文を残す。
        /// </summary>
        public Action<ChatChannel, string> Sender { get; set; }

        /// <summary>
        /// Chat / TeamChat と、開いている間に止めるアクションを探すアセット。Play 中に null なら <see cref="InputSystem.actions"/> を使う。
        /// 開いている間に替えると、送らずに閉じてから替える。
        /// </summary>
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

        /// <summary>左下に出す最近の行の数。</summary>
        /// <exception cref="ArgumentOutOfRangeException">1 未満。</exception>
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

        /// <summary>行が届いてから薄くするまでの秒数。</summary>
        /// <exception cref="ArgumentOutOfRangeException">負か有限でない。</exception>
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

        /// <summary>入力欄を開いているか。</summary>
        public bool IsOpen => _state.IsOpen;

        /// <summary>開いている入力欄の宛先。閉じている間は最後に開いた宛先。</summary>
        public ChatChannel Channel => _channel;

        /// <summary>入力欄。要素を持っていなければ null。</summary>
        public TextField Field => _field;

        /// <summary>今止めているアクション (例: "Player/Move")。閉じている間は空。</summary>
        public IReadOnlyList<string> SuspendedActions => _state.SuspendedActions;

        /// <summary>出している行の文 (古い順)。</summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>
        /// line を 1 行の文にする。名前は participants から引き、引けないか null なら <c>?&lt;id&gt;</c> にする。
        /// 本文は表を引かずにそのまま入れる。
        /// </summary>
        public static LocalizedText LineText(ChatLine line, IParticipants participants)
        {
            var name = participants != null && participants.TryGet(line.SenderId, out var participant) && participant.Name.Length > 0
                ? participant.Name
                : KillFeedText.Unknown(line.SenderId);
            return new LocalizedText(line.Channel == ChatChannel.Team ? TeamLineKey : LineKey, name, line.Text);
        }

        /// <summary>出している行のうち index 番目 (古い順) がチームの行か。</summary>
        /// <exception cref="ArgumentOutOfRangeException">index が 0 以上 <see cref="Lines"/> の数未満でない。</exception>
        public bool IsTeamLine(int index)
        {
            return ShownEntry(index).Line.Channel == ChatChannel.Team;
        }

        /// <summary>出している行のうち index 番目 (古い順) を薄くしているか。</summary>
        /// <exception cref="ArgumentOutOfRangeException">index が 0 以上 <see cref="Lines"/> の数未満でない。</exception>
        public bool IsFadedLine(int index)
        {
            return IsFaded(ShownEntry(index));
        }

        /// <summary>
        /// lines を出す行にする。前に出していたのと同じ行は届いた時刻を引き継ぎ、新しい行は今届いたものとする。null なら行を消す。
        /// 要素を持っている間は <see cref="GameSignals.Chat"/> が出されるたびに呼ばれる。
        /// </summary>
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

        /// <summary>時間を deltaSeconds 進め、薄くする行を決め直す。Play 中は Update が呼ぶ。</summary>
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds が負か有限でない。</exception>
        public void Advance(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "delta must be finite and not negative");
            }

            _clock += deltaSeconds;
            RefreshFade();
        }

        /// <summary>
        /// channel へ送る入力欄を開き、プレイヤーの入力を止めて入力欄にフォーカスを置く。
        /// 開いたら true。<see cref="Sender"/> が null か、要素を持っていないか、既に開いていれば何もせず false。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">channel が定義に無い値。</exception>
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

        /// <summary>
        /// 入力欄の本文を前後の空白を除いて <see cref="Sender"/> に渡し、閉じる。送ったら true。
        /// 本文が空なら送らずに閉じて false。<see cref="Sender"/> が投げたら開いたまま本文を残して false。閉じていれば何もせず false。
        /// </summary>
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

        /// <summary>送らずに入力欄を閉じ、止めた入力を戻す。閉じていれば何もしない。</summary>
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
