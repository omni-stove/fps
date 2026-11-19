using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Runtime
{
    // 自分では HUD に書かず文も作らないのは、どの HUD にどう出すかをモードの Runner と UI が決めるため。
    [DisallowMultipleComponent]
    public sealed class ShrinkingZone : MonoBehaviour
    {
        public const string LogTag = ZoneLog.Tag;

        public const string BoundaryName = "ZoneBoundary";

        public const float DefaultInitialRadius = 60f;
        public const float DefaultDamageIntervalSeconds = 1f;
        public const float DefaultBoundaryHeight = 60f;
        public const int BoundarySegments = 64;

        public static readonly Color DefaultBoundaryColor = new Color(1f, 0.35f, 0.2f, 0.35f);

        private static readonly int _surfaceId = Shader.PropertyToID("_Surface");
        private static readonly int _blendId = Shader.PropertyToID("_Blend");
        private static readonly int _srcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int _dstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int _srcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
        private static readonly int _dstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");
        private static readonly int _zWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int _cullId = Shader.PropertyToID("_Cull");
        private const string _transparentKeyword = "_SURFACE_TYPE_TRANSPARENT";

        private const int GizmoSegments = 48;

        [Serializable]
        public struct PhaseSettings
        {
            [Min(0f), Tooltip("縮み始めるまでの待ち時間 (秒)")]
            public float WaitSeconds;

            [Min(0f), Tooltip("縮むのにかける時間 (秒)")]
            public float ShrinkSeconds;

            [Min(0.1f), Tooltip("この段階の終わりの円の半径 (m)。前の円の半径以下")]
            public float Radius;

            [Min(0f), Tooltip("この段階の間、円の外で受けるダメージ (毎秒)")]
            public float DamagePerSecond;

            public PhaseSettings(float waitSeconds, float shrinkSeconds, float radius, float damagePerSecond)
            {
                WaitSeconds = waitSeconds;
                ShrinkSeconds = shrinkSeconds;
                Radius = radius;
                DamagePerSecond = damagePerSecond;
            }

            public ZonePhase ToPhase()
            {
                return new ZonePhase(WaitSeconds, ShrinkSeconds, Radius, DamagePerSecond);
            }
        }

        [SerializeField, Min(0.1f), Tooltip("最初の円の半径 (m)。中心はこの GameObject の位置")]
        private float _initialRadius = DefaultInitialRadius;

        [SerializeField, Tooltip("段階の並び。上から順に進む")]
        private PhaseSettings[] _phases =
        {
            new PhaseSettings(30f, 20f, 35f, 1f),
            new PhaseSettings(20f, 15f, 18f, 3f),
            new PhaseSettings(15f, 10f, 6f, 8f),
        };

        [SerializeField, Tooltip("次の円の中心を選ぶ乱数のシード。UseRandomSeed なら無視する")]
        private int _seed;

        [SerializeField, Tooltip("始めるたびにシードを選び直すか")]
        private bool _useRandomSeed = true;

        [SerializeField, Tooltip("Start で自分から始めるか。モードの Runner が始めるなら外す")]
        private bool _beginOnStart = true;

        [SerializeField, Min(0.05f), Tooltip("外にいる者にダメージを与える間隔 (秒)")]
        private float _damageIntervalSeconds = DefaultDamageIntervalSeconds;

        [SerializeField, Tooltip("Play 中に境界の筒を出すか")]
        private bool _showBoundary = true;

        [SerializeField, Min(0.1f), Tooltip("境界の筒の高さ (m)。この GameObject の高さから上下に半分ずつ")]
        private float _boundaryHeight = DefaultBoundaryHeight;

        [SerializeField, Tooltip("境界の筒の色。アルファで透け具合を決める。色をパレットから取るときは、アルファだけを使う")]
        private Color _boundaryColor = DefaultBoundaryColor;

        [SerializeField, Tooltip("境界の筒の色を、設定の色覚のパレットの危険の色にするか (変えると塗り直す)。外すと境界の筒の色をそのまま使う")]
        private bool _boundaryColorFromPalette = true;

        [SerializeField, Tooltip("境界の筒に使う材質。空なら既定の材質を半透明にして使う")]
        private Material _boundaryMaterial;

        [SerializeField, Tooltip("境界の見た目。原点を中心にした半径 1m・高さ 1m の筒の形にする (円の半径と筒の高さまで拡大する)。当たり判定は外し、材質と色はプレハブのまま。空なら仮の筒を作る")]
        private GameObject _boundaryPrefab;

        private readonly Dictionary<Damageable, float> _owedDamage = new Dictionary<Damageable, float>();
        private readonly HashSet<Damageable> _outside = new HashSet<Damageable>();
        private readonly List<Damageable> _targets = new List<Damageable>();
        private readonly List<Damageable> _stale = new List<Damageable>();
        private IReadOnlyList<Damageable> _explicitTargets;

        private ZoneSchedule _schedule;
        private ZoneState _state;
        private float _elapsedSeconds;
        private float _damageTimer;
        private Vector3 _origin;

        private GameObject _boundary;
        private Mesh _boundaryMesh;
        private Material _boundaryMaterialInstance;
        private bool _followsSettings;

        public event Action<ZoneState> StageChanged;

        public event Action<Damageable, int> DamageDealt;

        public ZoneSchedule Schedule => _schedule;

        public bool IsRunning => _schedule != null;

        public float ElapsedSeconds => _elapsedSeconds;

        public ZoneState State => _state;

        public float SecondsUntilShrink => IsRunning ? _state.SecondsUntilShrink : 0f;

        public IReadOnlyCollection<Damageable> Outside => _outside;

        public GameObject Boundary => _boundary;

        public Material BoundaryMaterial
        {
            get => _boundaryMaterial;
            set => _boundaryMaterial = value;
        }

        public GameObject BoundaryPrefab
        {
            get => _boundaryPrefab;
            set => _boundaryPrefab = value;
        }

        public bool BeginsOnStart
        {
            get => _beginOnStart;
            set => _beginOnStart = value;
        }

        public float InitialRadius
        {
            get => _initialRadius;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "InitialRadius must be positive and finite");
                }

                _initialRadius = value;
            }
        }

        public float DamageIntervalSeconds
        {
            get => _damageIntervalSeconds;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "DamageIntervalSeconds must be positive and finite");
                }

                _damageIntervalSeconds = value;
            }
        }

        public float BoundaryHeight
        {
            get => _boundaryHeight;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "BoundaryHeight must be positive and finite");
                }

                _boundaryHeight = value;
            }
        }

        public bool ShowsBoundary
        {
            get => _showBoundary;
            set => _showBoundary = value;
        }

        public Color BoundaryColor
        {
            get => _boundaryColor;
            set
            {
                _boundaryColor = value;
                ApplyBoundaryColor();
            }
        }

        public bool BoundaryColorFromPalette
        {
            get => _boundaryColorFromPalette;
            set
            {
                _boundaryColorFromPalette = value;
                ApplyBoundaryColor();
            }
        }

        public Color BoundaryColorFor(TeamPalette palette)
        {
            if (!_boundaryColorFromPalette)
            {
                return _boundaryColor;
            }

            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            var danger = TeamColors.ToColor(palette.Danger);
            return new Color(danger.r, danger.g, danger.b, _boundaryColor.a);
        }

        public IReadOnlyList<PhaseSettings> PhaseSettingsList => _phases ?? Array.Empty<PhaseSettings>();

        public void SetPhases(IReadOnlyList<PhaseSettings> phases)
        {
            if (phases == null)
            {
                throw new ArgumentNullException(nameof(phases));
            }

            _phases = new PhaseSettings[phases.Count];
            for (var i = 0; i < phases.Count; i++)
            {
                _phases[i] = phases[i];
            }
        }

        public void SetTargets(IReadOnlyList<Damageable> targets)
        {
            _explicitTargets = targets;
        }

        public bool Begin()
        {
            var seed = _useRandomSeed ? Environment.TickCount : _seed;
            return Begin(seed);
        }

        public int Seed
        {
            get => _seed;
            set => _seed = value;
        }

        public bool UseRandomSeed
        {
            get => _useRandomSeed;
            set => _useRandomSeed = value;
        }

        public bool Begin(int seed)
        {
            ZoneSchedule schedule;
            try
            {
                schedule = new ZoneSchedule(
                    new System.Numerics.Vector2(transform.position.x, transform.position.z),
                    _initialRadius,
                    BuildPhases(),
                    seed);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} エリアを組み立てられません: {e.Message}", this);
                return false;
            }

            _schedule = schedule;
            _origin = transform.position;
            _elapsedSeconds = 0f;
            _damageTimer = 0f;
            _owedDamage.Clear();
            _outside.Clear();
            _state = schedule.Evaluate(0f);

            Debug.Log(ZoneLog.Started(schedule), this);
            Debug.Log(ZoneLog.Stage(_state, schedule.PhaseCount), this);
            StageChanged?.Invoke(_state);
            RefreshBoundary();
            return true;
        }

        public void Stop()
        {
            if (_schedule == null)
            {
                return;
            }

            _schedule = null;
            _owedDamage.Clear();
            _outside.Clear();
            Debug.Log(ZoneLog.Stopped(), this);
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_schedule == null)
            {
                return;
            }

            _elapsedSeconds += deltaSeconds;
            var previous = _state;
            _state = _schedule.Evaluate(_elapsedSeconds);
            if (_state.PhaseIndex != previous.PhaseIndex || _state.Stage != previous.Stage)
            {
                Debug.Log(ZoneLog.Stage(_state, _schedule.PhaseCount), this);
                StageChanged?.Invoke(_state);
            }

            RefreshBoundary();

            _damageTimer += deltaSeconds;
            while (_damageTimer >= _damageIntervalSeconds && _schedule != null)
            {
                _damageTimer -= _damageIntervalSeconds;
                ApplyDamage(_state.DamagePerSecond * _damageIntervalSeconds);
            }
        }

        public bool Contains(Vector3 position)
        {
            return !IsRunning || _state.Contains(new System.Numerics.Vector2(position.x, position.z));
        }

        public void RefreshBoundary()
        {
            if (!_showBoundary || !IsRunning)
            {
                return;
            }

            if (_boundary == null)
            {
                if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
                {
                    return;
                }

                CreateBoundary();
            }

            var boundaryTransform = _boundary.transform;
            boundaryTransform.SetPositionAndRotation(
                new Vector3(_state.Center.X, _origin.y, _state.Center.Y), Quaternion.identity);

            // 親の拡縮を打ち消し、世界で半径 r・高さ _boundaryHeight にする (筒のメッシュは半径 1・高さ 1)。
            var parentScale = transform.lossyScale;
            boundaryTransform.localScale = new Vector3(
                SafeDivide(_state.Radius, parentScale.x),
                SafeDivide(_boundaryHeight, parentScale.y),
                SafeDivide(_state.Radius, parentScale.z));
        }

        private void Start()
        {
            if (_beginOnStart && !IsRunning)
            {
                Begin();
            }
        }

        private void Update()
        {
            if (IsRunning)
            {
                Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (_followsSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsSettings = false;
            }

            DestroySafely(_boundaryMaterialInstance);
            _boundaryMaterialInstance = null;
            DestroySafely(_boundaryMesh);
            _boundaryMesh = null;
        }

        private ZonePhase[] BuildPhases()
        {
            var settings = _phases ?? Array.Empty<PhaseSettings>();
            var phases = new ZonePhase[settings.Length];
            for (var i = 0; i < settings.Length; i++)
            {
                try
                {
                    phases[i] = settings[i].ToPhase();
                }
                catch (ArgumentOutOfRangeException e)
                {
                    throw new ArgumentException("phase " + i + ": " + e.Message, e);
                }
            }

            return phases;
        }

        private void ApplyDamage(float amount)
        {
            CollectTargets();
            foreach (var target in _targets)
            {
                // コールバック内で Stop() されたら、残りの相手には触らず、空にした状態も書き戻さない。
                if (_schedule == null)
                {
                    return;
                }

                var inside = target.IsDead || _state.Contains(new System.Numerics.Vector2(target.transform.position.x, target.transform.position.z));
                if (inside)
                {
                    if (_outside.Remove(target))
                    {
                        _owedDamage.Remove(target);
                        if (!target.IsDead)
                        {
                            Debug.Log(ZoneLog.Inside(target.Id), target);
                        }
                    }

                    continue;
                }

                if (_outside.Add(target))
                {
                    Debug.Log(ZoneLog.Outside(target.Id), target);
                }

                // 1 回の量が 1 に満たない設定 (毎秒 0.5 など) でも減るよう、端数を相手ごとに持ち越す。
                _owedDamage.TryGetValue(target, out var owed);
                owed += amount;
                var whole = (int)Math.Floor(owed);
                _owedDamage[target] = owed - whole;
                if (whole > 0)
                {
                    target.TakeDamage(whole);
                    if (_schedule == null)
                    {
                        return;
                    }

                    DamageDealt?.Invoke(target, whole);
                }
            }

            ForgetMissingTargets();
        }

        private void CollectTargets()
        {
            _targets.Clear();
            if (_explicitTargets != null)
            {
                foreach (var target in _explicitTargets)
                {
                    if (target != null)
                    {
                        _targets.Add(target);
                    }
                }

                return;
            }

            _targets.AddRange(FindObjectsByType<Damageable>(FindObjectsInactive.Exclude));
        }

        private void ForgetMissingTargets()
        {
            _stale.Clear();
            foreach (var target in _outside)
            {
                if (target == null || !_targets.Contains(target))
                {
                    _stale.Add(target);
                }
            }

            foreach (var target in _stale)
            {
                _outside.Remove(target);
                _owedDamage.Remove(target);
            }
        }

        private void CreateBoundary()
        {
            if (_boundaryPrefab != null)
            {
                // 縮む円に合わせて拡大した当たり判定が、移動や射撃を遮らないように外す。
                _boundary = VisualPrefab.Instantiate(_boundaryPrefab, transform, BoundaryName, stripColliders: true);
                foreach (var prefabRenderer in _boundary.GetComponentsInChildren<Renderer>(true))
                {
                    prefabRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    prefabRenderer.receiveShadows = false;
                }

                return;
            }

            // プリミティブを作るのは、その描画パイプラインの既定の材質を得るため。メッシュは上下の開いた筒に差し替える。
            var boundary = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            boundary.name = BoundaryName;
            // Destroy はフレーム末まで残り、その間に拡大した Collider が移動や射撃の判定に当たるため即時に消す。
            DestroyImmediate(boundary.GetComponent<Collider>());
            boundary.transform.SetParent(transform, false);

            _boundaryMesh = BuildTubeMesh(BoundarySegments);
            boundary.GetComponent<MeshFilter>().sharedMesh = _boundaryMesh;

            _boundary = boundary;
            var renderer = boundary.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (_boundaryMaterial != null)
            {
                renderer.sharedMaterial = _boundaryMaterial;
                return;
            }

            // 共有の既定の材質を書き換えると他のプリミティブまで透けるので、このエリア用に複製する。
            _boundaryMaterialInstance = CreateBoundaryMaterial(renderer.sharedMaterial);
            renderer.sharedMaterial = _boundaryMaterialInstance;
            if (!_followsSettings)
            {
                AppliedControlSettings.Changed += HandleSettingsChanged;
                _followsSettings = true;
            }

            ApplyBoundaryColor();
        }

        // 差し替えた材質とプレハブの材質は、ゲームが決めた見た目なので塗らない。
        private void ApplyBoundaryColor()
        {
            if (_boundaryMaterialInstance != null)
            {
                _boundaryMaterialInstance.color = BoundaryColorFor(TeamColors.Current);
            }
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                return;
            }

            ApplyBoundaryColor();
        }

        // 内側の面を足さず両面描画で内側からも見せるので、法線は外向きだけにする。
        public static Mesh BuildTubeMesh(int segments)
        {
            if (segments < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(segments), segments, "segments must be at least 3");
            }

            // 継ぎ目で UV が折り返すよう、最初の列を最後に重ねて置く。
            var columns = segments + 1;
            var vertices = new Vector3[columns * 2];
            var normals = new Vector3[columns * 2];
            var uvs = new Vector2[columns * 2];
            for (var i = 0; i < columns; i++)
            {
                var u = (float)i / segments;
                var angle = u * Mathf.PI * 2f;
                var normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = normal + (Vector3.down * 0.5f);
                vertices[(i * 2) + 1] = normal + (Vector3.up * 0.5f);
                normals[i * 2] = normal;
                normals[(i * 2) + 1] = normal;
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[(i * 2) + 1] = new Vector2(u, 1f);
            }

            var triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var bottom = i * 2;
                var top = bottom + 1;
                var nextBottom = bottom + 2;
                var nextTop = bottom + 3;
                var t = i * 6;
                triangles[t] = bottom;
                triangles[t + 1] = top;
                triangles[t + 2] = nextBottom;
                triangles[t + 3] = nextBottom;
                triangles[t + 4] = top;
                triangles[t + 5] = nextTop;
            }

            var mesh = new Mesh { name = BoundaryName };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        // 対戦のエリア (NetV2ZonePart) が同じ見た目の筒を作るのに使う。共有の材質を書き換えないよう複製を返す。
        public static Material CreateBoundaryMaterial(Material source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var material = new Material(source) { name = BoundaryName };
            MakeTranslucentAndDoubleSided(material);
            return material;
        }

        // 他のシェーダーの材質では無いプロパティへの書き込みは無視されるので、描画が変わらないだけで壊れない。
        private static void MakeTranslucentAndDoubleSided(Material material)
        {
            material.SetFloat(_surfaceId, 1f);
            material.SetFloat(_blendId, 0f);
            material.SetFloat(_srcBlendId, (float)BlendMode.SrcAlpha);
            material.SetFloat(_dstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat(_srcBlendAlphaId, (float)BlendMode.One);
            material.SetFloat(_dstBlendAlphaId, (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat(_zWriteId, 0f);
            material.SetFloat(_cullId, (float)CullMode.Off);
            material.EnableKeyword(_transparentKeyword);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private void OnDrawGizmos()
        {
            if (IsRunning)
            {
                DrawCircle(new Vector3(_state.Center.X, _origin.y, _state.Center.Y), _state.Radius, new Color(1f, 0.35f, 0.2f));
                if (_state.Stage != ZoneStage.Closed)
                {
                    DrawCircle(new Vector3(_state.TargetCenter.X, _origin.y, _state.TargetCenter.Y), _state.TargetRadius, Color.white);
                }

                return;
            }

            if (_initialRadius > 0f)
            {
                DrawCircle(transform.position, _initialRadius, new Color(1f, 0.35f, 0.2f));
            }
        }

        private static void DrawCircle(Vector3 center, float radius, Color color)
        {
            Gizmos.color = color;
            var previous = center + new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= GizmoSegments; i++)
            {
                var angle = i * Mathf.PI * 2f / GizmoSegments;
                var next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 1e-6f ? value / divisor : value;
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
