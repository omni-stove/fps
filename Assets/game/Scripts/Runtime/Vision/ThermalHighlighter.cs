using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 見え方を決めるのは VisionModeController で、ここはサーマルの間に敵の体の材質を差し替えて、終わったら元の材質に戻すだけ。
    [DisallowMultipleComponent]
    public sealed class ThermalHighlighter : MonoBehaviour
    {
        public const string LogTag = VisionLog.Tag;

        // 頂点色と色で塗れて照明に左右されない組み込みのシェーダー。どのパイプラインでも描け、ビルドにも常に入る。
        public const string CodeShaderName = "Sprites/Default";

        private static bool _warnedMissingShader;

        private readonly Dictionary<Damageable, List<Swap>> _highlighted = new Dictionary<Damageable, List<Swap>>();
        private readonly Dictionary<GameObject, List<Swap>> _bodies = new Dictionary<GameObject, List<Swap>>();
        private readonly HashSet<Renderer> _swapped = new HashSet<Renderer>();
        private readonly List<Damageable> _scratch = new List<Damageable>();
        private readonly List<GameObject> _bodyScratch = new List<GameObject>();
        private readonly HashSet<GameObject> _wantedBodies = new HashSet<GameObject>();

        private VisionVisualCatalog _catalog;
        private bool _catalogResolved;
        private VisionModeController _controller;
        private bool _subscribed;
        private Material _codeMaterial;
        private float _sinceRescan;

        public VisionVisualCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = VisionVisualCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                var wasActive = IsActive;
                if (wasActive)
                {
                    RestoreAll();
                }

                _catalog = value;
                _catalogResolved = true;
                if (wasActive)
                {
                    Rescan();
                }
            }
        }

        public VisionModeController Controller
        {
            get => _controller;
            set
            {
                if (ReferenceEquals(_controller, value))
                {
                    return;
                }

                Unsubscribe();
                _controller = value;
                Subscribe();
                Show(_controller != null ? _controller.Current.Mode : VisionMode.None);
            }
        }

        // テストや、場の Damageable を別の一覧で持つ側が差し込む。null なら場の有効な Damageable をすべて見る。
        public Func<IEnumerable<Damageable>> Candidates { get; set; }

        // オンラインの相手の体のように Damageable を持たず、敵かどうかを場の陣営ではなく外 (スナップショット) で決める体を差し込む。
        // 返した体だけを光らせ、返さなくなった・隠れた・消えた体は戻す。null なら Damageable だけを見る
        public Func<IEnumerable<GameObject>> Bodies { get; set; }

        public bool IsActive { get; private set; }

        public int HighlightedCount => _highlighted.Count + _bodies.Count;

        public Material CodeMaterial => _codeMaterial;

        public bool IsHighlighted(Damageable damageable)
        {
            return damageable != null && _highlighted.ContainsKey(damageable);
        }

        public bool IsHighlighted(GameObject body)
        {
            return body != null && _bodies.ContainsKey(body);
        }

        public void Show(VisionMode mode)
        {
            if (mode == VisionMode.Thermal)
            {
                if (IsActive)
                {
                    return;
                }

                IsActive = true;
                _sinceRescan = 0f;
                Rescan();
                return;
            }

            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            RestoreAll();
        }

        // 新しく現れた敵を光らせ、倒れた・消えた・味方になった体を戻す。光らせた体の数を返す。
        public int Rescan()
        {
            if (!IsActive)
            {
                return 0;
            }

            _sinceRescan = 0f;
            CollectWantedBodies();
            var restored = RestoreGone() + RestoreGoneBodies();
            var material = HighlightMaterial(out var codeMaterial);
            var added = 0;
            if (material != null)
            {
                foreach (var candidate in EnumerateCandidates())
                {
                    if (candidate == null || _highlighted.ContainsKey(candidate) || !UavReward.IsEnemyOf(gameObject, candidate))
                    {
                        continue;
                    }

                    var swaps = SwapRenderers(candidate.gameObject, material);
                    if (swaps != null)
                    {
                        _highlighted.Add(candidate, swaps);
                        added++;
                    }
                }

                foreach (var body in _wantedBodies)
                {
                    if (_bodies.ContainsKey(body))
                    {
                        continue;
                    }

                    var swaps = SwapRenderers(body, material);
                    if (swaps != null)
                    {
                        _bodies.Add(body, swaps);
                        added++;
                    }
                }
            }

            _wantedBodies.Clear();
            if (added > 0)
            {
                Debug.Log(VisionLog.Highlighted(added, HighlightedCount, codeMaterial), this);
            }

            if (restored > 0)
            {
                Debug.Log(VisionLog.Restored(restored, HighlightedCount), this);
            }

            return HighlightedCount;
        }

        private void OnEnable()
        {
            if (_controller == null)
            {
                _controller = GetComponent<VisionModeController>();
            }

            Subscribe();
            if (_controller != null)
            {
                Show(_controller.Current.Mode);
            }
        }

        private void LateUpdate()
        {
            // 付ける順で VisionModeController が後から足されても拾えるよう、見つかるまで探し直す。
            if (_controller == null)
            {
                Controller = GetComponent<VisionModeController>();
            }

            if (!IsActive)
            {
                return;
            }

            _sinceRescan += Time.deltaTime;
            if (_sinceRescan >= VisionVisualCatalog.ThermalRescanSecondsOf(Catalog))
            {
                Rescan();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            IsActive = false;
            RestoreAll();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            IsActive = false;
            RestoreAll();
            if (_codeMaterial != null)
            {
                DestroyObject(_codeMaterial);
                _codeMaterial = null;
            }
        }

        private void Subscribe()
        {
            if (_subscribed || _controller == null)
            {
                return;
            }

            _controller.Changed += HandleVisionChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_controller, null))
            {
                _controller.Changed -= HandleVisionChanged;
            }

            _subscribed = false;
        }

        private void HandleVisionChanged(VisionStatus status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                if (!ReferenceEquals(_controller, null))
                {
                    _controller.Changed -= HandleVisionChanged;
                }

                return;
            }

            Show(status.Mode);
        }

        private IEnumerable<Damageable> EnumerateCandidates()
        {
            if (Candidates != null)
            {
                return Candidates() ?? Array.Empty<Damageable>();
            }

            return FindObjectsByType<Damageable>(FindObjectsInactive.Exclude);
        }

        // 見えている体だけを残すので、隠れた (使い回しで止めた・見えなくなった) 体は返しても光らせない。
        private void CollectWantedBodies()
        {
            _wantedBodies.Clear();
            var source = Bodies;
            if (source == null)
            {
                return;
            }

            var bodies = source();
            if (bodies == null)
            {
                return;
            }

            foreach (var body in bodies)
            {
                if (body != null && body.activeInHierarchy && !body.transform.IsChildOf(transform))
                {
                    _wantedBodies.Add(body);
                }
            }
        }

        private int RestoreGoneBodies()
        {
            _bodyScratch.Clear();
            foreach (var pair in _bodies)
            {
                if (pair.Key == null || !_wantedBodies.Contains(pair.Key))
                {
                    _bodyScratch.Add(pair.Key);
                }
            }

            foreach (var body in _bodyScratch)
            {
                RestoreBody(body);
            }

            var count = _bodyScratch.Count;
            _bodyScratch.Clear();
            return count;
        }

        private void RestoreBody(GameObject body)
        {
            if (!_bodies.TryGetValue(body, out var swaps))
            {
                return;
            }

            _bodies.Remove(body);
            RestoreSwaps(swaps);
        }

        private List<Swap> SwapRenderers(GameObject root, Material material)
        {
            var swaps = new List<Swap>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // 線や粒は体ではないので、体の形を描く Renderer だけを差し替える。
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                // 敵の下に入れ子になった別の Damageable の体を、二重に覚えて戻し損ねないようにする。
                if (!_swapped.Add(renderer))
                {
                    continue;
                }

                var original = renderer.sharedMaterials;
                var replaced = new Material[Math.Max(1, original.Length)];
                for (var i = 0; i < replaced.Length; i++)
                {
                    replaced[i] = material;
                }

                renderer.sharedMaterials = replaced;
                swaps.Add(new Swap(renderer, original));
            }

            return swaps.Count > 0 ? swaps : null;
        }

        private int RestoreGone()
        {
            _scratch.Clear();
            foreach (var pair in _highlighted)
            {
                var damageable = pair.Key;
                // 使い回しのために隠された敵も、場から探し直す一覧に入らないので戻しておく。
                if (damageable == null
                    || damageable.IsDead
                    || !damageable.gameObject.activeInHierarchy
                    || !UavReward.IsEnemyOf(gameObject, damageable))
                {
                    _scratch.Add(damageable);
                }
            }

            foreach (var damageable in _scratch)
            {
                Restore(damageable);
            }

            var count = _scratch.Count;
            _scratch.Clear();
            return count;
        }

        private void RestoreAll()
        {
            var restored = HighlightedCount;
            if (restored == 0)
            {
                return;
            }

            _scratch.Clear();
            _scratch.AddRange(_highlighted.Keys);
            foreach (var damageable in _scratch)
            {
                Restore(damageable);
            }

            _scratch.Clear();
            _bodyScratch.Clear();
            _bodyScratch.AddRange(_bodies.Keys);
            foreach (var body in _bodyScratch)
            {
                RestoreBody(body);
            }

            _bodyScratch.Clear();
            Debug.Log(VisionLog.Restored(restored, 0), this);
        }

        private void Restore(Damageable damageable)
        {
            if (!_highlighted.TryGetValue(damageable, out var swaps))
            {
                return;
            }

            _highlighted.Remove(damageable);
            RestoreSwaps(swaps);
        }

        private void RestoreSwaps(List<Swap> swaps)
        {
            foreach (var swap in swaps)
            {
                _swapped.Remove(swap.Renderer);
                if (swap.Renderer != null)
                {
                    swap.Renderer.sharedMaterials = swap.Original;
                }
            }
        }

        private Material HighlightMaterial(out bool codeMaterial)
        {
            var catalog = Catalog;
            if (catalog != null && catalog.ThermalMaterial != null)
            {
                codeMaterial = false;
                return catalog.ThermalMaterial;
            }

            codeMaterial = true;
            var color = VisionVisualCatalog.ThermalHighlightColorOf(catalog);
            if (_codeMaterial != null)
            {
                _codeMaterial.color = color;
                return _codeMaterial;
            }

            var shader = Shader.Find(CodeShaderName);
            if (shader == null)
            {
                if (!_warnedMissingShader)
                {
                    _warnedMissingShader = true;
                    Debug.LogWarning($"{LogTag} the built-in shader '{CodeShaderName}' is missing; thermal vision does not highlight enemies without a catalog material", this);
                }

                return null;
            }

            _codeMaterial = new Material(shader) { name = "ThermalHighlight", hideFlags = HideFlags.DontSave, color = color };
            return _codeMaterial;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _warnedMissingShader = false;
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private readonly struct Swap
        {
            public Swap(Renderer renderer, Material[] original)
            {
                Renderer = renderer;
                Original = original;
            }

            public Renderer Renderer { get; }

            public Material[] Original { get; }
        }
    }
}
