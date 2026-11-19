using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public static class DefaultHudFactory
    {
        public const string HudObjectName = "HUD";

        private static bool _enabled = true;

        public static Hud Current { get; private set; }

        // 購読 (Register) とは別に持つのは、既定の UI の登録より先に入れても後に入れても効くようにするため。
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Sync();
                if (value)
                {
                    AttachActionDebug(GameSignals.LocalPlayer.Value);
                }
            }
        }

        // エディターの読み込みでは始めない。Edit Mode のテストが値を出すたびに、片付ける人のいない HUD がシーンに残るため。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register()
        {
            // GameSignals を空に戻すと購読者も外れるので、覚えている印ではなく外してから付け直す
            GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
            GameSignals.HudModel.Changed -= HandleHudModelChanged;
            GameSignals.LocalPlayer.Changed += HandleLocalPlayerChanged;
            GameSignals.HudModel.Changed += HandleHudModelChanged;
            Sync();
            AttachActionDebug(GameSignals.LocalPlayer.Value);
        }

        public static void Unregister()
        {
            GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
            GameSignals.HudModel.Changed -= HandleHudModelChanged;
            DestroyCurrent();
        }

        public static void AttachActionDebug(LocalPlayerSignal player)
        {
            if (!_enabled || player == null || player.Player == null || player.Actions == null)
            {
                return;
            }

            var overlay = player.Player.GetComponent<ActionDebugOverlay>();
            if (overlay == null)
            {
                overlay = player.Player.AddComponent<ActionDebugOverlay>();
            }

            overlay.Bind(player.Actions);
        }

        private static void HandleLocalPlayerChanged(LocalPlayerSignal player)
        {
            Sync(player != null);
            AttachActionDebug(player);
        }

        private static void HandleHudModelChanged(HudModel model)
        {
            Sync(model != null);
        }

        // 片付けの通知では作らない。シーンを閉じるときは HUD が先に壊れ、その後の OnDestroy の片付けで作り直すと、閉じたシーンに HUD が残るため。
        private static void Sync(bool mayCreate = true)
        {
            var wanted = _enabled && (GameSignals.LocalPlayer.HasValue || GameSignals.HudModel.HasValue);
            if (!wanted)
            {
                DestroyCurrent();
                return;
            }

            if (!mayCreate || Current != null || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            // 足す前に Current へ入れるのは、足すのに失敗しても、作った HUD を Unregister や次の片付けで消せるようにするため。
            Current = Hud.Create(HudObjectName);
            AddAircraftView(Current);
        }

        // Hud.AddViews に入れないのは、既定の HUD にだけ足し、Hud.Create で組むほかの HUD (オンラインの HUD など) の表示を変えずに済ませるため。
        public static AircraftHudView AddAircraftView(Hud hud)
        {
            if (hud == null)
            {
                throw new System.ArgumentNullException(nameof(hud));
            }

            var view = hud.GetComponent<AircraftHudView>();
            if (view == null)
            {
                view = hud.gameObject.AddComponent<AircraftHudView>();
            }

            // HUD はもう木を結び終えているので、後から足した表示は自分で結ぶ。Edit Mode では Start が走らないため、ここで結ぶ。
            var document = hud.GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (view.Element == null && root != null && root.childCount > 0)
            {
                view.Bind(root);
            }

            return view;
        }

        private static void DestroyCurrent()
        {
            var hud = Current;
            Current = null;
            if (hud == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(hud.gameObject);
                return;
            }

            // Edit Mode では Hud の OnDestroy が走らないので、HUD 用に作った PanelSettings もここで消す
            var panelSettings = hud.OwnedPanelSettings;
            Object.DestroyImmediate(hud.gameObject);
            if (panelSettings != null)
            {
                Object.DestroyImmediate(panelSettings);
            }
        }
    }
}
