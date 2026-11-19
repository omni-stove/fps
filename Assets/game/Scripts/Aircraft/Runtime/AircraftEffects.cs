using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Runtime
{
    // 飛び方 (SimpleAircraftMotor) の入力と State を見るだけの部品に分けたのは、ゲームが見た目と音を丸ごと別の部品に替えられるようにするため。
    [DisallowMultipleComponent]
    public sealed class AircraftEffects : MonoBehaviour
    {
        public const string LogTag = VehicleLog.Tag;

        private const string CodeVaporShader = "Sprites/Default";

        public const float CodeVaporTime = 0.6f;
        public const float CodeVaporWidth = 0.12f;

        public static readonly Color CodeVaporColor = new Color(1f, 1f, 1f, 0.7f);

        private static Material _codeVaporMaterial;

        [SerializeField, Tooltip("入力と飛び方の状態を読む機体。空なら親階層から探す")]
        private SimpleAircraftMotor _motor;

        [SerializeField, Tooltip("舵面の角度・音の高さ・ベイパーの G を読む機体のアセット (Effects の欄)。空なら機体の Definition、それも空なら既定値")]
        private AircraftDefinition _definition;

        [SerializeField, Tooltip("入力の角度で回す舵面")]
        private AircraftControlSurface[] _surfaces = Array.Empty<AircraftControlSurface>();

        [SerializeField, Tooltip("繰り返し鳴らすエンジン音。空なら鳴らさない")]
        private AudioClip _engineClip;

        [SerializeField, Tooltip("G が閾値を超えている間に出すベイパーのプレハブ (ParticleSystem か TrailRenderer を含む)。空なら白い線をコードで作る")]
        private GameObject _vaporPrefab;

        [SerializeField, Tooltip("ベイパーを出す位置 (翼端)。空なら機体の見た目の左右の端")]
        private Transform[] _vaporAnchors = Array.Empty<Transform>();

        private AircraftEffectsSpec _configuredSpec;
        private AircraftEffectsSpec _definitionSpec;
        private Quaternion[] _restRotations;
        private AudioSource _engineSource;
        private readonly List<GameObject> _vaporInstances = new List<GameObject>();
        private readonly List<GameObject> _createdAnchors = new List<GameObject>();
        private readonly List<TrailRenderer> _vaporTrails = new List<TrailRenderer>();
        private readonly List<ParticleSystem> _vaporParticles = new List<ParticleSystem>();
        private bool _vaporBuilt;
        private bool _vaporOn;
        private bool _warnedNoMotor;

        public SimpleAircraftMotor Motor => _motor;

        public AircraftDefinition Definition
        {
            get => _definition;
            set
            {
                _definition = value;
                _configuredSpec = null;
                _definitionSpec = null;
            }
        }

        // Configure で渡した値をアセットより優先するのは、SimpleAircraftMotor と同じく、コードから組んだ機体がアセット無しでも値を差し替えられるようにするため。
        public AircraftEffectsSpec Spec => _configuredSpec ?? (_definitionSpec ??= BuildSpec());

        public IReadOnlyList<AircraftControlSurface> Surfaces => _surfaces ?? Array.Empty<AircraftControlSurface>();

        public AudioClip EngineClip
        {
            get => _engineClip;
            set => _engineClip = value;
        }

        public GameObject VaporPrefab
        {
            get => _vaporPrefab;
            set
            {
                _vaporPrefab = value;
                ClearVapor();
            }
        }

        public IReadOnlyList<Transform> VaporAnchors => _vaporAnchors ?? Array.Empty<Transform>();

        public AudioSource EngineSource => _engineSource;

        public IReadOnlyList<GameObject> VaporInstances => _vaporInstances;

        public bool IsVaporOn => _vaporOn;

        private Transform Root => _motor != null ? _motor.transform : transform;

        private void OnEnable()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            if (_motor == null)
            {
                _motor = GetComponentInParent<SimpleAircraftMotor>();
            }
        }

        private void OnDisable()
        {
            if (_engineSource != null)
            {
                _engineSource.Stop();
            }

            SetVapor(false);
            RestoreSurfaces();
        }

        private void OnValidate()
        {
            _definitionSpec = null;
        }

        private void LateUpdate()
        {
            if (_motor == null)
            {
                if (!_warnedNoMotor)
                {
                    _warnedNoMotor = true;
                    Debug.LogWarning(AircraftLog.EffectsMissingMotor(name), this);
                }

                return;
            }

            Show(_motor.Input, _motor.State);
        }

        // Edit Mode では OnEnable が走らないので、組み立てる側やテストが直接つなげるようにする。
        public void Bind(SimpleAircraftMotor motor)
        {
            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            if (_motor != motor)
            {
                RestoreSurfaces();
                ClearVapor();
            }

            _motor = motor;
            _definitionSpec = null;
        }

        public void Configure(AircraftEffectsSpec spec)
        {
            _configuredSpec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public void SetSurfaces(IReadOnlyList<AircraftControlSurface> surfaces)
        {
            if (surfaces == null)
            {
                throw new ArgumentNullException(nameof(surfaces));
            }

            RestoreSurfaces();
            _surfaces = new AircraftControlSurface[surfaces.Count];
            for (var i = 0; i < surfaces.Count; i++)
            {
                _surfaces[i] = surfaces[i] ?? throw new ArgumentException($"surfaces[{i}] is null", nameof(surfaces));
            }
        }

        public void SetVaporAnchors(IReadOnlyList<Transform> anchors)
        {
            if (anchors == null)
            {
                throw new ArgumentNullException(nameof(anchors));
            }

            _vaporAnchors = new Transform[anchors.Count];
            for (var i = 0; i < anchors.Count; i++)
            {
                _vaporAnchors[i] = anchors[i];
            }

            ClearVapor();
        }

        // LateUpdate から呼ぶ。物理の進まない Edit Mode のテストからも直接呼べるよう public にする。
        public void Show(VehicleMotorInput input, AircraftFlightState state)
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            var spec = Spec;
            MoveSurfaces(spec, input);
            PlayEngine(spec, input, state);
            SetVapor(AircraftEffectsMix.VaporOn(spec, state));
        }

        private void MoveSurfaces(AircraftEffectsSpec spec, VehicleMotorInput input)
        {
            var surfaces = _surfaces;
            if (surfaces == null || surfaces.Length == 0)
            {
                return;
            }

            if (_restRotations == null || _restRotations.Length != surfaces.Length)
            {
                CaptureRest(surfaces);
            }

            for (var i = 0; i < surfaces.Length; i++)
            {
                var surface = surfaces[i];
                if (surface == null || surface.Transform == null)
                {
                    continue;
                }

                var angle = AircraftEffectsMix.SurfaceAngle(spec, input, surface.Role);
                if (surface.Reversed)
                {
                    angle = -angle;
                }

                surface.Transform.localRotation = _restRotations[i] * Quaternion.AngleAxis(angle, surface.HingeAxis);
            }
        }

        private void CaptureRest(AircraftControlSurface[] surfaces)
        {
            _restRotations = new Quaternion[surfaces.Length];
            for (var i = 0; i < surfaces.Length; i++)
            {
                var target = surfaces[i]?.Transform;
                _restRotations[i] = target != null ? target.localRotation : Quaternion.identity;
            }
        }

        private void RestoreSurfaces()
        {
            var surfaces = _surfaces;
            if (_restRotations != null && surfaces != null && _restRotations.Length == surfaces.Length)
            {
                for (var i = 0; i < surfaces.Length; i++)
                {
                    var target = surfaces[i]?.Transform;
                    if (target != null)
                    {
                        target.localRotation = _restRotations[i];
                    }
                }
            }

            _restRotations = null;
        }

        private void PlayEngine(AircraftEffectsSpec spec, VehicleMotorInput input, AircraftFlightState state)
        {
            if (_engineClip == null)
            {
                if (_engineSource != null && _engineSource.isPlaying)
                {
                    _engineSource.Stop();
                }

                return;
            }

            if (_engineSource == null)
            {
                _engineSource = gameObject.AddComponent<AudioSource>();
                _engineSource.playOnAwake = false;
                _engineSource.loop = true;
                _engineSource.spatialBlend = 1f;
            }

            var level = AircraftEffectsMix.EngineLevel(spec, input, state.Airspeed, MaxSpeed());
            _engineSource.pitch = AircraftEffectsMix.EnginePitch(spec, level);
            _engineSource.volume = AircraftEffectsMix.EngineVolume(spec, level);
            if (_engineSource.clip != _engineClip || !_engineSource.isPlaying)
            {
                _engineSource.clip = _engineClip;
                _engineSource.Play();
            }
        }

        private float MaxSpeed()
        {
            return _motor != null ? _motor.Spec.Aero.MaxSpeed : AircraftAeroSpec.DefaultMaxSpeed;
        }

        private void SetVapor(bool on)
        {
            if (on && !_vaporBuilt)
            {
                BuildVapor();
            }

            _vaporOn = on;
            foreach (var trail in _vaporTrails)
            {
                if (trail != null)
                {
                    trail.emitting = on;
                }
            }

            foreach (var particles in _vaporParticles)
            {
                if (particles == null)
                {
                    continue;
                }

                if (on && !particles.isEmitting)
                {
                    particles.Play(true);
                }
                else if (!on && particles.isEmitting)
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void BuildVapor()
        {
            _vaporBuilt = true;
            foreach (var anchor in ResolveVaporAnchors())
            {
                var instance = _vaporPrefab != null
                    ? Instantiate(_vaporPrefab, anchor.position, anchor.rotation, anchor)
                    : CreateCodeVapor(anchor);
                _vaporInstances.Add(instance);
                _vaporTrails.AddRange(instance.GetComponentsInChildren<TrailRenderer>(true));
                _vaporParticles.AddRange(instance.GetComponentsInChildren<ParticleSystem>(true));
            }

            if (_vaporPrefab != null && _vaporTrails.Count == 0 && _vaporParticles.Count == 0)
            {
                Debug.LogWarning(VehicleLog.VaporUnswitchable(name, _vaporPrefab.name), this);
            }
        }

        private List<Transform> ResolveVaporAnchors()
        {
            var anchors = new List<Transform>();
            foreach (var anchor in VaporAnchors)
            {
                if (anchor != null)
                {
                    anchors.Add(anchor);
                }
            }

            if (anchors.Count > 0)
            {
                return anchors;
            }

            // 翼端の位置が渡されていないときは、機体の見た目を包む箱の左右の端を翼端とみなす。
            var root = Root;
            if (TryLocalBounds(root, out var min, out var max))
            {
                var y = (min.y + max.y) * 0.5f;
                var z = (min.z + max.z) * 0.5f;
                anchors.Add(CreateAnchor(root, "VaporAnchor (left)", new Vector3(min.x, y, z)));
                anchors.Add(CreateAnchor(root, "VaporAnchor (right)", new Vector3(max.x, y, z)));
            }
            else
            {
                anchors.Add(CreateAnchor(root, "VaporAnchor", Vector3.zero));
            }

            return anchors;
        }

        private static bool TryLocalBounds(Transform root, out Vector3 min, out Vector3 max)
        {
            min = Vector3.positiveInfinity;
            max = Vector3.negativeInfinity;
            var found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                {
                    continue;
                }

                // 非アクティブな Renderer の bounds はワールド原点の箱になるため、除く。
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var bounds = renderer.localBounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var rendererLocal = new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    var world = renderer.transform.TransformPoint(rendererLocal);
                    var local = root.InverseTransformPoint(world);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }

                found = true;
            }

            return found;
        }

        private Transform CreateAnchor(Transform root, string anchorName, Vector3 localPosition)
        {
            var anchor = new GameObject(anchorName).transform;
            anchor.SetParent(root, false);
            anchor.localPosition = localPosition;
            _createdAnchors.Add(anchor.gameObject);
            return anchor;
        }

        private static GameObject CreateCodeVapor(Transform anchor)
        {
            var visual = new GameObject("Vapor");
            visual.transform.SetParent(anchor, false);
            var trail = visual.AddComponent<TrailRenderer>();
            trail.emitting = false;
            trail.time = CodeVaporTime;
            trail.widthMultiplier = CodeVaporWidth;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.2f;
            trail.numCapVertices = 2;
            trail.alignment = LineAlignment.View;
            trail.startColor = CodeVaporColor;
            trail.endColor = new Color(CodeVaporColor.r, CodeVaporColor.g, CodeVaporColor.b, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.lightProbeUsage = LightProbeUsage.Off;
            trail.reflectionProbeUsage = ReflectionProbeUsage.Off;
            trail.sharedMaterial = CodeVaporMaterial();
            return visual;
        }

        // 頂点色で塗れて照明に左右されない組み込みのシェーダーを使う。どのパイプラインでも描け、ビルドにも常に入る。
        private static Material CodeVaporMaterial()
        {
            if (_codeVaporMaterial != null)
            {
                return _codeVaporMaterial;
            }

            var shader = Shader.Find(CodeVaporShader);
            if (shader == null)
            {
                Debug.LogWarning(VehicleLog.VaporShaderMissing(CodeVaporShader));
                return null;
            }

            _codeVaporMaterial = new Material(shader) { name = "AircraftVapor", hideFlags = HideFlags.DontSave };
            return _codeVaporMaterial;
        }

        private void ClearVapor()
        {
            for (var i = _vaporInstances.Count - 1; i >= 0; i--)
            {
                DestroySafely(_vaporInstances[i]);
            }

            for (var i = _createdAnchors.Count - 1; i >= 0; i--)
            {
                DestroySafely(_createdAnchors[i]);
            }

            _vaporInstances.Clear();
            _createdAnchors.Clear();
            _vaporTrails.Clear();
            _vaporParticles.Clear();
            _vaporBuilt = false;
            _vaporOn = false;
        }

        private AircraftEffectsSpec BuildSpec()
        {
            if (_definition != null)
            {
                return _definition.ToEffectsSpec();
            }

            var motorDefinition = _motor != null ? _motor.Definition : null;
            return motorDefinition != null ? motorDefinition.ToEffectsSpec() : AircraftEffectsSpec.Default;
        }

        private static void DestroySafely(GameObject target)
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
