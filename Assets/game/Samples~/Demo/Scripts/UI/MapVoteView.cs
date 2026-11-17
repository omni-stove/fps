using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MapVoteView : HudElementView
    {
        // Hud.uxml には自分の枠を足さず、HUD の根の中に枠をコードで作る。HUD を手で組み替えたゲームでも、根さえあれば出せるようにするため。
        public const string UxmlName = "hud";

        public const string PanelName = "map-vote";

        public const string StyleSheetResourcePath = "Hud/MapVote";

        public const string PanelClass = "map-vote";
        public const string TitleClass = "map-vote__title";
        public const string TimerClass = "map-vote__timer";
        public const string CandidatesClass = "map-vote__candidates";
        public const string CandidateClass = "map-vote__candidate";
        public const string MineClass = "map-vote__candidate--mine";
        public const string DecidedCandidateClass = "map-vote__candidate--decided";
        public const string NameClass = "map-vote__name";
        public const string VotesClass = "map-vote__votes";

        public const string TitleKey = "mapVote.title";
        public const string RemainingKey = "mapVote.remaining";
        public const string VotesKey = "mapVote.votes";
        public const string DecidedKey = "mapVote.decided";
        public const string MapNameKeyPrefix = "map.";

        // 結果の文の後ろに入れると、結果より手前に、ポーズの画面より奥に描かれる
        private const string ResultSiblingName = "hud-result-restart";

        private static StyleSheet _styleSheet;
        private static bool _warnedMissingStyleSheet;

        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Label> _names = new List<Label>();
        private readonly List<Label> _votes = new List<Label>();
        private readonly List<string> _builtMapIds = new List<string>();
        private MapVoteStatus? _status;
        private float _remainingSeconds;
        private int _shownSeconds = -1;
        private VisualElement _panel;
        private Label _title;
        private Label _timer;
        private VisualElement _candidates;
        private bool _subscribed;
        private bool _restoreCursor;

        public override string ElementName => UxmlName;

        // 票は GameSignals.MapVoteSender へ送り、これは送った後の知らせにする。ここで受けて送らせないのは、送る口が出ていないときに選んだことにしないため。
        public event Action<int> Voted;

        public MapVoteStatus? Status => _status;

        public bool IsShown => _status.HasValue && _status.Value.Candidates.Count > 0;

        public VisualElement Panel => _panel;

        public IReadOnlyList<Button> CandidateButtons => _buttons;

        public string TitleText => _title != null ? _title.text : string.Empty;

        public string TimerText => _timer != null && _timer.style.display != DisplayStyle.None ? _timer.text : string.Empty;

        // データにマップの名前の鍵が無いので、Id から鍵を作って言語の表を引く。
        public static string MapNameKey(string mapId)
        {
            return MapNameKeyPrefix + (mapId ?? string.Empty);
        }

        public static string MapName(string mapId)
        {
            var id = mapId ?? string.Empty;
            return id.Length > 0 && Localizer.TryGet(MapNameKey(id), out var name) ? name : id;
        }

        // 残りが 0.2 秒でも「残り 0 秒」と出すと締め切ったように見えるので切り上げる
        public static int WholeSeconds(float seconds)
        {
            return seconds > 0f ? Mathf.CeilToInt(seconds) : 0;
        }

        public void Show(MapVoteStatus? status)
        {
            _status = status;
            _remainingSeconds = status.HasValue ? status.Value.RemainingSeconds : 0f;
            Render();
            UpdateCursor();
        }

        // もう入れた候補を選び直しても渡さないのは、ボタンを押し続けたときに同じ票を何度も送らないため。
        public bool Choose(int index)
        {
            if (!IsShown || _status.Value.IsDecided)
            {
                return false;
            }

            var status = _status.Value;
            if (index < 0 || index >= status.Candidates.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"index must be between 0 and {status.Candidates.Count - 1}");
            }

            var mapId = status.Candidates[index].MapId;
            if (string.Equals(mapId, status.MyVote, StringComparison.Ordinal))
            {
                return false;
            }

            var sender = GameSignals.MapVoteSender.Value;
            if (sender == null)
            {
                Debug.LogWarning($"{Hud.LogTag} map '{mapId}' was chosen but no vote sender is published (GameSignals.MapVoteSender)", this);
                return false;
            }

            Debug.Log($"{Hud.LogTag} map vote chose={mapId} index={index}", this);
            sender.Vote(index);
            Voted?.Invoke(index);
            return true;
        }

        protected override void OnBound(VisualElement element)
        {
            BuildPanel(element);
            Subscribe();
            Show(GameSignals.MapVote.Value);
        }

        protected override void OnLanguageChanged()
        {
            RefreshTexts();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            if (_panel != null)
            {
                _panel.RemoveFromHierarchy();
            }

            _panel = null;
            _title = null;
            _timer = null;
            _candidates = null;
            ClearCandidates();
            RestoreCursor();
        }

        // Hud は木を作り直すときにしかビューへ要素を渡さないので、HUD を作った後に足されたときは今の木を自分で受ける
        private void Start()
        {
            if (Element != null)
            {
                return;
            }

            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root != null && root.Q<VisualElement>(UxmlName) != null)
            {
                Bind(root);
            }
        }

        private void OnDisable()
        {
            RestoreCursor();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            RestoreCursor();
        }

        // 届く値は送られた時点の残りなので、次の値が届くまでは手元で数える
        private void Update()
        {
            if (!IsShown || _status.Value.IsDecided || _remainingSeconds <= 0f)
            {
                return;
            }

            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Time.unscaledDeltaTime);
            if (WholeSeconds(_remainingSeconds) != _shownSeconds)
            {
                RefreshTimer();
            }
        }

        private void BuildPanel(VisualElement root)
        {
            _panel = new VisualElement { name = PanelName, pickingMode = PickingMode.Ignore };
            _panel.AddToClassList(PanelClass);
            var styleSheet = LoadStyleSheet();
            if (styleSheet != null)
            {
                _panel.styleSheets.Add(styleSheet);
            }

            _title = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            _title.AddToClassList(TitleClass);
            _panel.Add(_title);

            _timer = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            _timer.AddToClassList(TimerClass);
            _panel.Add(_timer);

            _candidates = new VisualElement { pickingMode = PickingMode.Ignore };
            _candidates.AddToClassList(CandidatesClass);
            _panel.Add(_candidates);

            _panel.style.display = DisplayStyle.None;
            var sibling = root.Q<VisualElement>(ResultSiblingName);
            if (sibling != null && sibling.parent == root)
            {
                root.Insert(root.IndexOf(sibling) + 1, _panel);
            }
            else
            {
                root.Add(_panel);
            }
        }

        private void Render()
        {
            if (_panel == null)
            {
                return;
            }

            if (!IsShown)
            {
                ClearCandidates();
                _panel.style.display = DisplayStyle.None;
                return;
            }

            var built = SameCandidates(_status.Value.Candidates);
            if (!built)
            {
                BuildCandidates(_status.Value.Candidates);
            }

            _panel.style.display = DisplayStyle.Flex;
            RefreshTexts();
            if (!built && !_status.Value.IsDecided)
            {
                FocusChoice();
            }
        }

        // 票数だけが変わったときにボタンを作り直すと、ゲームパッドで動かしたフォーカスが先頭へ戻ってしまう
        private bool SameCandidates(IReadOnlyList<MapVoteCandidate> candidates)
        {
            if (candidates.Count != _builtMapIds.Count)
            {
                return false;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (!string.Equals(candidates[i].MapId, _builtMapIds[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private void BuildCandidates(IReadOnlyList<MapVoteCandidate> candidates)
        {
            ClearCandidates();
            for (var i = 0; i < candidates.Count; i++)
            {
                var index = i;
                var button = new Button(() => Choose(index)) { text = string.Empty };
                button.AddToClassList(CandidateClass);

                var name = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
                name.AddToClassList(NameClass);
                button.Add(name);

                var votes = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
                votes.AddToClassList(VotesClass);
                button.Add(votes);

                _candidates.Add(button);
                _buttons.Add(button);
                _names.Add(name);
                _votes.Add(votes);
                _builtMapIds.Add(candidates[i].MapId);
            }
        }

        private void ClearCandidates()
        {
            foreach (var button in _buttons)
            {
                button.RemoveFromHierarchy();
            }

            _buttons.Clear();
            _names.Clear();
            _votes.Clear();
            _builtMapIds.Clear();
        }

        private void RefreshTexts()
        {
            if (_panel == null || !IsShown)
            {
                return;
            }

            var status = _status.Value;
            _title.text = status.IsDecided
                ? Localizer.Resolve(new LocalizedText(DecidedKey, MapName(status.DecidedMapId)))
                : Localizer.Get(TitleKey);

            var candidates = status.Candidates;
            for (var i = 0; i < _buttons.Count; i++)
            {
                var mapId = candidates[i].MapId;
                _names[i].text = MapName(mapId);
                _votes[i].text = Localizer.Resolve(new LocalizedText(VotesKey, candidates[i].Votes));
                _buttons[i].EnableInClassList(MineClass, string.Equals(mapId, status.MyVote, StringComparison.Ordinal));
                _buttons[i].EnableInClassList(DecidedCandidateClass, string.Equals(mapId, status.DecidedMapId, StringComparison.Ordinal));
                _buttons[i].SetEnabled(!status.IsDecided);
            }

            RefreshTimer();
        }

        private void RefreshTimer()
        {
            if (_timer == null || !IsShown)
            {
                return;
            }

            if (_status.Value.IsDecided)
            {
                _shownSeconds = -1;
                _timer.style.display = DisplayStyle.None;
                return;
            }

            _shownSeconds = WholeSeconds(_remainingSeconds);
            _timer.text = Localizer.Resolve(new LocalizedText(RemainingKey, _shownSeconds));
            _timer.style.display = DisplayStyle.Flex;
        }

        private void FocusChoice()
        {
            // PauseMenu は開くときに 1 度しかフォーカスを取らないので、ここで奪うとポーズのボタンへ戻らなくなる
            if (PauseMenu.IsAnyOpen || _buttons.Count == 0)
            {
                return;
            }

            var mine = _builtMapIds.IndexOf(_status.Value.MyVote);
            var target = _buttons[mine >= 0 ? mine : 0];
            target.Focus();

            // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (target.panel != null && target.focusController?.focusedElement != target)
            {
                target.schedule.Execute(() =>
                {
                    if (this != null && _buttons.Contains(target) && !PauseMenu.IsAnyOpen)
                    {
                        target.Focus();
                    }
                });
            }
        }

        private void UpdateCursor()
        {
            if (Element != null && IsShown && !_status.Value.IsDecided)
            {
                ReleaseCursor();
            }
            else
            {
                RestoreCursor();
            }
        }

        private void ReleaseCursor()
        {
            // Edit Mode のテストでエディタのカーソルを書き換えない
            if (!Application.isPlaying || _restoreCursor)
            {
                return;
            }

            _restoreCursor = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void RestoreCursor()
        {
            if (!_restoreCursor)
            {
                return;
            }

            _restoreCursor = false;

            // 出している間に視点操作が切り替わることがあるので、控えた値は戻さず決め直す。ポーズ中は閉じるときに PauseMenu が決め直す
            if (!PauseMenu.IsAnyOpen)
            {
                FirstPersonLook.RefreshCursorLock();
            }
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
                Debug.LogWarning($"{Hud.LogTag} style sheet 'Resources/{StyleSheetResourcePath}.uss' was not found; the map vote is unstyled");
            }

            return _styleSheet;
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.MapVote.Changed += HandleMapVoteChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.MapVote.Changed -= HandleMapVoteChanged;
                _subscribed = false;
            }
        }

        private void HandleMapVoteChanged(MapVoteStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.MapVote.Changed -= HandleMapVoteChanged;
                return;
            }

            Show(status);
        }
    }
}
