using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Runtime
{
    // 撒く数・間隔・効き目は CountermeasurePod に任せ、ここは座席の入力を受けて撒く・見た目を動かす・捕捉する側に見つけてもらうことだけを持つ。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Vehicle))]
    public sealed class VehicleCountermeasures : MonoBehaviour, IVehicleInputSink
    {
        public const string FlareObjectName = "Flare";

        public const float PlaceholderRadius = 0.35f;

        public const float PlaceholderLightRange = 12f;

        public const float PlaceholderLightIntensity = 4f;

        private static readonly Color _placeholderColor = new Color(1f, 0.55f, 0.15f);
        private static Material _placeholderMaterial;

        private readonly Dictionary<int, GameObject> _visuals = new Dictionary<int, GameObject>();
        private readonly List<int> _gone = new List<int>();

        [SerializeField]
        private CountermeasureSettings _settings = new CountermeasureSettings();

        private Vehicle _vehicle;
        private CountermeasurePod _pod;
        private AudioSource _audio;
        private bool _bound;

        public CountermeasureSettings Settings => _settings;

        public Vehicle Vehicle => _vehicle != null ? _vehicle : (_vehicle = GetComponent<Vehicle>());

        public CountermeasurePod Pod => _pod ?? (_pod = new CountermeasurePod(ReadSpec()));

        public int Remaining => Pod.Remaining;

        public bool IsBound => _bound;

        public IReadOnlyDictionary<int, GameObject> Visuals => _visuals;

        // 乗っている人に当たって捉えられても、乗り物の撒いたフレアで外れるよう、乗り手からは座っている乗り物を引く。
        public static VehicleCountermeasures For(Component target)
        {
            if (target == null)
            {
                return null;
            }

            var rider = target.GetComponent<IVehicleRider>();
            var seat = rider?.Seat;
            var host = seat != null && seat.Vehicle != null ? (Component)seat.Vehicle : target;
            var found = host.GetComponentInParent<VehicleCountermeasures>();

            // OnEnable の走らない Edit Mode でも見つけられるよう、isActiveAndEnabled ではなく自分で見る。
            return found != null && found.enabled && found.gameObject.activeInHierarchy ? found : null;
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            ClearVisuals();
        }

        // OnEnable の走らない Edit Mode のテストでも結べるよう public にする (VehicleMotorLink と同じ)。
        public bool Bind()
        {
            if (_bound)
            {
                return true;
            }

            if (Vehicle == null)
            {
                return false;
            }

            Vehicle.AddInputSink(this);
            _bound = true;
            return true;
        }

        public void Unbind()
        {
            if (!_bound)
            {
                return;
            }

            _bound = false;
            if (_vehicle != null)
            {
                _vehicle.RemoveInputSink(this);
            }
        }

        // 撒いた残りを持ち越さず数え直すので、走らせている途中に差し替えると空にいるフレアも消える。
        public void Configure(CountermeasureSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _settings.CopyFrom(spec);
            ClearVisuals();
            _pod = new CountermeasurePod(spec);
        }

        public void Refill()
        {
            Pod.Refill();
            Debug.Log(CountermeasureLog.Refilled(name, Pod.Spec.Count), this);
        }

        public void OnSeatInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            // 撒くのは操縦する人だけにする。同乗者が押しても操縦する人の残りの数が減らないようにするため。
            if (seat == null || seat.Role != VehicleSeatRole.Driver || !input.CountermeasurePressed)
            {
                return;
            }

            TryDeploy();
        }

        public void OnSeatVacated(VehicleSeat seat)
        {
        }

        public CountermeasureVerdict TryDeploy()
        {
            var pod = Pod;
            var origin = transform.TransformPoint(_settings.EjectLocalOffset);
            var velocity = BodyVelocity() + (EjectDirection() * pod.Spec.EjectSpeed);
            var verdict = pod.TryDeploy(origin.ToNumerics(), velocity.ToNumerics(), out var flareId);
            if (verdict != CountermeasureVerdict.Deployed)
            {
                Debug.Log(CountermeasureLog.Refused(name, verdict, pod.Remaining, pod.Spec.Count, pod.CooldownLeft), this);
                return verdict;
            }

            Debug.Log(CountermeasureLog.Deployed(name, flareId, pod.Remaining, pod.Spec.Count), this);
            _visuals[flareId] = CreateVisual(origin);
            PlaySound();
            return verdict;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaSeconds)
        {
            var pod = Pod;
            pod.Tick(deltaSeconds, Physics.gravity.ToNumerics());
            _gone.Clear();
            foreach (var pair in _visuals)
            {
                if (pod.TryLocate(pair.Key, out var point))
                {
                    if (pair.Value != null)
                    {
                        pair.Value.transform.position = point.ToUnity();
                    }
                }
                else
                {
                    _gone.Add(pair.Key);
                }
            }

            foreach (var id in _gone)
            {
                DestroySafely(_visuals[id]);
                _visuals.Remove(id);
            }
        }

        private CountermeasureSpec ReadSpec()
        {
            try
            {
                return _settings.ToSpec();
            }
            catch (ArgumentException e)
            {
                Debug.LogError(CountermeasureLog.InvalidSettings(name, e.Message), this);
                return CountermeasureSpec.Default;
            }
        }

        private Vector3 EjectDirection()
        {
            var local = _settings.EjectLocalDirection;
            return local.sqrMagnitude > 1e-8f ? transform.TransformDirection(local.normalized) : Vector3.down;
        }

        private Vector3 BodyVelocity()
        {
            return TryGetComponent<Rigidbody>(out var body) ? body.linearVelocity : Vector3.zero;
        }

        private GameObject CreateVisual(Vector3 position)
        {
            var prefab = _settings.FlarePrefab;
            var visual = prefab != null ? Instantiate(prefab) : BuildPlaceholder();
            visual.name = FlareObjectName;

            // フレアの当たりが残ると、撃った弾や周りの Raycast がフレアに当たって止まるので切る。
            foreach (var collider in visual.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            visual.transform.position = position;
            return visual;
        }

        // 見た目のプレハブが空のときだけ使う。描画パイプラインの既定の材質を元に、色と光だけを変えた球にする。
        private static GameObject BuildPlaceholder()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = Vector3.one * (PlaceholderRadius * 2f);
            var renderer = sphere.GetComponent<Renderer>();
            if (_placeholderMaterial == null)
            {
                _placeholderMaterial = new Material(renderer.sharedMaterial) { name = FlareObjectName, color = _placeholderColor };
                _placeholderMaterial.EnableKeyword("_EMISSION");
                _placeholderMaterial.SetColor("_EmissionColor", _placeholderColor * 4f);
            }

            renderer.sharedMaterial = _placeholderMaterial;
            var light = sphere.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = _placeholderColor;
            light.range = PlaceholderLightRange;
            light.intensity = PlaceholderLightIntensity;
            return sphere;
        }

        private void PlaySound()
        {
            var clip = _settings.DeploySound;
            if (clip == null)
            {
                return;
            }

            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 1f;
            }

            _audio.PlayOneShot(clip);
        }

        private void ClearVisuals()
        {
            foreach (var visual in _visuals.Values)
            {
                DestroySafely(visual);
            }

            _visuals.Clear();
        }

        // 再生していない EditMode のテストでも消せるよう、Destroy が使えないときは即座に消す。
        private static void DestroySafely(Object target)
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
