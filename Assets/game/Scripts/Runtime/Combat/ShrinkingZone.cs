using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fps.Runtime
{
    /// <summary>
    /// 時間とともに縮むエリア。この GameObject の位置 (水平面) を最初の円の中心にし、<see cref="ZoneSchedule"/> の予定どおりに縮める。
    /// <list type="bullet">
    /// <item>Update で <see cref="Tick"/> を呼ぶ。Edit Mode のテストでは直接呼ぶ。<see cref="Begin()"/> するまでは何もしない
    /// (<see cref="BeginsOnStart"/> なら Start で始める)。</item>
    /// <item><see cref="DamageIntervalSeconds"/> ごとに、円の外にいる生きた <see cref="Damageable"/> へ、その間の
    /// <see cref="ZoneState.DamagePerSecond"/> ぶんのダメージを与える。端数は相手ごとに持ち越す。高さは見ない。</item>
    /// <item>段階か動き (待ち・縮み・最後) が変わるたびと、相手が外に出た・中に戻ったときに <c>[Zone]</c> ログを出す。</item>
    /// <item>Play 中は境界を、上下が開いた半透明の筒で出す (中からも外からも見えるよう両面を描く)。</item>
    /// </list>
    /// HUD は <see cref="SecondsUntilShrink"/> か <see cref="HudLine"/> を読む (このコンポーネントは自分では HUD に書かない。
    /// どの HUD に出すかはモードの Runner が決めるので)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShrinkingZone : MonoBehaviour
    {
        public const string LogTag = ZoneLog.Tag;

        /// <summary>Play 中に作る、境界を示す筒の名前。</summary>
        public const string BoundaryName = "ZoneBoundary";

        public const float DefaultInitialRadius = 60f;
        public const float DefaultDamageIntervalSeconds = 1f;
        public const float DefaultBoundaryHeight = 60f;
        public const int BoundarySegments = 64;

        public static readonly Color DefaultBoundaryColor = new Color(1f, 0.35f, 0.2f, 0.35f);

        // URP の Lit (プリミティブの既定の材質) の、半透明と両面描画のプロパティ
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

        /// <summary>Inspector で並べる段階 1 つ。<see cref="ZonePhase"/> と同じ意味。</summary>
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

        [SerializeField, Tooltip("境界の筒の色。アルファで透け具合を決める")]
        private Color _boundaryColor = DefaultBoundaryColor;

        [SerializeField, Tooltip("境界の筒に使う材質。空なら既定の材質を半透明にして使う")]
        private Material _boundaryMaterial;

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

        private Renderer _boundary;
        private Mesh _boundaryMesh;
        private Material _boundaryMaterialInstance;

        /// <summary>段階が進んだか、動き (待ち・縮み・最後) が変わったときに、新しい様子を渡して呼ばれる。<see cref="Begin()"/> 直後にも呼ぶ。</summary>
        public event Action<ZoneState> StageChanged;

        /// <summary>エリアのダメージを与えたときに、相手と量を渡して呼ばれる。</summary>
        public event Action<Damageable, int> DamageDealt;

        /// <summary>動かしている予定。<see cref="Begin()"/> 前は null。</summary>
        public ZoneSchedule Schedule => _schedule;

        /// <summary><see cref="Begin()"/> してから <see cref="Stop"/> するまで true。</summary>
        public bool IsRunning => _schedule != null;

        /// <summary>始めてからの経過秒数。</summary>
        public float ElapsedSeconds => _elapsedSeconds;

        /// <summary>今の様子。<see cref="Begin()"/> 前は既定値。</summary>
        public ZoneState State => _state;

        /// <summary>次に縮み始めるまでの秒数。待っていなければ (縮んでいる途中・最後の円・始める前) 0。HUD 用。</summary>
        public float SecondsUntilShrink => IsRunning ? _state.SecondsUntilShrink : 0f;

        /// <summary>HUD に出す 1 行 (<see cref="ZoneHudText.Line"/>)。出す側が今の言語で文にする。始める前は空の文 (<c>default</c>)。</summary>
        public LocalizedText HudLine => IsRunning ? ZoneHudText.Line(_state) : default;

        /// <summary>最後にダメージを与える判定をしたとき、外にいた者。</summary>
        public IReadOnlyCollection<Damageable> Outside => _outside;

        /// <summary>Play 中に作った境界の筒。作っていなければ null。</summary>
        public Renderer Boundary => _boundary;

        public bool BeginsOnStart
        {
            get => _beginOnStart;
            set => _beginOnStart = value;
        }

        /// <summary>最初の円の半径 (m)。0 より大きい有限値。次の <see cref="Begin()"/> から使う。</summary>
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

        /// <summary>外にいる者にダメージを与える間隔 (秒)。0 より大きい有限値。</summary>
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

        /// <summary>Play 中に境界の筒を出すか。始めた後に変えても、作った筒はそのまま。</summary>
        public bool ShowsBoundary
        {
            get => _showBoundary;
            set => _showBoundary = value;
        }

        /// <summary>段階の並び (Inspector の値の写し)。次の <see cref="Begin()"/> から使う。</summary>
        public IReadOnlyList<PhaseSettings> PhaseSettingsList => _phases ?? Array.Empty<PhaseSettings>();

        /// <summary>段階の並びを差し替える。次の <see cref="Begin()"/> から使う。中身は <see cref="Begin()"/> で確かめる。</summary>
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

        /// <summary>
        /// ダメージを与える相手を targets に限る。null なら、判定のたびにシーンの <see cref="Damageable"/> をすべて探す。
        /// 参加者を知っているモードの Runner が渡す (渡した一覧は持ち続けるので、Runner 側で入れ替えればそれに従う)。
        /// </summary>
        public void SetTargets(IReadOnlyList<Damageable> targets)
        {
            _explicitTargets = targets;
        }

        /// <summary><see cref="Seed"/> で始める (<see cref="UseRandomSeed"/> なら選び直す)。始められなければ false を返し、理由をエラーログに出す。</summary>
        public bool Begin()
        {
            var seed = _useRandomSeed ? Environment.TickCount : _seed;
            return Begin(seed);
        }

        /// <summary><see cref="Begin()"/> で使うシード。<see cref="UseRandomSeed"/> なら使わない。</summary>
        public int Seed
        {
            get => _seed;
            set => _seed = value;
        }

        /// <summary><see cref="Begin()"/> のたびにシードを選び直すか。</summary>
        public bool UseRandomSeed
        {
            get => _useRandomSeed;
            set => _useRandomSeed = value;
        }

        /// <summary>
        /// seed で円の列を決めて最初から始める。動いている途中でも最初からになる。
        /// Inspector の値が不正 (段階が無い・半径が前より大きいなど) なら false を返し、理由をエラーログに出す。
        /// </summary>
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

        /// <summary>止める。境界の筒は最後の形のまま残す。動いていなければ何もしない。</summary>
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

        /// <summary>
        /// 時間を deltaSeconds 進め、段階の変化をログに出し、境界の筒を合わせ、ダメージの間隔に達するたびに外の者へダメージを与える。
        /// 動いていなければ何もしない。
        /// </summary>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
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

        /// <summary>position が今の円の中 (境界を含む) なら true。高さは見ない。始める前は常に true。</summary>
        public bool Contains(Vector3 position)
        {
            return !IsRunning || _state.Contains(new System.Numerics.Vector2(position.x, position.z));
        }

        /// <summary>
        /// 境界の筒を作るか、作ってあれば今の円に合わせる。<see cref="ShowsBoundary"/> が false か、始める前なら何もしない。
        /// 当たり判定は持たせない (弾と移動を遮らないように)。
        /// </summary>
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

            _targets.AddRange(FindObjectsByType<Damageable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }

        // 破棄された・対象から外れた相手の持ち越しと「外にいる」を捨てる。外にいたまま外れた者は、戻ってきたら外に出たとして数え直す。
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
            // プリミティブを作るのは、その描画パイプラインの既定の材質を得るため。メッシュは上下の開いた筒に差し替える。
            var boundary = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            boundary.name = BoundaryName;
            // Destroy はフレーム末まで残り、その間に拡大した Collider が移動や射撃の判定に当たるため即時に消す。
            DestroyImmediate(boundary.GetComponent<Collider>());
            boundary.transform.SetParent(transform, false);

            _boundaryMesh = BuildTubeMesh(BoundarySegments);
            boundary.GetComponent<MeshFilter>().sharedMesh = _boundaryMesh;

            _boundary = boundary.GetComponent<Renderer>();
            _boundary.shadowCastingMode = ShadowCastingMode.Off;
            _boundary.receiveShadows = false;

            if (_boundaryMaterial != null)
            {
                _boundary.sharedMaterial = _boundaryMaterial;
                return;
            }

            // 共有の既定の材質を書き換えると他のプリミティブまで透けるので、このエリア用に複製する。
            _boundaryMaterialInstance = new Material(_boundary.sharedMaterial) { name = BoundaryName };
            MakeTranslucentAndDoubleSided(_boundaryMaterialInstance);
            _boundaryMaterialInstance.color = _boundaryColor;
            _boundary.sharedMaterial = _boundaryMaterialInstance;
        }

        /// <summary>半径 1・高さ 1 (y は -0.5 から 0.5)、上下の開いた筒。法線は外向き (両面描画で内側からも見せる)。</summary>
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
