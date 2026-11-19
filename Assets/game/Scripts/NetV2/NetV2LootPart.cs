using System;
using System.Collections.Generic;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    // 見た目はオフラインの LootContainer と ExtractionZone をそのまま使い、差し替えの見た目もコードの形も同じにする。
    // 中身を引くのも脱出を数えるのもサーバーなので、ここではスナップショットの状態を写すだけで、部品の Open や Runner は使わない
    public sealed class NetV2LootPart : INetV2ClientPart
    {
        public const string ObjectNamePrefix = "NetLoot-";

        public const string InteractActionPath = InputActionPaths.Interact;

        private readonly ClientLoot _loot;
        private readonly NetV2LootVisuals _visuals;
        private readonly Func<bool> _canAct;
        private readonly Func<Vector3?> _localPosition;
        private readonly Func<bool> _interactPressed;
        private readonly Action<string> _log;
        private readonly Dictionary<int, View> _views = new Dictionary<int, View>();
        private readonly HashSet<int> _listed = new HashSet<int>();
        private readonly List<int> _scratch = new List<int>();
        private InputAction _interactAction;
        private bool _disposed;

        // 押したかと自分の位置を引く口は、テストや別の入力から差し替えられるよう受け取る
        public NetV2LootPart(
            Func<IMessage, bool> send,
            Func<bool> canAct,
            NetV2LootVisuals visuals = null,
            Func<Vector3?> localPosition = null,
            Func<bool> interactPressed = null,
            Action<string> log = null,
            float answerSeconds = ClientLoot.DefaultAnswerSeconds)
        {
            _canAct = canAct ?? throw new ArgumentNullException(nameof(canAct));
            _visuals = visuals ?? new NetV2LootVisuals();
            _localPosition = localPosition ?? ReadLocalPosition;
            _interactPressed = interactPressed ?? ReadInteractPressed;
            _log = log ?? (line => Debug.Log(line));
            _loot = new ClientLoot(send ?? throw new ArgumentNullException(nameof(send)), _log, answerSeconds);
        }

        public ClientLoot Loot => _loot;

        public int ViewCount => _views.Count;

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null || !_loot.Apply(snapshot))
            {
                return;
            }

            _listed.Clear();
            foreach (var box in _loot.Boxes)
            {
                Show(box);
            }

            foreach (var point in _loot.ExtractionPoints)
            {
                Show(point);
            }

            _scratch.Clear();
            foreach (var id in _views.Keys)
            {
                if (!_listed.Contains(id))
                {
                    _scratch.Add(id);
                }
            }

            foreach (var id in _scratch)
            {
                Forget(id);
            }

            _scratch.Clear();
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            _loot.Tick(deltaSeconds);
            if (GameUi.IsPauseOpen || !_interactPressed() || !_canAct())
            {
                return;
            }

            var position = _localPosition();
            if (position.HasValue)
            {
                _loot.OpenNearest(NetV2Math.ToNumerics(position.Value));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var view in _views.Values)
            {
                view.Remove();
            }

            _views.Clear();
            _loot.Reset();
        }

        // 見た目が無ければ (消えたか、まだ出していなければ) null
        public GameObject ViewOf(int itemId)
        {
            return _views.TryGetValue(itemId, out var view) ? view.Root : null;
        }

        private void Show(ClientLootItem item)
        {
            _listed.Add(item.Id);
            if (_views.TryGetValue(item.Id, out var view))
            {
                // 次の試合でサーバーが Id を振り直すと、同じ Id で別の種類の物や閉じた箱が届くので、見た目から作り直す
                if (view.Kind == item.Kind && !(view.Box != null && view.Box.IsOpened && !item.Opened))
                {
                    Update(view, item);
                    return;
                }

                Forget(item.Id);
            }

            _views.Add(item.Id, item.IsBox ? CreateBox(item) : CreateExtractionPoint(item));
        }

        private void Update(View view, ClientLootItem item)
        {
            if (view.Root == null)
            {
                return;
            }

            view.Root.transform.position = NetV2Math.ToUnity(item.Position);
            if (view.Box != null)
            {
                if (item.Opened && !view.Box.IsOpened)
                {
                    view.Box.ShowOpened();
                    Quiet(view.Root);
                }

                return;
            }

            if (ExtractionZone.IsValidRadius(item.Radius) && !Mathf.Approximately(view.Zone.Radius, item.Radius))
            {
                view.Zone.Radius = item.Radius;
            }

            view.Zone.SetOpen(item.Opened);
        }

        private View CreateBox(ClientLootItem item)
        {
            var closed = _visuals.ClosedBoxOf(item.Kind);
            var opened = _visuals.OpenedBoxOf(item.Kind);
            var go = NewRoot(item);
            var container = go.AddComponent<LootContainer>();
            container.VisualPrefab = closed;
            container.OpenedVisualPrefab = opened;
            if (LootContainer.IsValidReach(item.Radius))
            {
                container.Reach = item.Radius;
            }

            go.SetActive(true);
            // Edit Mode では Awake が呼ばれないので、自分で組み立てる。Play Mode では Awake の後で、見た目は作り直さない
            container.Build();
            if (item.Opened)
            {
                container.ShowOpened();
            }

            Quiet(go);
            _log(LootClientLog.Look(item.Id, item.Kind, closed != null));
            return new View(go, item.Kind, container, null);
        }

        private View CreateExtractionPoint(ClientLootItem item)
        {
            var closed = _visuals.ClosedExtractionVisual;
            var open = _visuals.OpenExtractionVisual;
            var go = NewRoot(item);
            var zone = go.AddComponent<ExtractionZone>();
            // Radius を入れると見た目を組み立てるので、差し替えの見た目を先に渡す
            zone.ClosedVisualPrefab = closed;
            zone.OpenVisualPrefab = open;
            if (ExtractionZone.IsValidRadius(item.Radius))
            {
                zone.Radius = item.Radius;
            }

            go.SetActive(true);
            zone.Build();
            zone.SetOpen(item.Opened);
            _log(LootClientLog.Look(item.Id, item.Kind, closed != null || open != null));
            return new View(go, item.Kind, null, zone);
        }

        // 部品の Awake が差し替えの見た目を受け取る前に走らないよう、無効のまま組み立てる
        private static GameObject NewRoot(ClientLootItem item)
        {
            var go = new GameObject(ObjectNamePrefix + item.Kind + "-" + item.Id);
            go.SetActive(false);
            go.transform.SetPositionAndRotation(NetV2Math.ToUnity(item.Position), Quaternion.identity);
            return go;
        }

        // オフラインの箱は身を隠せる固い物だが、サーバーの移動はこの箱を知らないので、当たりを残すと手元の予測がずれる
        private static void Quiet(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private void Forget(int itemId)
        {
            if (_views.TryGetValue(itemId, out var view))
            {
                view.Remove();
                _views.Remove(itemId);
            }
        }

        private static Vector3? ReadLocalPosition()
        {
            var local = NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null;
            return local != null ? local.transform.position : (Vector3?)null;
        }

        private bool ReadInteractPressed()
        {
            if (_interactAction == null)
            {
                var asset = InputSystem.actions;
                _interactAction = asset != null ? asset.FindAction(InteractActionPath) : null;
            }

            return _interactAction != null && _interactAction.WasPressedThisFrame();
        }

        private sealed class View
        {
            public View(GameObject root, ObjectiveItemKind kind, LootContainer box, ExtractionZone zone)
            {
                Root = root;
                Kind = kind;
                Box = box;
                Zone = zone;
            }

            public GameObject Root { get; }

            public ObjectiveItemKind Kind { get; }

            public LootContainer Box { get; }

            public ExtractionZone Zone { get; }

            // Edit Mode (テスト) では Destroy を呼べないので、すぐに消す
            public void Remove()
            {
                if (Root == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(Root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }
            }
        }
    }
}
