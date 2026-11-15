using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Fps.Runtime
{
    /// <summary>
    /// 爆発の届く範囲を線で見せるデバッグ表示。まだ爆発していないグレネード (<see cref="ThrownGrenade.Live"/>) には今の位置に、
    /// 起きた爆発 (<see cref="Report"/>) にはその中心に、届く半径 (赤) と減らさずに当たる半径 (橙) の球を、水平と縦 2 本の円で描く。
    /// 爆発の円は <see cref="HoldSeconds"/> 秒かけて薄くして消す。
    /// Editor と Development Build だけで動き、それ以外と専用サーバーでは作られず <see cref="Report"/> は何もしない。
    /// シーンの読み込み後に自分で 1 つ作り、F4 で表示と非表示を切り替える (入力アセットは変えず <see cref="Keyboard.current"/> を直接読む)。既定は表示。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExplosionRangeDebug : MonoBehaviour
    {
        public const string LogTag = "[ExplosionDebug]";

        public const string ObjectName = "ExplosionRangeDebug";

        /// <summary>起きた爆発の円を出し続ける秒数。</summary>
        public const float HoldSeconds = 2.5f;

        private const int CircleSegments = 48;
        private const float LineWidth = 0.04f;

        private static readonly Color OuterColor = new Color(1f, 0.15f, 0.1f, 0.9f);
        private static readonly Color InnerColor = new Color(1f, 0.65f, 0.1f, 0.9f);

        private static ExplosionRangeDebug _instance;
        private static Material _material;

        private readonly List<Blast> _blasts = new List<Blast>();
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private int _usedLines;
        private bool _visible = true;

        /// <summary>この Build で出すか。Editor と Development Build だけ true。</summary>
        public static bool IsSupported => Debug.isDebugBuild;

        /// <summary>今動いている表示。作られていなければ null。</summary>
        public static ExplosionRangeDebug Instance => _instance;

        /// <summary>表示中か (F4 で切り替わる)。</summary>
        public bool IsVisible => _visible;

        /// <summary>出している爆発の円の数 (グレネードの分は数えない)。</summary>
        public int BlastCount => _blasts.Count;

        /// <summary>
        /// center で radius (届く半径) と fullDamageRadius (減らさずに当たる半径) の爆発が起きたことを知らせ、その範囲を描く。
        /// 表示が無いときは何もしない。半径が 0 以下か有限でなければ描かない。
        /// </summary>
        public static void Report(Vector3 center, float radius, float fullDamageRadius)
        {
            if (_instance == null || !(radius > 0f) || float.IsInfinity(radius))
            {
                return;
            }

            _instance.Add(center, radius, fullDamageRadius);
        }

        /// <summary>表示と非表示を切り替える。F4 を押したときと同じ。</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible)
            {
                HideUnusedLines(0);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnPlay()
        {
            if (!IsSupported || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild) || _instance != null)
            {
                return;
            }

            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            go.AddComponent<ExplosionRangeDebug>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f4Key.wasPressedThisFrame)
            {
                SetVisible(!_visible);
                Debug.Log($"{LogTag} visible={_visible}", this);
            }
        }

        private void LateUpdate()
        {
            var step = Time.deltaTime;
            for (var i = _blasts.Count - 1; i >= 0; i--)
            {
                var blast = _blasts[i];
                blast.Elapsed += step;
                if (blast.Elapsed >= HoldSeconds)
                {
                    _blasts.RemoveAt(i);
                }
                else
                {
                    _blasts[i] = blast;
                }
            }

            if (!_visible)
            {
                return;
            }

            _usedLines = 0;
            foreach (var grenade in ThrownGrenade.Live)
            {
                var damage = grenade != null && !grenade.HasExploded ? grenade.Explosion : null;
                if (damage != null)
                {
                    DrawSphere(grenade.transform.position, damage.Radius, damage.FullDamageRadius, 1f);
                }
            }

            foreach (var blast in _blasts)
            {
                DrawSphere(blast.Center, blast.Radius, blast.FullDamageRadius, 1f - (blast.Elapsed / HoldSeconds));
            }

            HideUnusedLines(_usedLines);
        }

        private void Add(Vector3 center, float radius, float fullDamageRadius)
        {
            _blasts.Add(new Blast { Center = center, Radius = radius, FullDamageRadius = fullDamageRadius });
            Debug.Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} blast at=({1:0.0}, {2:0.0}, {3:0.0}) radius={4:0.0} full={5:0.0}",
                    LogTag,
                    center.x,
                    center.y,
                    center.z,
                    radius,
                    fullDamageRadius),
                this);
        }

        private void DrawSphere(Vector3 center, float radius, float fullDamageRadius, float alpha)
        {
            DrawCircles(center, radius, Fade(OuterColor, alpha));
            if (fullDamageRadius > 0f && !float.IsInfinity(fullDamageRadius))
            {
                DrawCircles(center, fullDamageRadius, Fade(InnerColor, alpha));
            }
        }

        private void DrawCircles(Vector3 center, float radius, Color color)
        {
            DrawCircle(center, radius, Vector3.right, Vector3.forward, color);
            DrawCircle(center, radius, Vector3.right, Vector3.up, color);
            DrawCircle(center, radius, Vector3.forward, Vector3.up, color);
        }

        private void DrawCircle(Vector3 center, float radius, Vector3 axisA, Vector3 axisB, Color color)
        {
            var line = NextLine();
            line.startColor = color;
            line.endColor = color;
            for (var i = 0; i < CircleSegments; i++)
            {
                var angle = i * (Mathf.PI * 2f / CircleSegments);
                line.SetPosition(i, center + (((axisA * Mathf.Cos(angle)) + (axisB * Mathf.Sin(angle))) * radius));
            }
        }

        private LineRenderer NextLine()
        {
            if (_usedLines == _lines.Count)
            {
                _lines.Add(CreateLine());
            }

            var line = _lines[_usedLines++];
            line.enabled = true;
            return line;
        }

        private void HideUnusedLines(int used)
        {
            for (var i = used; i < _lines.Count; i++)
            {
                _lines[i].enabled = false;
            }
        }

        private LineRenderer CreateLine()
        {
            var go = new GameObject("Circle");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = CircleSegments;
            line.widthMultiplier = LineWidth;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.sharedMaterial = LineMaterial();
            return line;
        }

        // 頂点色で塗れて照明に左右されない組み込みのシェーダーを使う。どのパイプラインでも描け、ビルドにも常に入る。
        private static Material LineMaterial()
        {
            if (_material != null)
            {
                return _material;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning($"{LogTag} the built-in shader 'Sprites/Default' is missing; the range lines use the default material");
                return null;
            }

            _material = new Material(shader) { name = ObjectName, hideFlags = HideFlags.DontSave };
            return _material;
        }

        private static Color Fade(Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            return color;
        }

        private struct Blast
        {
            public Vector3 Center;
            public float Radius;
            public float FullDamageRadius;
            public float Elapsed;
        }
    }
}
