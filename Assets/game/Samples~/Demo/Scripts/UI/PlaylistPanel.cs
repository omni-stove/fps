using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // playlists.json は表示名を持たないので、名前は Id から組んだキーで引き、表に無ければ Id をそのまま出す。
    public sealed class PlaylistPanel : IMainMenuPanel
    {
        public const string LogTag = "[PlaylistPanel]";

        public const string MenuRunnerObjectName = "PlaylistRunner";

        private static PlaylistRunner _menuRunner;

        public const string PanelClass = "playlist-select";
        public const string TitleClass = "playlist-select__title";
        public const string ListClass = "playlist-select__list";
        public const string PlaylistClass = "playlist-select__playlist";
        public const string PlaylistNameClass = "playlist-select__playlist-name";
        public const string OrderClass = "playlist-select__order";
        public const string EntryClass = "playlist-select__entry";
        public const string EmptyClass = "playlist-select__empty";
        public const string BackClass = "playlist-select__back";
        public const string StatusClass = "playlist-select__status";
        public const string ErrorStatusClass = "playlist-select__status--error";

        public const string ListName = "playlist-list";
        public const string EmptyName = "playlist-empty";
        public const string BackButtonName = "playlist-back";
        public const string StatusName = "playlist-status";

        public const string TitleKey = "playlist.title";
        public const string EmptyKey = "playlist.empty";
        public const string BackKey = "playlist.back";
        public const string SequentialKey = "playlist.order.sequential";
        public const string ShuffleKey = "playlist.order.shuffle";
        public const string EntryKey = "playlist.entry";
        public const string EntryWithMutatorsKey = "playlist.entryWithMutators";
        public const string MutatorSeparatorKey = "playlist.mutatorSeparator";
        public const string StartingKey = "playlist.starting";
        public const string CannotStartKey = "playlist.cannotStart";
        public const string NoSceneKey = "playlist.noScene";

        public const string NameKeyPrefix = "playlist.name.";
        public const string ModeKeyPrefix = "playlist.mode.";
        public const string MutatorKeyPrefix = "playlist.mutator.";

        private static readonly IReadOnlyDictionary<string, GameRules> _noRules =
            new Dictionary<string, GameRules>(StringComparer.Ordinal);

        private readonly VisualElement _container;
        private readonly PlaylistRunner _runner;
        private readonly IReadOnlyList<Playlist> _playlists;
        private readonly IReadOnlyDictionary<string, GameRules> _rules;
        private readonly Label _title;
        private readonly Label _empty;
        private readonly Button _back;
        private readonly Label _status;
        private readonly List<Button> _playlistButtons = new List<Button>();
        private readonly List<Label> _playlistNames = new List<Label>();
        private readonly List<Label> _orders = new List<Label>();
        private readonly List<List<Label>> _entries = new List<List<Label>>();
        private readonly VisualElement _navigationRoot;
        private LocalizedText? _statusSource;
        private bool _statusIsError;
        private bool _started;
        private bool _disposed;

        public PlaylistPanel(
            VisualElement container,
            PlaylistRunner runner,
            IReadOnlyList<Playlist> playlists = null,
            IReadOnlyDictionary<string, GameRules> rules = null)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            _runner = runner;
            _playlists = playlists ?? KitTuning.Playlists;
            _rules = rules ?? _noRules;
            for (var i = 0; i < _playlists.Count; i++)
            {
                if (_playlists[i] == null)
                {
                    throw new ArgumentException($"playlist {i} is null", nameof(playlists));
                }
            }

            container.Clear();
            container.AddToClassList(PanelClass);

            _title = new Label(string.Empty);
            _title.AddToClassList(TitleClass);
            container.Add(_title);

            var list = new VisualElement { name = ListName };
            list.AddToClassList(ListClass);
            container.Add(list);
            for (var i = 0; i < _playlists.Count; i++)
            {
                list.Add(BuildPlaylist(i));
            }

            _empty = new Label(string.Empty) { name = EmptyName };
            _empty.AddToClassList(EmptyClass);
            _empty.style.display = _playlistButtons.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(_empty);

            _status = new Label(string.Empty) { name = StatusName };
            _status.AddToClassList(StatusClass);
            _status.style.display = DisplayStyle.None;
            container.Add(_status);

            _back = new Button { name = BackButtonName };
            _back.clicked += Cancel;
            _back.AddToClassList(BackClass);
            container.Add(_back);

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            Localizer.Changed += HandleLanguageChanged;

            RefreshTexts();
            FocusFirst();
        }

        public event Action<Playlist> Started;

        public event Action Cancelled;

        public VisualElement Container => _container;

        public PlaylistRunner Runner => _runner;

        public IReadOnlyList<Playlist> Playlists => _playlists;

        public int Count => _playlistButtons.Count;

        public string Title => _title.text;

        public string EmptyNotice => _empty.style.display == DisplayStyle.None ? null : _empty.text;

        public string Status => _status.style.display == DisplayStyle.None ? null : _status.text;

        public bool IsStatusError => _status.ClassListContains(ErrorStatusClass);

        public bool HasStarted => _started;

        public Button BackButton => _back;

        public Button PlaylistButton(int index)
        {
            return _playlistButtons[index];
        }

        public string PlaylistName(int index)
        {
            return _playlistNames[index].text;
        }

        public string OrderText(int index)
        {
            return _orders[index].text;
        }

        public IReadOnlyList<string> EntryTexts(int index)
        {
            var lines = _entries[index];
            var texts = new string[lines.Count];
            for (var i = 0; i < lines.Count; i++)
            {
                texts[i] = lines[i].text;
            }

            return texts;
        }

        // 始めたらシーンが替わるまで二度押しで別のプレイリストを始めないよう、成功した後は何も受けない。
        public bool Start(int index)
        {
            if (_disposed || _started)
            {
                return false;
            }

            if (index < 0 || index >= _playlists.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"the panel lists {_playlists.Count} playlists");
            }

            var playlist = _playlists[index];
            Debug.Log($"{LogTag} starting playlist '{playlist.Id}'");
            try
            {
                _runner.Begin(playlist, _rules);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} could not start playlist '{playlist.Id}': {e.Message}");
                ShowStatus(new LocalizedText(CannotStartKey, NameOf(playlist)), true);
                return false;
            }

            // Runner は読めるシーンが 1 つも無いと例外ではなく理由をログに出して止まるので、始まったかは IsRunning で見る
            if (!_runner.IsRunning)
            {
                ShowStatus(new LocalizedText(NoSceneKey, NameOf(playlist)), true);
                return false;
            }

            _started = true;
            foreach (var button in _playlistButtons)
            {
                button.SetEnabled(false);
            }

            _back.SetEnabled(false);
            ShowStatus(new LocalizedText(StartingKey, NameOf(playlist)), false);
            Started?.Invoke(playlist);
            return true;
        }

        public void Cancel()
        {
            if (!_disposed && !_started)
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
            Localizer.Changed -= HandleLanguageChanged;
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _back.clicked -= Cancel;
            _container.Clear();
            _container.RemoveFromClassList(PanelClass);
            Started = null;
            Cancelled = null;
        }

        // メインメニューの既定の画面 (MainMenuPanels) として開く。ルールの表は渡さないので、ルール名を持つ項目のあるプレイリストは始めるときに理由を出して断る。
        public static PlaylistPanel Open(VisualElement container)
        {
            var panel = new PlaylistPanel(container, MenuRunner());
            panel.Started += _ => StopOnReturnToMenu();
            return panel;
        }

        // 回す役はシーンをまたいで残る (DontDestroyOnLoad) ので、メニューへ戻るたびに作り足さず、残っているものを使い回す
        public static PlaylistRunner MenuRunner()
        {
            if (_menuRunner == null)
            {
                _menuRunner = UnityEngine.Object.FindAnyObjectByType<PlaylistRunner>();
            }

            if (_menuRunner == null)
            {
                _menuRunner = new GameObject(MenuRunnerObjectName).AddComponent<PlaylistRunner>();
            }

            return _menuRunner;
        }

        // 外してから付けるのは、何度始めても 1 度だけ呼ばれ、Play のたびに MenuNavigator が購読を空にしても付け直せるようにするため
        private static void StopOnReturnToMenu()
        {
            MenuNavigator.ReturningToMenu -= HandleReturningToMenu;
            MenuNavigator.ReturningToMenu += HandleReturningToMenu;
        }

        // 途中でメニューへ戻ったら、プレイリストが積んだルールとミューテーターが、次にメニューから始める試合に効かないよう止める
        private static void HandleReturningToMenu()
        {
            MenuNavigator.ReturningToMenu -= HandleReturningToMenu;
            if (_menuRunner != null)
            {
                _menuRunner.Stop();
            }
        }

        public static string NameKeyOf(string playlistId)
        {
            return NameKeyPrefix + playlistId;
        }

        // シーンはパスでも名前でも書けるので、どちらで書いても同じキーになるようファイル名だけを使う。
        public static string ModeKeyOf(string scene)
        {
            return ModeKeyPrefix + SceneNameOf(scene);
        }

        public static string MutatorKeyOf(string mutatorId)
        {
            return MutatorKeyPrefix + mutatorId;
        }

        public static string OrderKey(PlaylistOrder order)
        {
            return order == PlaylistOrder.Shuffle ? ShuffleKey : SequentialKey;
        }

        public static LocalizedText NameOf(Playlist playlist)
        {
            if (playlist == null)
            {
                throw new ArgumentNullException(nameof(playlist));
            }

            return KeyOrLiteral(NameKeyOf(playlist.Id), playlist.Id);
        }

        public static LocalizedText ModeOf(PlaylistEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return KeyOrLiteral(ModeKeyOf(entry.Scene), SceneNameOf(entry.Scene));
        }

        public static LocalizedText MutatorOf(string mutatorId)
        {
            if (mutatorId == null)
            {
                throw new ArgumentNullException(nameof(mutatorId));
            }

            return KeyOrLiteral(MutatorKeyOf(mutatorId), mutatorId);
        }

        public static string EntryText(PlaylistEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var mode = Localizer.Resolve(ModeOf(entry));
            if (entry.MutatorIds.Count == 0)
            {
                return Localizer.Format(EntryKey, mode);
            }

            var names = new string[entry.MutatorIds.Count];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = Localizer.Resolve(MutatorOf(entry.MutatorIds[i]));
            }

            return Localizer.Format(EntryWithMutatorsKey, mode, string.Join(Localizer.Get(MutatorSeparatorKey), names));
        }

        private static string SceneNameOf(string scene)
        {
            var name = Path.GetFileNameWithoutExtension(scene ?? string.Empty);
            return string.IsNullOrEmpty(name) ? scene ?? string.Empty : name;
        }

        // 足したばかりのプレイリストやミューテーターで名前の欄が空にならず、表に無い警告も出し続けないよう、無いキーは引かずに Id を出す。
        private static LocalizedText KeyOrLiteral(string key, string fallback)
        {
            return Localizer.TryGet(key, out _) ? new LocalizedText(key) : LocalizedText.Literal(fallback);
        }

        private VisualElement BuildPlaylist(int index)
        {
            var playlist = _playlists[index];
            var button = new Button(() => Start(index)) { text = string.Empty };
            button.AddToClassList(PlaylistClass);

            var playlistName = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            playlistName.AddToClassList(PlaylistNameClass);
            button.Add(playlistName);

            var order = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            order.AddToClassList(OrderClass);
            button.Add(order);

            var lines = new List<Label>();
            for (var i = 0; i < playlist.Entries.Count; i++)
            {
                var line = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
                line.AddToClassList(EntryClass);
                button.Add(line);
                lines.Add(line);
            }

            _playlistButtons.Add(button);
            _playlistNames.Add(playlistName);
            _orders.Add(order);
            _entries.Add(lines);
            return button;
        }

        private void RefreshTexts()
        {
            _title.text = Localizer.Get(TitleKey);
            _empty.text = Localizer.Get(EmptyKey);
            _back.text = Localizer.Get(BackKey);
            for (var i = 0; i < _playlistButtons.Count; i++)
            {
                var playlist = _playlists[i];
                _playlistNames[i].text = Localizer.Resolve(NameOf(playlist));
                _orders[i].text = Localizer.Get(OrderKey(playlist.Order));
                for (var j = 0; j < playlist.Entries.Count; j++)
                {
                    _entries[i][j].text = EntryText(playlist.Entries[j]);
                }
            }

            RefreshStatus();
        }

        private void ShowStatus(LocalizedText text, bool isError)
        {
            _statusSource = text;
            _statusIsError = isError;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (!_statusSource.HasValue)
            {
                _status.text = string.Empty;
                _status.RemoveFromClassList(ErrorStatusClass);
                _status.style.display = DisplayStyle.None;
                return;
            }

            _status.text = Localizer.Resolve(_statusSource.Value);
            _status.EnableInClassList(ErrorStatusClass, _statusIsError);
            _status.style.display = DisplayStyle.Flex;
        }

        private void FocusFirst()
        {
            var target = _playlistButtons.Count > 0 ? _playlistButtons[0] : _back;
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

        private void HandleCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            if (!_disposed)
            {
                RefreshTexts();
            }
        }
    }
}
