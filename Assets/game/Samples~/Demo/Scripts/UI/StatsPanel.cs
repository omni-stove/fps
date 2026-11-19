using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public sealed class StatsPanel : IMainMenuPanel
    {
        public const string PanelClass = "stats";
        public const string TitleClass = "stats__title";
        public const string SectionClass = "stats__section";
        public const string SectionTitleClass = "stats__section-title";
        public const string StatClass = "stats__stat";
        public const string AchievementClass = "stats__achievement";
        public const string AchievementUnlockedClass = "stats__achievement--unlocked";
        public const string AchievementNameClass = "stats__achievement-name";
        public const string AchievementDescriptionClass = "stats__achievement-description";
        public const string BackClass = "stats__back";

        public const string RecordName = "stats-record";
        public const string AchievementsName = "stats-achievements";
        public const string BackButtonName = "stats-back";

        private readonly VisualElement _container;
        private readonly AchievementTracker _tracker;
        private readonly Label _title;
        private readonly Label _recordTitle;
        private readonly VisualElement _record;
        private readonly Label _achievementsTitle;
        private readonly VisualElement _achievements;
        private readonly Button _back;
        private readonly VisualElement _navigationRoot;
        private PlayerProfile _profile;
        private bool _disposed;

        public StatsPanel(VisualElement container, PlayerProfile profile, AchievementSpec achievements)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _tracker = new AchievementTracker(achievements ?? throw new ArgumentNullException(nameof(achievements)));

            container.Clear();
            container.AddToClassList(PanelClass);

            _title = MakeLabel(TitleClass);
            container.Add(_title);

            _record = new VisualElement { name = RecordName };
            _record.AddToClassList(SectionClass);
            _recordTitle = MakeLabel(SectionTitleClass);
            _record.Add(_recordTitle);
            container.Add(_record);

            // 実績は数が増えても戻るのボタンが画面の外へ押し出されないよう、巻物の中に並べる
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList(SectionClass);
            _achievementsTitle = MakeLabel(SectionTitleClass);
            scroll.Add(_achievementsTitle);
            _achievements = new VisualElement { name = AchievementsName };
            scroll.Add(_achievements);
            container.Add(scroll);

            _back = new Button(Cancel) { name = BackButtonName };
            _back.AddToClassList(BackClass);
            container.Add(_back);

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);

            Refresh();
        }

        public event Action Cancelled;

        public VisualElement Container => _container;

        public PlayerProfile Profile => _profile;

        public Button BackButton => _back;

        public string TitleText => _title.text;

        public string AchievementsTitleText => _achievementsTitle.text;

        public IReadOnlyList<string> StatLines => LinesOf(_record, StatClass);

        public IReadOnlyList<string> AchievementLines => LinesOf(_achievements, AchievementNameClass);

        public IReadOnlyList<string> AchievementDescriptions => LinesOf(_achievements, AchievementDescriptionClass);

        // 記録係が無ければ開けない。メインメニューはこの例外を知らせに出す (MainMenuScreen.OpenPanel)。
        // 実績の表は記録係が数えているものを使い、記録係より先に開いても同じ調整値を読む
        public static StatsPanel Open(VisualElement container)
        {
            var progression = ProgressionBootstrap.Recorder
                ?? throw new InvalidOperationException("no progression recorder is installed (ProgressionBootstrap)");
            var achievements = ProgressionBootstrap.Achievements?.Tracker.Spec ?? AchievementRecorder.LoadSpec();
            return new StatsPanel(container, progression.Profile, achievements);
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

            _title.text = StatsFormat.Title;
            _recordTitle.text = StatsFormat.RecordTitle;
            WriteStats(new[]
            {
                StatsFormat.Matches(_profile.Matches),
                StatsFormat.Wins(_profile.Wins),
                StatsFormat.WinRate(_profile.Wins, _profile.Matches),
                StatsFormat.Kills(_profile.Kills),
                StatsFormat.Assists(_profile.Assists),
                StatsFormat.Headshots(_profile.Headshots),
                StatsFormat.MeleeKills(_profile.MeleeKills),
                StatsFormat.PlayTime(_profile.PlaySeconds),
            });

            WriteAchievements();
            _back.text = StatsFormat.Back;
            _back.Focus();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _container.Clear();
            _container.RemoveFromClassList(PanelClass);
            Cancelled = null;
        }

        private void WriteStats(IReadOnlyList<string> lines)
        {
            var rows = _record.Query<Label>(className: StatClass).ToList();
            for (var i = rows.Count; i < lines.Count; i++)
            {
                var row = MakeLabel(StatClass);
                _record.Add(row);
                rows.Add(row);
            }

            for (var i = 0; i < lines.Count; i++)
            {
                rows[i].text = lines[i];
            }
        }

        // 行を毎回作り直すのは、実績の数は開いている間に変わらず、達成済みの印 (クラス) と説明の有無を行ごとに合わせ直すより簡単なため
        private void WriteAchievements()
        {
            var definitions = _tracker.Spec.Achievements;
            var progress = _tracker.Progress(_profile);
            var unlocked = 0;
            foreach (var entry in progress)
            {
                if (entry.Unlocked)
                {
                    unlocked++;
                }
            }

            _achievementsTitle.text = StatsFormat.AchievementsTitle(unlocked, definitions.Count);
            _achievements.Clear();
            if (definitions.Count == 0)
            {
                _achievements.Add(MakeLabel(AchievementNameClass, StatsFormat.NoAchievements));
                return;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var row = new VisualElement();
                row.AddToClassList(AchievementClass);
                row.EnableInClassList(AchievementUnlockedClass, progress[i].Unlocked);
                row.Add(MakeLabel(AchievementNameClass, StatsFormat.Achievement(definitions[i], progress[i])));
                var description = StatsFormat.AchievementDescription(definitions[i], progress[i]);
                if (description.Length > 0)
                {
                    row.Add(MakeLabel(AchievementDescriptionClass, description));
                }

                _achievements.Add(row);
            }
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
        }

        private static IReadOnlyList<string> LinesOf(VisualElement section, string className)
        {
            var lines = new List<string>();
            section.Query<Label>(className: className).ForEach(label => lines.Add(label.text));
            return lines;
        }

        private static Label MakeLabel(string className, string text = "")
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }
}
