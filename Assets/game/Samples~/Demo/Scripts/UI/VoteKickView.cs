using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 試合の最中に出るので、MapVoteView と違ってカーソルを放さない。押せるのはカーソルが出ている間だけで、キーで入れるときは Vote を呼ぶ。
    [DisallowMultipleComponent]
    public sealed class VoteKickView : HudElementView
    {
        public const string UxmlName = "vote-kick";

        public const string StyleSheetResourcePath = "Hud/VoteKick";

        public const string TitleClass = "vote-kick__title";
        public const string StatusRowClass = "vote-kick__status";
        public const string TallyClass = "vote-kick__tally";
        public const string TimerClass = "vote-kick__timer";
        public const string ChoicesClass = "vote-kick__choices";
        public const string ChoiceClass = "vote-kick__choice";
        public const string YesClass = "vote-kick__yes";
        public const string NoClass = "vote-kick__no";
        public const string MineClass = "vote-kick__choice--mine";
        public const string TargetIsLocalClass = "vote-kick--target-is-local";

        private static StyleSheet _styleSheet;
        private static bool _warnedMissingStyleSheet;

        private VoteKickStatus? _status;
        private float _remainingSeconds;
        private int _shownSeconds = -1;
        private bool? _myVote;
        private Label _title;
        private Label _tally;
        private Label _timer;
        private VisualElement _choices;
        private Button _yes;
        private Button _no;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        // 票は GameSignals.VoteKickSender へ送り、これは送った後の知らせにする。
        public event Action<bool> Voted;

        public VoteKickStatus? Status => _status;

        public bool IsShown => _status.HasValue;

        // 送った票は届く状態に含まれないので、手元で覚えた票。言い出した人は言い出したときに賛成を入れている。
        public bool? MyVote => _myVote;

        public bool IsLocalTarget => _status.HasValue && IsLocalPlayer(_status.Value.TargetId);

        public bool CanVote => IsShown && !IsLocalTarget && GameSignals.VoteKickSender.HasValue;

        public string TitleText => IsShown && _title != null ? _title.text : string.Empty;

        public string TallyText => IsShown && _tally != null ? _tally.text : string.Empty;

        public string TimerText => IsShown && _timer != null ? _timer.text : string.Empty;

        public Button YesButton => _yes;

        public Button NoButton => _no;

        public bool ChoicesShown => _choices != null && _choices.style.display != DisplayStyle.None;

        public static string Tally(int yesVotes, int requiredVotes)
        {
            return ScoreboardText.Count(yesVotes) + " / " + ScoreboardText.Count(requiredVotes);
        }

        // スコアボードに載っていない参加者でも投票は出すので、番号の名前にする。
        public static string PlayerName(int playerId)
        {
            var entries = GameSignals.Scoreboard.Value;
            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].PlayerId != playerId)
                    {
                        continue;
                    }

                    try
                    {
                        var name = Localizer.Resolve(ScoreboardText.Name(entries[i].Name));
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        Debug.LogWarning($"{Hud.LogTag} unknown scoreboard name {entries[i].Name} for participant {playerId}");
                    }

                    break;
                }
            }

            return Localizer.Resolve(ScoreboardText.PlayerName(playerId));
        }

        public static bool IsLocalPlayer(int playerId)
        {
            var entries = GameSignals.Scoreboard.Value;
            if (entries == null)
            {
                return false;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].PlayerId == playerId)
                {
                    return entries[i].IsLocal;
                }
            }

            return false;
        }

        public void Show(VoteKickStatus? status)
        {
            var previous = _status;
            _status = status;
            if (!status.HasValue)
            {
                _myVote = null;
            }
            else if (!previous.HasValue
                || previous.Value.TargetId != status.Value.TargetId
                || previous.Value.CallerId != status.Value.CallerId)
            {
                _myVote = IsLocalPlayer(status.Value.CallerId) ? true : (bool?)null;
            }

            _remainingSeconds = status.HasValue ? status.Value.RemainingSeconds : 0f;
            Render();
        }

        // 同じ票を入れ直しても送らないのは、キーを押し続けたときに同じ票を何度も送らないため。
        public bool Vote(bool inFavor)
        {
            if (!IsShown || IsLocalTarget || _myVote == inFavor)
            {
                return false;
            }

            var sender = GameSignals.VoteKickSender.Value;
            if (sender == null)
            {
                Debug.LogWarning($"{Hud.LogTag} a vote-kick vote was chosen but no sender is published (GameSignals.VoteKickSender)", this);
                return false;
            }

            Debug.Log($"{Hud.LogTag} vote kick target={_status.Value.TargetId} inFavor={inFavor}", this);
            sender.Vote(inFavor);
            _myVote = inFavor;
            Voted?.Invoke(inFavor);
            RefreshChoices();
            return true;
        }

        protected override void OnBound(VisualElement element)
        {
            Build(element);
            Subscribe();
            Show(GameSignals.VoteKick.Value);
        }

        protected override void OnLanguageChanged()
        {
            RefreshTexts();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.Clear();
            element.RemoveFromClassList(TargetIsLocalClass);
            if (_styleSheet != null)
            {
                element.styleSheets.Remove(_styleSheet);
            }

            element.style.display = DisplayStyle.None;
            _title = null;
            _tally = null;
            _timer = null;
            _choices = null;
            _yes = null;
            _no = null;
        }

        // Hud が木を作り直すときにしかビューへ要素を渡さないので、HUD を作った後に足されたときは今の木を自分で受ける
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

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // 届く値は送られた時点の残りなので、次の値が届くまでは手元で数える
        private void Update()
        {
            if (!IsShown || _remainingSeconds <= 0f)
            {
                return;
            }

            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Time.unscaledDeltaTime);
            if (MapVoteView.WholeSeconds(_remainingSeconds) != _shownSeconds)
            {
                RefreshTimer();
            }
        }

        private void Build(VisualElement element)
        {
            element.Clear();
            var styleSheet = LoadStyleSheet();
            if (styleSheet != null && !element.styleSheets.Contains(styleSheet))
            {
                element.styleSheets.Add(styleSheet);
            }

            _title = NewLabel(TitleClass);
            element.Add(_title);

            var statusRow = new VisualElement { pickingMode = PickingMode.Ignore };
            statusRow.AddToClassList(StatusRowClass);
            _tally = NewLabel(TallyClass);
            statusRow.Add(_tally);
            _timer = NewLabel(TimerClass);
            statusRow.Add(_timer);
            element.Add(statusRow);

            _choices = new VisualElement { pickingMode = PickingMode.Ignore };
            _choices.AddToClassList(ChoicesClass);
            _yes = NewChoice(YesClass, true);
            _choices.Add(_yes);
            _no = NewChoice(NoClass, false);
            _choices.Add(_no);
            element.Add(_choices);
        }

        private Button NewChoice(string className, bool inFavor)
        {
            var button = new Button(() => Vote(inFavor)) { text = string.Empty };
            button.AddToClassList(ChoiceClass);
            button.AddToClassList(className);
            return button;
        }

        private static Label NewLabel(string className)
        {
            var label = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private void Render()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            if (!IsShown)
            {
                _shownSeconds = -1;
                element.RemoveFromClassList(TargetIsLocalClass);
                element.style.display = DisplayStyle.None;
                return;
            }

            RefreshTexts();
            element.style.display = DisplayStyle.Flex;
        }

        private void RefreshTexts()
        {
            if (Element == null || _title == null || !IsShown)
            {
                return;
            }

            var status = _status.Value;
            _title.text = Localizer.Resolve(ModerationText.VoteKickCalled(PlayerName(status.CallerId), PlayerName(status.TargetId)));
            _tally.text = Tally(status.YesVotes, status.RequiredVotes);
            _yes.text = Localizer.Resolve(ModerationText.VoteKickYes);
            _no.text = Localizer.Resolve(ModerationText.VoteKickNo);
            RefreshTimer();
            RefreshChoices();
        }

        private void RefreshTimer()
        {
            if (_timer == null || !IsShown)
            {
                return;
            }

            _shownSeconds = MapVoteView.WholeSeconds(_remainingSeconds);
            _timer.text = Localizer.Resolve(ModerationText.VoteKickRemaining(_shownSeconds));
        }

        // 対象の人は自分の投票に入れられず、送る口が無いときは押しても届かないので、どちらも賛否のボタンを出さない。
        private void RefreshChoices()
        {
            var element = Element;
            if (element == null || _choices == null || !IsShown)
            {
                return;
            }

            element.EnableInClassList(TargetIsLocalClass, IsLocalTarget);
            _choices.style.display = CanVote ? DisplayStyle.Flex : DisplayStyle.None;
            _yes.EnableInClassList(MineClass, _myVote == true);
            _no.EnableInClassList(MineClass, _myVote == false);
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
                Debug.LogWarning($"{Hud.LogTag} style sheet 'Resources/{StyleSheetResourcePath}.uss' was not found; the vote kick is unstyled");
            }

            return _styleSheet;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.VoteKick.Changed += HandleVoteKickChanged;
            GameSignals.VoteKickSender.Changed += HandleSenderChanged;
            GameSignals.Scoreboard.Changed += HandleScoreboardChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.VoteKick.Changed -= HandleVoteKickChanged;
            GameSignals.VoteKickSender.Changed -= HandleSenderChanged;
            GameSignals.Scoreboard.Changed -= HandleScoreboardChanged;
            _subscribed = false;
        }

        private void HandleVoteKickChanged(VoteKickStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.VoteKick.Changed -= HandleVoteKickChanged;
                return;
            }

            Show(status);
        }

        private void HandleSenderChanged(IVoteKickSender sender)
        {
            if (this == null)
            {
                GameSignals.VoteKickSender.Changed -= HandleSenderChanged;
                return;
            }

            RefreshChoices();
        }

        // 名前と自分が対象かは一覧から引くので、一覧が後から届いたときも書き直す
        private void HandleScoreboardChanged(IReadOnlyList<ScoreboardEntry> entries)
        {
            if (this == null)
            {
                GameSignals.Scoreboard.Changed -= HandleScoreboardChanged;
                return;
            }

            RefreshTexts();
        }
    }
}
