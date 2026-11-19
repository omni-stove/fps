using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 出撃の時間と脱出は ExtractionSession が数えるので、ModeSession には時間切れも勝ち条件も持たせず、終わったときに Finish で閉じるだけにする。
    // 失敗 (倒された・時間切れ) を引き分けでなく負けとして MatchFinished に出すため、敵の陣営を勝者に立てる。
    public sealed class ExtractionRunner : ModeRunnerBase
    {
        public const int DefaultHostileSideId = 1;

        [SerializeField, Tooltip("このモードの調整値のアセット。空なら入口 (GameContent) の extraction の JSON、それも無ければ Resources/Tuning/extraction を読む")]
        private ExtractionDefinition _definition;

        [SerializeField, Min(0), Tooltip("敵の陣営の ID。SideMember を持つ敵はこの陣営にする。プレイヤーの陣営と違うこと")]
        private int _hostileSideId = DefaultHostileSideId;

        [SerializeField, Tooltip("このシーンで使う箱の中身の表の JSON。空なら入口 (GameContent) の loot の JSON、それも無ければ Resources/Tuning/loot を読む")]
        private TextAsset _lootTuning;

        [SerializeField, Tooltip("開けられる箱。空ならシーンの LootContainer をすべて使う")]
        private LootContainer[] _containers = Array.Empty<LootContainer>();

        [SerializeField, Tooltip("脱出地点。空ならシーンの ExtractionZone をすべて使う")]
        private ExtractionZone[] _zones = Array.Empty<ExtractionZone>();

        [SerializeField, Tooltip("開ける地点と箱の中身を選ぶ乱数の種。0 なら出撃ごとに変える")]
        private int _seed;

        private readonly List<LootContainer> _activeContainers = new List<LootContainer>();
        private readonly List<ExtractionZone> _activeZones = new List<ExtractionZone>();
        private ExtractionSpec _spec;
        private LootSpec _loot;
        private LootSpec _lootInUse;
        private Stash _stash;
        private StashStore _store;
        private System.Random _random;
        private ExtractionSession _extraction;
        private StashStatus? _publishedStash;

        public static ExtractionSpec Tuning =>
            KitTuning.Load(c => c.ExtractionTuning, ExtractionJson.ResourcePath, ExtractionJson.Parse, ExtractionSpec.Default);

        public static LootSpec LootTuning => KitTuning.Load(c => c.LootTuning, LootJson.ResourcePath, LootJson.Parse, LootSpec.Default);

        public static StashSpec StashTuning => KitTuning.Load(c => c.StashTuning, StashJson.ResourcePath, StashJson.Parse, StashSpec.Default);

        public ExtractionDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public Side HostileSide
        {
            get => new Side(Math.Max(0, _hostileSideId));
            set => _hostileSideId = value.Id;
        }

        public TextAsset LootTuningJson
        {
            get => _lootTuning;
            set => _lootTuning = value;
        }

        public int Seed
        {
            get => _seed;
            set => _seed = value;
        }

        // 入っていなければ Definition、それも無ければ Tuning を読む。試すときに Resources を通さず値を渡せるよう、上書きできるようにしておく。
        public ExtractionSpec Spec
        {
            get => _spec ?? (_definition != null ? _definition.ToSpec() : Tuning);
            set => _spec = value;
        }

        public LootSpec Loot
        {
            get => _loot ?? (_lootTuning != null ? ReadLoot(_lootTuning) : LootTuning);
            set => _loot = value;
        }

        public Stash Stash
        {
            get => _stash;
            set
            {
                if (IsStarted)
                {
                    throw new InvalidOperationException($"{nameof(ExtractionRunner)} '{name}' has already started a raid");
                }

                _stash = value;
            }
        }

        // キーを替えられるのは、テストで本物の持ち物庫を壊さないため。
        public StashStore Store
        {
            get => _store ??= new StashStore();
            set => _store = value;
        }

        public System.Random Random
        {
            get => _random;
            set => _random = value;
        }

        public string InteractKeyLabel { get; set; }

        public ExtractionSession Extraction => _extraction;

        public IReadOnlyList<LootContainer> Containers => _activeContainers;

        public IReadOnlyList<ExtractionZone> Zones => _activeZones;

        public IReadOnlyList<LootContainer> ConfiguredContainers => _containers ?? Array.Empty<LootContainer>();

        public IReadOnlyList<ExtractionZone> ConfiguredZones => _zones ?? Array.Empty<ExtractionZone>();

        public void SetContainers(IReadOnlyList<LootContainer> containers)
        {
            _containers = Copy(containers);
        }

        public void SetZones(IReadOnlyList<ExtractionZone> zones)
        {
            _zones = Copy(zones);
        }

        public void BeginRaid(IReadOnlyList<SideMember> members, IReadOnlyList<SideSpawnPoint> spawnPoints, SideMember localPlayer = null)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException($"{nameof(ExtractionRunner)} '{name}' has already started a raid");
            }

            var session = CreateSession();
            try
            {
                Begin(session, members, spawnPoints, localPlayer);
            }
            catch
            {
                DetachExtraction();
                throw;
            }
        }

        public bool TryLoot()
        {
            var player = LocalPlayer;
            if (_extraction == null || _extraction.IsOver || player == null || !player.IsAlive)
            {
                return false;
            }

            var container = NearestClosedContainer(player.GroundPosition);
            if (container == null)
            {
                return false;
            }

            var table = _lootInUse.Find(container.TableId);
            if (table == null)
            {
                Debug.LogWarning(LootLog.UnknownTable(container.name, container.TableId), container);
            }

            var items = container.Open(table, _random);
            foreach (var item in items)
            {
                _extraction.Pick(item.ItemId, item.Count);
            }

            Debug.Log(LootLog.Opened(container.name, container.TableId, items), container);
            RefreshHud();
            return true;
        }

        public LootContainer NearestClosedContainer(Vector3 position)
        {
            LootContainer nearest = null;
            var best = float.PositiveInfinity;
            foreach (var container in _activeContainers)
            {
                if (container == null || container.IsOpened || !container.IsInReach(position))
                {
                    continue;
                }

                var distance = container.HorizontalDistanceTo(position);
                if (distance < best)
                {
                    best = distance;
                    nearest = container;
                }
            }

            return nearest;
        }

        // 開いた地点と閉じた地点が重なっていれば、開いた方に立っているとみなす。
        public ExtractionZone ZoneAt(Vector3 position)
        {
            ExtractionZone closed = null;
            foreach (var zone in _activeZones)
            {
                if (zone == null || !zone.Contains(position))
                {
                    continue;
                }

                if (_extraction != null && _extraction.IsOpen(zone.PointId))
                {
                    return zone;
                }

                closed ??= zone;
            }

            return closed;
        }

        protected override ModeSession CreateSession()
        {
            DetachExtraction();

            var spec = Spec;
            var hostile = HostileSide;
            if (hostile == PlayerSide)
            {
                throw new InvalidOperationException($"敵の陣営 {hostile.Id} がプレイヤーの陣営と同じです。HostileSide を別の陣営にしてください");
            }

            CollectZones();
            CollectContainers();
            _lootInUse = Loot;
            _random ??= new System.Random(_seed != 0 ? _seed : Environment.TickCount);
            _stash ??= Store.Load(StashTuning, KitTuning.Items);

            var ids = new string[_activeZones.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = _activeZones[i].PointId;
            }

            _extraction = new ExtractionSession(spec, _stash, ExtractionSession.PickOpenPoints(spec, ids, _random));
            _extraction.Ended += OnExtractionEnded;
            ShowZones();

            return new ModeSession(
                new RaidLayout(PlayerSide, hostile),
                float.PositiveInfinity,
                Array.Empty<IScoreRule>(),
                null,
                Array.Empty<IWinCondition>());
        }

        // 外で作った ModeSession を Begin に渡されると、脱出のセッションが無いまま始まって終わり方が無くなる。
        protected override void ValidateSession(ModeSession session)
        {
            if (_extraction == null)
            {
                throw new ArgumentException($"{nameof(ExtractionRunner)} starts only through TryBegin or BeginRaid", nameof(session));
            }
        }

        protected override void OnBegan()
        {
            Debug.Log(ExtractionLog.Started(_extraction), this);
            PublishStash();
        }

        protected override void OnBeforeTick(float deltaSeconds)
        {
            if (_extraction == null || _extraction.IsOver)
            {
                return;
            }

            var player = LocalPlayer;
            var zone = player != null && player.IsAlive ? ZoneAt(player.GroundPosition) : null;
            _extraction.Tick(deltaSeconds, zone != null ? zone.PointId : null);
            ShowZones();
        }

        protected override void OnKill(SideMember killer, SideMember victim)
        {
            if (_extraction != null && victim != null && victim == LocalPlayer)
            {
                _extraction.Die();
            }
        }

        protected override bool OffersInteract
        {
            get
            {
                var player = LocalPlayer;
                return _extraction != null && !_extraction.IsOver && player != null && player.IsAlive
                    && NearestClosedContainer(player.GroundPosition) != null;
            }
        }

        protected override object HudDetail
        {
            get
            {
                if (_extraction == null)
                {
                    return null;
                }

                var player = LocalPlayer;
                var alive = player != null && player.IsAlive;
                var zone = alive ? ZoneAt(player.GroundPosition) : null;
                var onClosed = zone != null && !_extraction.IsOpen(zone.PointId);
                var lootInReach = alive && NearestClosedContainer(player.GroundPosition) != null;
                return new ExtractionStatus(_extraction, onClosed, lootInReach, InteractKeyLabel);
            }
        }

        protected override void OnStopping()
        {
            DetachExtraction();
            if (_publishedStash.HasValue)
            {
                GameSignals.Stash.ClearIfCurrent(_publishedStash);
            }

            _publishedStash = null;
            _activeContainers.Clear();
            _activeZones.Clear();
        }

        private void OnExtractionEnded(ExtractionOutcome outcome)
        {
            Debug.Log(ExtractionLog.Ended(_extraction), this);
            SaveStash();
            PublishStash();
            ShowZones();
            Session?.Finish(outcome == ExtractionOutcome.Extracted ? PlayerSide : HostileSide);
        }

        private void SaveStash()
        {
            try
            {
                Store.Save(_stash);
            }
            catch (PlayerPrefsException e)
            {
                // 保存に失敗しても試合は閉じる。持ち帰った物は今の Play の持ち物庫には残る。
                Debug.LogError($"{LogTag} 持ち物庫を保存できません: {e.Message}", this);
            }
        }

        private void PublishStash()
        {
            if (_stash == null)
            {
                return;
            }

            _publishedStash = _stash.Status;
            GameSignals.Stash.Publish(_publishedStash);
        }

        private void ShowZones()
        {
            foreach (var zone in _activeZones)
            {
                if (zone != null)
                {
                    zone.SetOpen(_extraction != null && !_extraction.IsOver && _extraction.IsOpen(zone.PointId));
                }
            }
        }

        private void CollectZones()
        {
            _activeZones.Clear();
            var source = _zones != null && _zones.Length > 0 ? (IReadOnlyList<ExtractionZone>)_zones : ExtractionZone.FindAll();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var zone in source)
            {
                if (zone == null || _activeZones.Contains(zone))
                {
                    continue;
                }

                var id = zone.PointId.Trim();
                if (id.Length == 0)
                {
                    throw new InvalidOperationException($"ExtractionZone '{zone.name}' に PointId がありません");
                }

                if (!seen.Add(id))
                {
                    throw new InvalidOperationException($"脱出地点の Id '{id}' が 2 つ以上の ExtractionZone で使われています");
                }

                _activeZones.Add(zone);
            }

            if (_activeZones.Count == 0)
            {
                throw new InvalidOperationException("シーンに ExtractionZone が無いため、脱出できません");
            }

            // FindObjectsByType の順は決まっていないので、同じ種で同じ地点が開くよう Id で並べる。
            _activeZones.Sort((a, b) => string.CompareOrdinal(a.PointId, b.PointId));
        }

        private void CollectContainers()
        {
            _activeContainers.Clear();
            var source = _containers != null && _containers.Length > 0 ? (IReadOnlyList<LootContainer>)_containers : LootContainer.FindAll();
            foreach (var container in source)
            {
                if (container != null && !_activeContainers.Contains(container))
                {
                    _activeContainers.Add(container);
                }
            }

            // 近さが同じときに開ける箱が Play ごとに変わらないよう名前で並べる。
            _activeContainers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void DetachExtraction()
        {
            if (_extraction != null)
            {
                _extraction.Ended -= OnExtractionEnded;
            }

            _extraction = null;
        }

        private static LootSpec ReadLoot(TextAsset asset)
        {
            return KitTuning.Read(asset.text, $"TextAsset '{asset.name}'", LootJson.Parse, LootSpec.Default);
        }

        private static T[] Copy<T>(IReadOnlyList<T> items)
        {
            var copy = new T[items != null ? items.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = items[i];
            }

            return copy;
        }

        private sealed class RaidLayout : ITeamLayout
        {
            private readonly Side[] _sides;
            private readonly Side _hostile;

            public RaidLayout(Side player, Side hostile)
            {
                _sides = new[] { player };
                _hostile = hostile;
            }

            public IReadOnlyList<Side> FixedSides => _sides;

            public int? Capacity => null;

            public bool Contains(Side side) => side == _sides[0] || side == _hostile;

            public Side? Assign(int joinIndex, Func<Side, int> memberCount)
            {
                if (joinIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(joinIndex), joinIndex, "joinIndex must not be negative");
                }

                return _sides[0];
            }
        }
    }
}
