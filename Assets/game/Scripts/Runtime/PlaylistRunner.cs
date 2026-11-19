using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlaylistRunner : MonoBehaviour
    {
        private static readonly IReadOnlyDictionary<string, GameRules> _noRules =
            new Dictionary<string, GameRules>(StringComparer.Ordinal);

        private Func<string, bool> _sceneLoader;
        private IReadOnlyList<GameRulesMutator> _mutatorsOverride;
        private int? _seed;

        private PlaylistCursor _cursor;
        private IReadOnlyDictionary<string, GameRules> _rules = _noRules;
        private IReadOnlyList<GameRulesMutator> _mutators = MutatorJson.Empty;
        private bool _subscribed;
        private bool _advancePending;
        private float _remainingSeconds;

        public Func<string, bool> SceneLoader
        {
            get => _sceneLoader;
            set => _sceneLoader = value;
        }

        public IReadOnlyList<GameRulesMutator> Mutators
        {
            get => _mutatorsOverride;
            set => _mutatorsOverride = value;
        }

        public int? Seed
        {
            get => _seed;
            set => _seed = value;
        }

        public bool IsRunning => _cursor != null;

        public Playlist Playlist => _cursor?.Playlist;

        public PlaylistCursor Cursor => _cursor;

        public PlaylistEntry CurrentEntry => _cursor?.Current;

        public bool IsAdvancePending => _advancePending;

        public float RemainingDelaySeconds => _advancePending ? Mathf.Max(0f, _remainingSeconds) : 0f;

        public void Begin(PlaylistDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Begin(definition.ToPlaylist(), definition.RulesByName());
        }

        public void Begin(string playlistId, IReadOnlyDictionary<string, GameRules> rules)
        {
            Begin(playlistId, KitTuning.Playlists, rules);
        }

        public void Begin(string playlistId, IReadOnlyList<Playlist> playlists, IReadOnlyDictionary<string, GameRules> rules)
        {
            if (playlists == null)
            {
                throw new ArgumentNullException(nameof(playlists));
            }

            var playlist = Playlist.Find(playlists, playlistId);
            if (playlist == null)
            {
                var known = new List<string>(playlists.Count);
                foreach (var candidate in playlists)
                {
                    known.Add(candidate.Id);
                }

                var list = known.Count == 0 ? "(none)" : string.Join(", ", known);
                throw new ArgumentException($"no playlist has the id '{playlistId}'; known ids are {list}", nameof(playlistId));
            }

            Begin(playlist, rules);
        }

        public void Begin(Playlist playlist, IReadOnlyDictionary<string, GameRules> rules)
        {
            if (playlist == null)
            {
                throw new ArgumentNullException(nameof(playlist));
            }

            rules ??= _noRules;

            // 知らないルール名で始めると、その項目まで回って初めて気づくので、始める前にすべての項目を確かめる。
            var missing = new List<string>();
            foreach (var entry in playlist.Entries)
            {
                if (entry.HasRules && (!rules.TryGetValue(entry.Rules, out var found) || found == null) && !missing.Contains(entry.Rules))
                {
                    missing.Add(entry.Rules);
                }
            }

            if (missing.Count > 0)
            {
                throw new ArgumentException(PlaylistLog.UnknownRules(playlist, missing, rules.Keys), nameof(rules));
            }

            Stop();

            _rules = rules;
            _mutators = _mutatorsOverride ?? KitTuning.Mutators;
            _cursor = new PlaylistCursor(playlist, _seed ?? Environment.TickCount);
            Subscribe();

            // EditMode のテストでは DontDestroyOnLoad を呼べない。
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            Debug.Log(PlaylistLog.Started(playlist), this);
            LoadCurrent();
        }

        public void Advance()
        {
            if (_cursor == null)
            {
                throw new InvalidOperationException("the playlist runner has not begun a playlist");
            }

            _advancePending = false;
            _cursor.Next();
            LoadCurrent();
        }

        public void Tick(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!_advancePending)
            {
                return;
            }

            _remainingSeconds -= deltaSeconds;
            if (_remainingSeconds <= 0f)
            {
                Advance();
            }
        }

        // 止めた後もルールとミューテーターが残ると、メニューから始めた次の試合に効くので、ここで積んだものを下ろす。
        public void Stop()
        {
            if (_cursor == null)
            {
                return;
            }

            var playlist = _cursor.Playlist;
            Unsubscribe();
            _cursor = null;
            _advancePending = false;
            _remainingSeconds = 0f;
            _rules = _noRules;
            _mutators = MutatorJson.Empty;
            GameRulesOverride.Clear();
            GameRulesOverride.ClearMutators();
            Debug.Log(PlaylistLog.Stopped(playlist), this);
        }

        private void Update()
        {
            // 試合の終わりにスローモーションや一時停止が掛かっても待ち時間が延びないよう、時間の倍率を受けない秒で数える。
            Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnMatchFinished(MatchResult result)
        {
            // 1 つの試合で終わりが 2 度届いても (モードと MatchRunner が両方流すなど)、項目を 2 つ進めない。
            if (_cursor == null || _advancePending)
            {
                return;
            }

            _advancePending = true;
            _remainingSeconds = _cursor.Playlist.NextDelaySeconds;
            Debug.Log(PlaylistLog.MatchFinished(_cursor.Playlist, _remainingSeconds), this);
        }

        // 待っている間に R の再開などでシーンが読み直されたら、やり直した試合の途中で次へ進まないよう待ちを下ろす。
        // Runner 自身が読むときは Advance が先に待ちを下ろしているので、ここに残る待ちは外から読まれたシーンのものである。
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CancelPendingAdvance();
        }

        public void CancelPendingAdvance()
        {
            _advancePending = false;
            _remainingSeconds = 0f;
        }

        private void LoadCurrent()
        {
            var playlist = _cursor.Playlist;
            // 混ぜた並びは周をまたぐと組み直されるので、試した回数ではなく試した位置で全項目を確かめる。
            var tried = new HashSet<int>();
            while (tried.Count < playlist.Entries.Count)
            {
                var entry = _cursor.Current;
                Apply(entry);
                if (LoadScene(entry.Scene))
                {
                    Debug.Log(PlaylistLog.Loading(_cursor), this);
                    return;
                }

                Debug.LogError(PlaylistLog.SceneNotLoaded(playlist, _cursor.Position, entry.Scene), this);
                tried.Add(_cursor.Position);
                if (tried.Count == playlist.Entries.Count)
                {
                    break;
                }

                do
                {
                    _cursor.Next();
                }
                while (tried.Contains(_cursor.Position));
            }

            Debug.LogError(PlaylistLog.NoSceneLoaded(playlist), this);
            Stop();
        }

        // GameBootstrap がシーンの始めに GameRulesOverride を読むので、シーンを読む前に積む。
        // 知らない Id で回すのを止めず、理由を出してその 1 つだけを飛ばす (GameBootstrap の SceneMutators と同じ扱い)。
        private void Apply(PlaylistEntry entry)
        {
            GameRulesOverride.Clear();
            GameRulesOverride.ClearMutators();
            if (entry.HasRules)
            {
                GameRulesOverride.Set(_rules[entry.Rules]);
            }

            foreach (var id in entry.MutatorIds)
            {
                var mutator = GameRulesMutator.Find(_mutators, id);
                if (mutator == null)
                {
                    Debug.LogError(PlaylistLog.UnknownMutator(_cursor.Playlist, _cursor.Position, id, _mutators), this);
                    continue;
                }

                GameRulesOverride.PushMutator(mutator);
            }
        }

        private bool LoadScene(string scene)
        {
            if (_sceneLoader != null)
            {
                return _sceneLoader(scene);
            }

            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                return false;
            }

            SceneManager.LoadScene(scene);
            return true;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            CombatEvents.MatchFinished += OnMatchFinished;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            CombatEvents.MatchFinished -= OnMatchFinished;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _subscribed = false;
        }
    }
}
