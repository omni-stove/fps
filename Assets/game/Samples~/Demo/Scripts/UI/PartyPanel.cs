using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public sealed class PartyPanel : IDisposable
    {
        public const string LogTag = "[Party]";

        public const string LayoutResourcePath = "Menu/Party";

        public const string PanelName = "party";
        public const string StageName = "party-stage";
        public const string LeaderNoteName = "party-leader-note";
        public const string InviteName = "party-invite";
        public const string InviteCodeName = "party-invite-code";
        public const string MembersName = "party-members";
        public const string NoMembersName = "party-no-members";
        public const string CreateButtonName = "party-create";
        public const string CodeFieldName = "party-code-field";
        public const string JoinButtonName = "party-join";
        public const string LeaveButtonName = "party-leave";
        public const string NoticeName = "party-notice";
        public const string BackButtonName = "party-back";

        public const string MemberClass = "party__member";
        public const string LeaderMemberClass = "party__member--leader";
        public const string MemberNameClass = "party__member-name";
        public const string LeaderBadgeClass = "party__leader-badge";
        public const string ErrorNoticeClass = "party__notice--error";

        public const string StageNoneKey = "party.stage.none";
        public const string StageInPartyKey = "party.stage.inParty";
        public const string StageSearchingKey = "party.stage.searching";
        public const string StageJoiningKey = "party.stage.joining";
        public const string LeaderKey = "party.leader";
        public const string CodePlaceholderKey = "party.codePlaceholder";
        public const string UnavailableKey = "party.unavailable";
        public const string CodeRequiredKey = "party.codeRequired";
        public const string InvalidCodeKey = "party.invalidCode";
        public const string FailedKey = "party.failed";

        private readonly VisualElement _container;
        private readonly LocalizedTexts _texts;
        private readonly Label _stage;
        private readonly VisualElement _leaderNote;
        private readonly VisualElement _invite;
        private readonly TextField _inviteCode;
        private readonly VisualElement _members;
        private readonly VisualElement _noMembers;
        private readonly Button _create;
        private readonly TextField _codeField;
        private readonly Button _join;
        private readonly Button _leave;
        private readonly Label _notice;
        private readonly Button _back;
        private readonly VisualElement _navigationRoot;
        private readonly List<string> _memberNames = new List<string>();
        private readonly List<VisualElement> _memberRows = new List<VisualElement>();
        private LocalizedText? _noticeSource;
        private bool _noticeIsError;
        private bool _disposed;

        public PartyPanel(VisualElement container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));

            var layout = Resources.Load<VisualTreeAsset>(LayoutResourcePath);
            if (layout == null)
            {
                throw new InvalidOperationException(
                    $"party layout 'Resources/{LayoutResourcePath}.uxml' was not found (expected under UI/Resources of the Demo sample)");
            }

            container.Clear();
            layout.CloneTree(container);
            Require<VisualElement>(PanelName);
            _stage = Require<Label>(StageName);
            _leaderNote = Require<VisualElement>(LeaderNoteName);
            _invite = Require<VisualElement>(InviteName);
            _inviteCode = Require<TextField>(InviteCodeName);
            _members = Require<VisualElement>(MembersName);
            _noMembers = Require<VisualElement>(NoMembersName);
            _create = Require<Button>(CreateButtonName);
            _codeField = Require<TextField>(CodeFieldName);
            _join = Require<Button>(JoinButtonName);
            _leave = Require<Button>(LeaveButtonName);
            _notice = Require<Label>(NoticeName);
            _back = Require<Button>(BackButtonName);

            // 読むだけの欄にするのは、招待のコードを選んで写せるようにするため。
            _inviteCode.isReadOnly = true;
            _codeField.maxLength = ConnectionExtras.MaxPartyIdLength;

            _create.clicked += HandleCreateClicked;
            _join.clicked += HandleJoinClicked;
            _leave.clicked += HandleLeaveClicked;
            _back.clicked += Cancel;

            _texts = LocalizedTexts.Bind(container);
            RefreshPlaceholder();

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);

            GameSignals.Party.Changed += HandlePartyChanged;
            GameSignals.PartyControl.Changed += HandleControlChanged;
            Localizer.Changed += HandleLanguageChanged;

            Render();
            FocusFirstEnabled();
        }

        public event Action Cancelled;

        public VisualElement Container => _container;

        public PartyStatus Status => GameSignals.Party.Value ?? default;

        public IPartyControl Control => GameSignals.PartyControl.Value;

        public Button CreateButton => _create;

        public Button JoinButton => _join;

        public Button LeaveButton => _leave;

        public Button BackButton => _back;

        public TextField CodeField => _codeField;

        public string StageText => _stage.text;

        public bool IsLeaderNoteShown => _leaderNote.style.display != DisplayStyle.None;

        public string InviteCode => _invite.style.display != DisplayStyle.None ? _inviteCode.value : string.Empty;

        public IReadOnlyList<string> MemberNames => _memberNames;

        public IReadOnlyList<VisualElement> MemberRows => _memberRows;

        public string Notice => _notice.style.display != DisplayStyle.None ? _notice.text : null;

        public bool IsNoticeError => _notice.ClassListContains(ErrorNoticeClass);

        public bool CanCreate => !_disposed && Control != null && Status.Stage == PartyStage.None;

        public bool CanJoin => CanCreate;

        public bool CanLeave => !_disposed && Control != null && Status.Stage != PartyStage.None;

        public bool Create()
        {
            if (!CanCreate)
            {
                return false;
            }

            return Call("create", control => control.Create());
        }

        // 空や使えない文字のコードは、サービスへ問い合わせる前にここで止めて、どう直せばよいかを出す。
        public bool Join()
        {
            if (!CanJoin)
            {
                return false;
            }

            var code = (_codeField.value ?? string.Empty).Trim();
            if (code.Length == 0)
            {
                ShowNotice(new LocalizedText(CodeRequiredKey), true);
                return false;
            }

            if (!ConnectionExtras.IsValidField(code, ConnectionExtras.MaxPartyIdLength))
            {
                ShowNotice(new LocalizedText(InvalidCodeKey, ConnectionExtras.MaxPartyIdLength), true);
                return false;
            }

            return Call("join", control => control.Join(code));
        }

        public bool Leave()
        {
            if (!CanLeave)
            {
                return false;
            }

            return Call("leave", control => control.Leave());
        }

        public void Cancel()
        {
            if (!_disposed)
            {
                Cancelled?.Invoke();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameSignals.Party.Changed -= HandlePartyChanged;
            GameSignals.PartyControl.Changed -= HandleControlChanged;
            Localizer.Changed -= HandleLanguageChanged;
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _create.clicked -= HandleCreateClicked;
            _join.clicked -= HandleJoinClicked;
            _leave.clicked -= HandleLeaveClicked;
            _back.clicked -= Cancel;
            _container.Clear();
            Cancelled = null;
        }

        private bool Call(string action, Action<IPartyControl> call)
        {
            var control = Control;
            try
            {
                call(control);
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} could not {action} the party: {e.Message}");
                Debug.LogException(e);
                ShowNotice(new LocalizedText(FailedKey), true);
                return false;
            }

            Debug.Log($"{LogTag} asked to {action} the party");
            HideNotice();
            return true;
        }

        private void Render()
        {
            if (_disposed)
            {
                return;
            }

            var control = Control;
            var status = Status;
            _stage.text = Localizer.Resolve(StageTextOf(status));
            _leaderNote.style.display = status.Stage != PartyStage.None && status.IsLocalLeader ? DisplayStyle.Flex : DisplayStyle.None;

            _inviteCode.SetValueWithoutNotify(status.InviteCode);
            _invite.style.display = status.HasInviteCode ? DisplayStyle.Flex : DisplayStyle.None;

            RenderMembers(status.Members);

            _create.SetEnabled(CanCreate);
            _codeField.SetEnabled(CanJoin);
            _join.SetEnabled(CanJoin);
            _leave.SetEnabled(CanLeave);

            if (control == null)
            {
                ShowNotice(new LocalizedText(UnavailableKey), false);
            }
            else if (_noticeSource.HasValue && _noticeSource.Value.Key == UnavailableKey)
            {
                HideNotice();
            }
            else
            {
                RefreshNotice();
            }
        }

        private static LocalizedText StageTextOf(PartyStatus status)
        {
            switch (status.Stage)
            {
                case PartyStage.InParty:
                    return new LocalizedText(StageInPartyKey, status.Members.Count);
                case PartyStage.Searching:
                    return new LocalizedText(StageSearchingKey);
                case PartyStage.Joining:
                    return new LocalizedText(StageJoiningKey);
                default:
                    return new LocalizedText(StageNoneKey);
            }
        }

        // メンバーは数人で、変わるのは知らせが届いたときと言語が変わったときだけなので、差分を取らずに作り直す。
        private void RenderMembers(IReadOnlyList<PartyMember> members)
        {
            _members.Clear();
            _memberRows.Clear();
            _memberNames.Clear();
            var leader = Localizer.Get(LeaderKey);
            foreach (var member in members)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList(MemberClass);
                row.EnableInClassList(LeaderMemberClass, member.IsLeader);

                // 名前はプレイヤーが付けたものなので、タグを色や大きさとして読ませない。
                var name = new Label(member.Name) { pickingMode = PickingMode.Ignore, enableRichText = false };
                name.AddToClassList(MemberNameClass);
                row.Add(name);

                if (member.IsLeader)
                {
                    var badge = new Label(leader) { pickingMode = PickingMode.Ignore };
                    badge.AddToClassList(LeaderBadgeClass);
                    row.Add(badge);
                }

                _members.Add(row);
                _memberRows.Add(row);
                _memberNames.Add(member.Name);
            }

            _noMembers.style.display = members.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ShowNotice(LocalizedText text, bool isError)
        {
            _noticeSource = text;
            _noticeIsError = isError;
            RefreshNotice();
        }

        private void HideNotice()
        {
            _noticeSource = null;
            _noticeIsError = false;
            RefreshNotice();
        }

        private void RefreshNotice()
        {
            if (!_noticeSource.HasValue)
            {
                _notice.text = string.Empty;
                _notice.style.display = DisplayStyle.None;
                _notice.RemoveFromClassList(ErrorNoticeClass);
                return;
            }

            _notice.text = Localizer.Resolve(_noticeSource.Value);
            _notice.EnableInClassList(ErrorNoticeClass, _noticeIsError);
            _notice.style.display = DisplayStyle.Flex;
        }

        private void RefreshPlaceholder()
        {
            _codeField.textEdition.placeholder = Localizer.Get(CodePlaceholderKey);
        }

        // 押したボタンが状態の変化で使えなくなるとフォーカスが消え、ゲームパッドで動かせなくなるので、使えるボタンへ移す。
        private void KeepFocus()
        {
            var focused = _container.focusController?.focusedElement as VisualElement;
            if (focused != null && _container.Contains(focused) && focused.enabledInHierarchy)
            {
                return;
            }

            if (focused == null || _container.Contains(focused))
            {
                FocusFirstEnabled();
            }
        }

        private void FocusFirstEnabled()
        {
            var target = _create.enabledSelf ? _create : _leave.enabledSelf ? _leave : _back;
            target.Focus();

            // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (target.panel != null && target.focusController?.focusedElement != target)
            {
                target.schedule.Execute(() =>
                {
                    if (!_disposed && target.enabledInHierarchy)
                    {
                        target.Focus();
                    }
                });
            }
        }

        private void HandleCreateClicked()
        {
            Create();
        }

        private void HandleJoinClicked()
        {
            Join();
        }

        private void HandleLeaveClicked()
        {
            Leave();
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
        }

        private void HandlePartyChanged(PartyStatus? status)
        {
            Render();
            KeepFocus();
        }

        private void HandleControlChanged(IPartyControl control)
        {
            Render();
            KeepFocus();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            if (_disposed)
            {
                return;
            }

            _texts.Refresh();
            RefreshPlaceholder();
            Render();
        }

        private T Require<T>(string name)
            where T : VisualElement
        {
            var element = _container.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"party layout 'Resources/{LayoutResourcePath}.uxml' has no {typeof(T).Name} named '{name}'");
            }

            return element;
        }
    }
}
