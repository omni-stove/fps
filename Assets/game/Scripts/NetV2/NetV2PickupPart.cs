using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    // 地面の拾い物と弾薬箱を描き、Interact で拾う要求を送る。拾えるか・何が入るかはサーバー (ServerPickups) が決めるので、
    // オフラインの Pickup・AmmoCrate は使わない (どちらも手元の体に中身を入れ、触れた体のトリガーで拾うため)。
    // 見た目は差し替えの見た目 (NetV2PickupVisuals) か、オフラインと同じ形と色をコードで作り、当たりは付けない
    public sealed class NetV2PickupPart : INetV2ClientPart
    {
        public const string ObjectNamePrefix = "NetPickup-";

        public const string InteractActionPath = InputActionPaths.Interact;

        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _colorId = Shader.PropertyToID("_Color");

        private readonly ClientPickups _pickups;
        private readonly NetV2PickupVisuals _visuals;
        private readonly Func<bool> _canAct;
        private readonly Func<Vector3?> _localPosition;
        private readonly Func<bool> _interactPressed;
        private readonly Action<string> _log;
        private readonly Dictionary<int, View> _views = new Dictionary<int, View>();
        private readonly HashSet<int> _listed = new HashSet<int>();
        private readonly List<int> _scratch = new List<int>();
        private InputAction _interactAction;
        private bool _disposed;

        // 押したかと自分の位置 (足元) を引く口は、テストや別の入力から差し替えられるよう受け取る
        public NetV2PickupPart(
            Func<IMessage, bool> send,
            Func<bool> canAct,
            NetV2PickupVisuals visuals = null,
            Func<Vector3?> localPosition = null,
            Func<bool> interactPressed = null,
            Action<string> log = null,
            float answerSeconds = ClientPickups.DefaultAnswerSeconds)
        {
            _canAct = canAct ?? throw new ArgumentNullException(nameof(canAct));
            _visuals = visuals ?? new NetV2PickupVisuals();
            _localPosition = localPosition ?? ReadLocalPosition;
            _interactPressed = interactPressed ?? ReadInteractPressed;
            _log = log ?? (line => Debug.Log(line));
            _pickups = new ClientPickups(send ?? throw new ArgumentNullException(nameof(send)), _log, answerSeconds);
        }

        public ClientPickups Pickups => _pickups;

        // NetV2ClientParts.Registered に並べる口。再生では拾う要求を受ける相手がいないので、箱 (NetV2LootPart) と同じく見せるだけにする
        public static INetV2ClientPart Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var client = context.Client;
            var localPlayer = context.LocalPlayer;
            return new NetV2PickupPart(
                context.Send,
                () => !context.Replay && NetV2ClientParts.CanAct(client),
                context.PickupVisuals,
                localPlayer != null ? () => LocalPositionOf(localPlayer) : (Func<Vector3?>)null,
                log: context.Log);
        }

        public int ViewCount => _views.Count;

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null || !_pickups.Apply(snapshot))
            {
                return;
            }

            _listed.Clear();
            foreach (var item in _pickups.Items)
            {
                Show(item);
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

            _pickups.Tick(deltaSeconds);
            Spin(deltaSeconds);
            var position = _localPosition();
            var canAct = _canAct();
            KeySharing.SetClaim(
                InputActionPaths.Interact,
                this,
                canAct && position.HasValue && _pickups.TryFindNearest(NetV2Math.ToNumerics(position.Value), out _));
            if (GameUi.IsPauseOpen || !canAct || !_interactPressed())
            {
                return;
            }

            if (position.HasValue)
            {
                _pickups.CollectNearest(NetV2Math.ToNumerics(position.Value));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            KeySharing.Release(InputActionPaths.Interact, this);
            foreach (var view in _views.Values)
            {
                view.Remove();
            }

            _views.Clear();
            _pickups.Reset();
        }

        // 見た目が無ければ (消えたか、まだ出していなければ) null
        public GameObject ViewOf(int itemId)
        {
            return _views.TryGetValue(itemId, out var view) ? view.Root : null;
        }

        private void Show(ClientPickupItem item)
        {
            _listed.Add(item.Id);
            if (_views.TryGetValue(item.Id, out var view))
            {
                // 次の試合でサーバーが Id を振り直すと、同じ Id で別の種類の物が届くので、見た目から作り直す
                if (view.Kind == item.Kind)
                {
                    Update(view, item);
                    return;
                }

                Forget(item.Id);
            }

            _views.Add(item.Id, Create(item));
        }

        // 拾い物は拾われて出直しを待つ間だけ隠す。弾薬箱は身を隠す物として置いたままにする
        private static void Update(View view, ClientPickupItem item)
        {
            if (view.Root == null)
            {
                return;
            }

            view.Root.transform.position = NetV2Math.ToUnity(item.Position);
            if (!item.IsAmmoCrate && view.Root.activeSelf == item.Opened)
            {
                view.Root.SetActive(!item.Opened);
            }
        }

        private View Create(ClientPickupItem item)
        {
            var go = new GameObject(ObjectNamePrefix + item.Kind + "-" + item.Id);
            go.transform.SetPositionAndRotation(NetV2Math.ToUnity(item.Position), Quaternion.identity);
            var prefab = _visuals.Of(item.Kind);
            Transform visual;
            if (prefab != null)
            {
                visual = VisualPrefab.Instantiate(prefab, go.transform, Pickup.VisualName, stripColliders: true).transform;
                visual.localPosition = item.IsAmmoCrate ? Vector3.zero : new Vector3(0f, Pickup.VisualHeight, 0f);
            }
            else
            {
                visual = item.IsAmmoCrate ? CreateCrate(go.transform) : CreatePlaceholder(go.transform, item);
            }

            if (!item.IsAmmoCrate && item.Opened)
            {
                go.SetActive(false);
            }

            _log(PickupClientLog.Look(item.Id, item.Kind, prefab != null));
            return new View(go, visual, item.Kind);
        }

        private void Spin(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            foreach (var view in _views.Values)
            {
                if (view.Root != null && view.Visual != null && view.Kind != ObjectiveItemKind.AmmoCrate && view.Root.activeSelf)
                {
                    view.Visual.Rotate(0f, Pickup.SpinDegreesPerSecond * deltaSeconds, 0f, Space.World);
                }
            }
        }

        private static Transform CreatePlaceholder(Transform root, ClientPickupItem item)
        {
            item.TryGetPickupKind(out var kind);
            if (kind != PickupKind.Weapon)
            {
                var box = CreatePart(Pickup.VisualName, root, Vector3.one * Pickup.VisualSize, Pickup.ColorOf(kind));
                box.localPosition = new Vector3(0f, Pickup.VisualHeight, 0f);
                return box;
            }

            // オフラインの Pickup の仮の銃と同じ形
            var gun = new GameObject(Pickup.VisualName).transform;
            gun.SetParent(root, false);
            gun.localPosition = new Vector3(0f, Pickup.VisualHeight, 0f);
            var color = PlayerMockBody.GunColor;
            CreatePart("Body", gun, Pickup.GunBodySize, color);
            var barrel = CreatePart("Barrel", gun, Pickup.GunBarrelSize, color);
            barrel.localPosition = new Vector3(0f, Pickup.GunBodySize.y / 4f, (Pickup.GunBodySize.z + Pickup.GunBarrelSize.z) / 2f);
            var grip = CreatePart("Grip", gun, Pickup.GunGripSize, color);
            grip.localPosition = new Vector3(0f, -(Pickup.GunBodySize.y + Pickup.GunGripSize.y) / 2f, -Pickup.GunBodySize.z / 6f);
            return gun;
        }

        private static Transform CreateCrate(Transform root)
        {
            var crate = CreatePart(AmmoCrate.VisualName, root, AmmoCrate.DefaultVisualSize, AmmoCrate.DefaultVisualColor);
            crate.localPosition = new Vector3(0f, AmmoCrate.DefaultVisualSize.y / 2f, 0f);
            return crate;
        }

        // サーバーの移動はこの物を知らないので、当たりを残すと手元の予測がずれる。作った直後に外す
        private static Transform CreatePart(string partName, Transform parent, Vector3 size, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localScale = size;
            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 共有マテリアルを塗り替えると他のプリミティブまで色が変わるので、この物だけの上書きで色を付ける
            var block = new MaterialPropertyBlock();
            block.SetColor(_baseColorId, color);
            block.SetColor(_colorId, color);
            renderer.SetPropertyBlock(block);
            return part.transform;
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
            return LocalPositionOf(() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
        }

        // 破棄済みの Unity の参照も無いものとして扱うため、?. ではなく != null で確かめる
        private static Vector3? LocalPositionOf(Func<NetV2LocalPlayer> localPlayer)
        {
            var local = localPlayer();
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
            public View(GameObject root, Transform visual, ObjectiveItemKind kind)
            {
                Root = root;
                Visual = visual;
                Kind = kind;
            }

            public GameObject Root { get; }

            public Transform Visual { get; }

            public ObjectiveItemKind Kind { get; }

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
