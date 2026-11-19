using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 誰が箱を開けるかと HUD への出し方はモードの Runner が決めるので、ここでは落とす時刻と場所と箱の出し入れだけを持つ。
    [DisallowMultipleComponent]
    public sealed class SupplyDropper : MonoBehaviour
    {
        public const string LogTag = SupplyDropLog.Tag;

        // 地面を探す光線を、エリアの高さのこれだけ上から下ろす。降ろす高さより上の屋根や丘も拾うため。
        public const float GroundProbeHeight = 500f;
        public const float GroundProbeDistance = GroundProbeHeight * 2f;

        private const int MaxProbeHits = 16;

        [SerializeField, Tooltip("投下の調整値の JSON。空なら入口 (GameContent) の supply-drops の JSON、それも無ければ Resources/Tuning/supply-drops を読む")]
        private TextAsset _tuning;

        [SerializeField, Tooltip("箱の中身の表の JSON。空なら入口 (GameContent) の loot の JSON、それも無ければ Resources/Tuning/loot を読む")]
        private TextAsset _lootTuning;

        [SerializeField, Tooltip("開ける前の箱の見た目。原点が底面の中心。空なら仮の箱を作る")]
        private GameObject _crateVisualPrefab;

        [SerializeField, Tooltip("開けた後の箱の見た目。空なら開ける前の見た目のまま (仮の箱なら色を変える)")]
        private GameObject _openedCrateVisualPrefab;

        [SerializeField, Tooltip("表から引いた物に加えて、どの箱にも入れる物。表の持ち物を拾えない参加者にも中身を渡すため")]
        private PickupContents[] _fixedContents = Array.Empty<PickupContents>();

        [SerializeField, Tooltip("落とす場所と中身を選ぶ乱数の種。0 なら始めるたびに変える")]
        private int _seed;

        [SerializeField, Tooltip("箱が着く地面として当てる層")]
        private LayerMask _groundLayers = Physics.DefaultRaycastLayers;

        private readonly List<SupplyCrate> _crates = new List<SupplyCrate>();
        private readonly RaycastHit[] _hits = new RaycastHit[MaxProbeHits];
        private SupplyDropSpec _spec;
        private LootSpec _loot;
        private SupplyDropSchedule _schedule;
        private LootTable _table;
        private ShrinkingZone _zone;
        private System.Random _random;
        private int _nextNumber = 1;

        public event Action<SupplyCrate> Dropped;

        public event Action<SupplyCrate> Landed;

        public static SupplyDropSpec Tuning =>
            KitTuning.Load(c => c.SupplyDropsTuning, SupplyDropJson.ResourcePath, SupplyDropJson.Parse, SupplyDropSpec.Default);

        public TextAsset TuningJson
        {
            get => _tuning;
            set => _tuning = value;
        }

        public TextAsset LootTuningJson
        {
            get => _lootTuning;
            set => _lootTuning = value;
        }

        public GameObject CrateVisualPrefab
        {
            get => _crateVisualPrefab;
            set => _crateVisualPrefab = value;
        }

        public GameObject OpenedCrateVisualPrefab
        {
            get => _openedCrateVisualPrefab;
            set => _openedCrateVisualPrefab = value;
        }

        public int Seed
        {
            get => _seed;
            set => _seed = value;
        }

        public LayerMask GroundLayers
        {
            get => _groundLayers;
            set => _groundLayers = value;
        }

        // 試すときに Resources を通さず値を渡せるよう、上書きできるようにしておく。
        public SupplyDropSpec Spec
        {
            get => _spec ?? (_tuning != null
                ? KitTuning.Read(_tuning.text, $"TextAsset '{_tuning.name}'", SupplyDropJson.Parse, SupplyDropSpec.Default)
                : Tuning);
            set => _spec = value;
        }

        public LootSpec Loot
        {
            get => _loot ?? (_lootTuning != null
                ? KitTuning.Read(_lootTuning.text, $"TextAsset '{_lootTuning.name}'", LootJson.Parse, LootSpec.Default)
                : ExtractionRunner.LootTuning);
            set => _loot = value;
        }

        public IReadOnlyList<PickupContents> FixedContents => _fixedContents ?? Array.Empty<PickupContents>();

        public bool IsRunning => _schedule != null;

        public SupplyDropSchedule Schedule => _schedule;

        // 落としている間は始めたときに読んだ値を使う。途中で JSON を差し替えても、落とす数と開ける距離が食い違わないように。
        public SupplyDropSpec ActiveSpec => _schedule != null ? _schedule.Spec : Spec;

        public ShrinkingZone Zone => _zone;

        public IReadOnlyList<SupplyCrate> Crates => _crates;

        public void SetFixedContents(IReadOnlyList<PickupContents> contents)
        {
            if (contents == null)
            {
                throw new ArgumentNullException(nameof(contents));
            }

            _fixedContents = new PickupContents[contents.Count];
            for (var i = 0; i < contents.Count; i++)
            {
                _fixedContents[i] = contents[i] ?? throw new ArgumentException("contents must not contain null", nameof(contents));
            }
        }

        // 縮むエリアが始まった後に呼んでも、今の段階から落とし始める。spec が null なら Spec を読む。
        public void Begin(ShrinkingZone zone, SupplyDropSpec spec = null)
        {
            if (zone == null)
            {
                throw new ArgumentNullException(nameof(zone));
            }

            if (IsRunning)
            {
                throw new InvalidOperationException($"{nameof(SupplyDropper)} '{name}' is already dropping");
            }

            spec ??= Spec;
            _table = null;
            if (spec.UsesTable)
            {
                _table = Loot.Find(spec.TableId);
                if (_table == null)
                {
                    Debug.LogWarning(SupplyDropLog.UnknownTable(spec.TableId), this);
                }
            }

            _random = new System.Random(_seed != 0 ? _seed : Environment.TickCount);
            _schedule = new SupplyDropSchedule(spec);
            _zone = zone;
            _zone.StageChanged += OnStageChanged;
            if (_zone.IsRunning)
            {
                OnStageChanged(_zone.State);
            }
        }

        // 落ちている途中の箱は Tick で着くまで落とし続ける。宙に止まった箱を残さないため。
        public void Stop()
        {
            if (_zone != null)
            {
                _zone.StageChanged -= OnStageChanged;
            }

            _zone = null;
            _schedule = null;
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            for (var i = 0; i < _crates.Count; i++)
            {
                var crate = _crates[i];
                if (crate != null && !crate.HasLanded)
                {
                    crate.Tick(deltaSeconds);
                }
            }

            if (_schedule == null || _zone == null || !_zone.IsRunning)
            {
                return;
            }

            var due = _schedule.Tick(deltaSeconds);
            for (var i = 0; i < due; i++)
            {
                DropInto(_zone.State);
            }
        }

        public SupplyCrate DropInto(ZoneState zone)
        {
            var spec = ActiveSpec;
            _random ??= new System.Random(_seed != 0 ? _seed : Environment.TickCount);
            var point = SupplyDropSchedule.PickPoint(zone.TargetCenter, zone.TargetRadius, spec.PlaceFraction, _random);
            return DropAt(new Vector3(point.X, _zone != null ? _zone.transform.position.y : transform.position.y, point.Y), spec);
        }

        public SupplyCrate DropAt(Vector3 point, SupplyDropSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var number = _nextNumber++;
            var ground = point.y;
            if (TryFindGround(point, out var found))
            {
                ground = found;
            }
            else
            {
                Debug.LogWarning(SupplyDropLog.NoGround(number, point.ToNumerics()), this);
            }

            var from = new Vector3(point.x, ground + spec.DropHeight, point.z);
            var crate = SupplyCrate.Create(
                number, from, ground, spec.FallSpeed, spec.Reach, _crateVisualPrefab, _openedCrateVisualPrefab, transform);
            crate.Landed += OnCrateLanded;
            _crates.Add(crate);
            Debug.Log(SupplyDropLog.Dropped(number, from.ToNumerics(), ground), crate);
            Dropped?.Invoke(crate);
            return crate;
        }

        public SupplyCrate NearestOpenable(Vector3 position)
        {
            SupplyCrate nearest = null;
            var best = float.PositiveInfinity;
            foreach (var crate in _crates)
            {
                if (crate == null || !crate.CanOpenFrom(position))
                {
                    continue;
                }

                var distance = crate.Container.HorizontalDistanceTo(position);
                if (distance < best)
                {
                    best = distance;
                    nearest = crate;
                }
            }

            return nearest;
        }

        public IReadOnlyList<Pickup> Open(SupplyCrate crate, int openerId)
        {
            if (crate == null)
            {
                throw new ArgumentNullException(nameof(crate));
            }

            if (!_crates.Contains(crate))
            {
                throw new ArgumentException($"supply crate '{crate.name}' was not dropped by '{name}'", nameof(crate));
            }

            _random ??= new System.Random(_seed != 0 ? _seed : Environment.TickCount);
            return crate.Open(openerId, _table, _random, FixedContents);
        }

        public void CollectMarks(List<SupplyDropMark> marks)
        {
            if (marks == null)
            {
                throw new ArgumentNullException(nameof(marks));
            }

            marks.Clear();
            foreach (var crate in _crates)
            {
                if (crate != null && !crate.IsOpened)
                {
                    marks.Add(crate.Mark);
                }
            }
        }

        // 落ちている途中の箱も着く所を渡す。bot が着く前から向かえるようにするため。
        public void CollectUnopenedLandingPoints(List<System.Numerics.Vector3> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            points.Clear();
            foreach (var crate in _crates)
            {
                if (crate != null && !crate.IsOpened)
                {
                    var position = crate.Position;
                    points.Add(new System.Numerics.Vector3(position.x, crate.GroundHeight, position.z));
                }
            }
        }

        // 次の試合で前の箱を残さないよう、モードを始める前に呼ぶ。
        public void ClearCrates()
        {
            foreach (var crate in _crates)
            {
                if (crate == null)
                {
                    continue;
                }

                crate.Landed -= OnCrateLanded;
                if (Application.isPlaying)
                {
                    Destroy(crate.gameObject);
                }
                else
                {
                    DestroyImmediate(crate.gameObject);
                }
            }

            _crates.Clear();
            _nextNumber = 1;
        }

        public bool TryFindGround(Vector3 point, out float height)
        {
            var origin = new Vector3(point.x, point.y + GroundProbeHeight, point.z);
            var count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, GroundProbeDistance, _groundLayers, QueryTriggerInteraction.Ignore);
            var best = float.PositiveInfinity;
            height = point.y;
            for (var i = 0; i < count; i++)
            {
                // 先に落とした箱の上や、たまたま下にいた参加者の頭に着かないよう、箱と参加者の当たり判定は地面にしない。
                var collider = _hits[i].collider;
                if (_hits[i].distance >= best
                    || collider is CharacterController
                    || collider.GetComponentInParent<SupplyCrate>() != null
                    || collider.GetComponentInParent<SideMember>() != null)
                {
                    continue;
                }

                best = _hits[i].distance;
                height = _hits[i].point.y;
            }

            return !float.IsPositiveInfinity(best);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void OnStageChanged(ZoneState state)
        {
            if (_schedule == null)
            {
                return;
            }

            // 最後の円の後は次の円が無いので、残りを捨てて落とすのをやめる。
            if (state.Stage == ZoneStage.Closed)
            {
                _schedule.Stop();
                return;
            }

            // waitSeconds が 0 の段階は Waiting を通らず Shrinking から始まるため、Stage ではなく段階の番号で新しさを見る。
            if (state.PhaseIndex == _schedule.StageIndex)
            {
                return;
            }

            var count = _schedule.BeginStage(state.PhaseIndex);
            Debug.Log(SupplyDropLog.Stage(state.PhaseIndex, count), this);
        }

        private void OnCrateLanded(SupplyCrate crate)
        {
            Landed?.Invoke(crate);
        }
    }
}
