using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public sealed class ProfilePanel : IDisposable
    {
        public const string PanelClass = "profile";
        public const string TitleClass = "profile__title";
        public const string LevelClass = "profile__level";
        public const string XpClass = "profile__xp";
        public const string SectionClass = "profile__section";
        public const string SectionTitleClass = "profile__section-title";
        public const string StatClass = "profile__stat";
        public const string UnlockClass = "profile__unlock";
        public const string BackClass = "profile__back";
        public const string AccountStatusClass = "profile__account-status";
        public const string AccountNoticeClass = "profile__account-notice";
        public const string AccountButtonClass = "profile__account-button";

        public const string LevelName = "profile-level";
        public const string TotalXpName = "profile-total-xp";
        public const string XpToNextName = "profile-xp-to-next";
        public const string StatsName = "profile-stats";
        public const string UnlocksName = "profile-unlocks";
        public const string BackButtonName = "profile-back";
        public const string AccountName = "profile-account";
        public const string AccountStatusName = "profile-account-status";
        public const string AccountNoticeName = "profile-account-notice";
        public const string AccountButtonName = "profile-account-button";

        private readonly VisualElement _container;
        private readonly ProgressionLedger _ledger;
        private readonly WeaponCatalog _weapons;
        private readonly ItemCatalog _items;
        private readonly Label _title;
        private readonly Label _level;
        private readonly Label _totalXp;
        private readonly Label _xpToNext;
        private readonly Label _statsTitle;
        private readonly VisualElement _stats;
        private readonly Label _unlocksTitle;
        private readonly VisualElement _unlocks;
        private readonly Button _back;
        private readonly VisualElement _account;
        private readonly Label _accountTitle;
        private readonly Label _accountStatus;
        private readonly Label _accountNotice;
        private readonly Button _accountButton;
        private readonly VisualElement _navigationRoot;
        private PlayerProfile _profile;
        private bool _disposed;

        // アンロックの表は武器とアタッチメントの Id を混ぜて持つので、名前は両方の一覧から引く。どちらも無ければ Id を出す
        public ProfilePanel(
            VisualElement container,
            ProgressionLedger ledger,
            PlayerProfile profile,
            WeaponCatalog weapons = null,
            ItemCatalog items = null)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _weapons = weapons;
            _items = items;

            container.Clear();
            container.AddToClassList(PanelClass);

            _title = MakeLabel(TitleClass);
            container.Add(_title);

            _level = MakeLabel(LevelClass, LevelName);
            container.Add(_level);
            _totalXp = MakeLabel(XpClass, TotalXpName);
            container.Add(_totalXp);
            _xpToNext = MakeLabel(XpClass, XpToNextName);
            container.Add(_xpToNext);

            _stats = new VisualElement { name = StatsName };
            _stats.AddToClassList(SectionClass);
            _statsTitle = MakeLabel(SectionTitleClass);
            _stats.Add(_statsTitle);
            container.Add(_stats);

            _unlocks = new VisualElement { name = UnlocksName };
            _unlocks.AddToClassList(SectionClass);
            _unlocksTitle = MakeLabel(SectionTitleClass);
            _unlocks.Add(_unlocksTitle);
            container.Add(_unlocks);

            _account = new VisualElement { name = AccountName };
            _account.AddToClassList(SectionClass);
            _accountTitle = MakeLabel(SectionTitleClass);
            _account.Add(_accountTitle);
            _accountStatus = MakeLabel(AccountStatusClass, AccountStatusName);
            _account.Add(_accountStatus);
            _accountNotice = MakeLabel(AccountNoticeClass, AccountNoticeName);
            _account.Add(_accountNotice);
            _accountButton = new Button(() => PressAccount()) { name = AccountButtonName };
            _accountButton.AddToClassList(AccountButtonClass);
            _account.Add(_accountButton);
            container.Add(_account);

            _back = new Button(Cancel) { name = BackButtonName };
            _back.AddToClassList(BackClass);
            container.Add(_back);

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationSubmitEvent>(HandleSubmit, TrickleDown.TrickleDown);
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);

            // サインインは画面を開いている間にも終わるので、開いている間も出し直す。
            GameSignals.Account.Changed += HandleAccountChanged;
            GameSignals.AccountControl.Changed += HandleAccountControlChanged;

            Refresh();
        }

        public event Action Cancelled;

        public VisualElement Container => _container;

        public PlayerProfile Profile => _profile;

        public int Level => _ledger.LevelOf(_profile);

        public Button BackButton => _back;

        public string LevelText => _level.text;

        public string TotalXpText => _totalXp.text;

        public string XpToNextText => _xpToNext.text;

        public IReadOnlyList<string> StatLines => LinesOf(_stats, StatClass);

        public IReadOnlyList<string> UnlockLines => LinesOf(_unlocks, UnlockClass);

        public Button AccountButton => _accountButton;

        public bool IsAccountShown => _account.style.display != DisplayStyle.None;

        public bool IsAccountButtonShown => IsAccountShown && _accountButton.style.display != DisplayStyle.None;

        public string AccountStatusText => _accountStatus.text;

        public string AccountNoticeText => _accountNotice.style.display != DisplayStyle.None ? _accountNotice.text : null;

        // 押せないとき (サービスが無い・サインインの最中) は何もせず false を返す。
        public bool PressAccount()
        {
            var control = GameSignals.AccountControl.Value;
            if (_disposed || control == null || !IsAccountButtonShown || !_accountButton.enabledSelf)
            {
                return false;
            }

            var status = CurrentAccountStatus(control);
            return status.CanSignOut ? control.SignOut() : control.SignInWithEpic();
        }

        public void Show(PlayerProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Refresh();
        }

        public void Cancel()
        {
            if (!_disposed)
            {
                Cancelled?.Invoke();
            }
        }

        public void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            var level = Level;
            _title.text = ProgressionFormat.Title;
            _level.text = ProgressionFormat.Level(level);
            _totalXp.text = ProgressionFormat.TotalXp(_profile.Xp);
            _xpToNext.text = level < _ledger.Spec.MaxLevel
                ? ProgressionFormat.XpToNext(_ledger.Spec.Levels[level] - _profile.Xp, level + 1)
                : ProgressionFormat.MaxLevel;

            _statsTitle.text = ProgressionFormat.StatsTitle;
            WriteLines(_stats, StatClass, new[]
            {
                ProgressionFormat.Kills(_profile.Kills),
                ProgressionFormat.Assists(_profile.Assists),
                ProgressionFormat.Deaths(_profile.Deaths),
                ProgressionFormat.KillDeathRatio(_profile.Kills, _profile.Deaths),
                ProgressionFormat.Matches(_profile.Matches),
                ProgressionFormat.Wins(_profile.Wins),
            });

            _unlocksTitle.text = ProgressionFormat.NextUnlocksTitle;
            WriteLines(_unlocks, UnlockClass, NextUnlockLines(level));

            RefreshAccount();

            _back.text = ProgressionFormat.Back;
            FocusBack();
        }

        private void RefreshAccount()
        {
            if (_disposed)
            {
                return;
            }

            var control = GameSignals.AccountControl.Value;
            SetDisplayed(_account, control != null);
            if (control == null)
            {
                return;
            }

            var status = CurrentAccountStatus(control);
            _accountTitle.text = AccountText.Title;
            _accountStatus.text = AccountText.Status(status);

            var notice = AccountText.Failure(status.LastFailure);
            _accountNotice.text = notice ?? string.Empty;
            SetDisplayed(_accountNotice, notice != null);

            // Epic アカウントを使わない設定では押せることが無いので、ボタンごと隠す。
            var showButton = status.CanUseEpicAccount || status.Kind == AccountKind.Epic;
            SetDisplayed(_accountButton, showButton);
            _accountButton.text = status.Kind == AccountKind.Epic ? AccountText.SignOut : AccountText.SignIn;
            _accountButton.SetEnabled(status.CanSignOut || status.CanSignInWithEpic);
        }

        private static AccountStatus CurrentAccountStatus(IAccountControl control)
        {
            return GameSignals.Account.Value ?? control.Status;
        }

        private void HandleAccountChanged(AccountStatus? status)
        {
            RefreshAccount();
        }

        private void HandleAccountControlChanged(IAccountControl control)
        {
            RefreshAccount();
        }

        private static void SetDisplayed(VisualElement element, bool shown)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameSignals.Account.Changed -= HandleAccountChanged;
            GameSignals.AccountControl.Changed -= HandleAccountControlChanged;
            _navigationRoot.UnregisterCallback<NavigationSubmitEvent>(HandleSubmit, TrickleDown.TrickleDown);
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _container.Clear();
            _container.RemoveFromClassList(PanelClass);
            Cancelled = null;
        }

        // 遠い先のアンロックまで並べると一覧が長くなり次の目標が埋もれるので、今のレベルより上で最も近いレベルの物だけを出す
        private List<string> NextUnlockLines(int level)
        {
            var nextLevel = int.MaxValue;
            foreach (var unlock in _ledger.Spec.Unlocks)
            {
                if (unlock.Level > level && unlock.Level < nextLevel)
                {
                    nextLevel = unlock.Level;
                }
            }

            var lines = new List<string>();
            if (nextLevel == int.MaxValue)
            {
                lines.Add(ProgressionFormat.AllUnlocked);
                return lines;
            }

            foreach (var unlock in _ledger.Spec.Unlocks)
            {
                if (unlock.Level == nextLevel)
                {
                    lines.Add(ProgressionFormat.Unlock(unlock.Level, NameOf(unlock.Id)));
                }
            }

            return lines;
        }

        private string NameOf(string id)
        {
            if (_weapons != null && _weapons.TryGet(id, out var weapon))
            {
                return GameText.Resolve(DisplayNames.Of(weapon));
            }

            if (_items != null && _items.TryFind(id, out var item))
            {
                return GameText.Resolve(DisplayNames.Of(item.NameKey, item.Id));
            }

            return id;
        }

        private void FocusBack()
        {
            _back.Focus();

            // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (_back.panel != null && _back.focusController?.focusedElement != _back)
            {
                _back.schedule.Execute(() =>
                {
                    if (!_disposed)
                    {
                        _back.Focus();
                    }
                });
            }
        }

        private void HandleSubmit(NavigationSubmitEvent evt)
        {
            // フォーカスしている戻るのボタンにも届くと Cancelled が二重に走るので、ここで止める
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            if (_navigationRoot.focusController?.focusedElement == _accountButton)
            {
                PressAccount();
                return;
            }

            Cancel();
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
        }

        private static void WriteLines(VisualElement section, string className, IReadOnlyList<string> lines)
        {
            var rows = section.Query<Label>(className: className).ToList();
            for (var i = rows.Count; i < lines.Count; i++)
            {
                var row = MakeLabel(className);
                section.Add(row);
                rows.Add(row);
            }

            for (var i = rows.Count - 1; i >= lines.Count; i--)
            {
                rows[i].RemoveFromHierarchy();
                rows.RemoveAt(i);
            }

            for (var i = 0; i < lines.Count; i++)
            {
                rows[i].text = lines[i];
            }
        }

        private static IReadOnlyList<string> LinesOf(VisualElement section, string className)
        {
            var lines = new List<string>();
            section.Query<Label>(className: className).ForEach(label => lines.Add(label.text));
            return lines;
        }

        private static Label MakeLabel(string className, string name = null)
        {
            var label = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            if (name != null)
            {
                label.name = name;
            }

            label.AddToClassList(className);
            return label;
        }
    }
}
