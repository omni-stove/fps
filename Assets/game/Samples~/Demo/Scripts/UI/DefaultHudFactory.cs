using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// 既定の HUD の作り役。<see cref="GameSignals"/> にこの画面のプレイヤー (<see cref="GameSignals.LocalPlayer"/>) か、その HUD の値
    /// (<see cref="GameSignals.HudModel"/>。ネット対戦は部品の無いプレイヤーなのでこちらだけが出る) が出たら <see cref="Hud.Create"/> で HUD を
    /// <see cref="HudObjectName"/> の名前で 1 つ作り、どちらも消えたら片付ける。プレイヤーに <see cref="PlayerActionReader"/> があれば
    /// <see cref="ActionDebugOverlay"/> を付ける。画面の無い専用サーバーでは何も作らない。
    /// シーンを読み込む前に <see cref="Register"/> で購読を始める。自分の HUD を出すゲームは <see cref="Enabled"/> を false にする。
    /// </summary>
    public static class DefaultHudFactory
    {
        /// <summary>作る HUD の GameObject の名前。</summary>
        public const string HudObjectName = "HUD";

        private static bool _enabled = true;

        /// <summary>作って片付けていない HUD。無ければ null。</summary>
        public static Hud Current { get; private set; }

        /// <summary>
        /// 既定の HUD とデバッグ表示を出すか。自分の HUD を出すゲームは false にする (作った HUD も片付ける)。
        /// 購読 (<see cref="Register"/>) とは別に持つので、既定の UI の登録より先に入れても後に入れても効く。
        /// </summary>
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

        /// <summary>
        /// 購読を始め、今出ている値に合わせて HUD を作る (か片付ける)。購読していれば今の値に合わせ直すだけ。
        /// Play ではシーンを読む前に呼ばれる。Edit Mode (テスト) で HUD が要るときは自分で呼ぶ。
        /// <see cref="GameSignals"/> を空に戻すと購読も外れるので、その後にもう一度呼んでよい。
        /// </summary>
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

        /// <summary>購読をやめ、作った HUD を片付ける。</summary>
        public static void Unregister()
        {
            GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
            GameSignals.HudModel.Changed -= HandleHudModelChanged;
            DestroyCurrent();
        }

        /// <summary>
        /// player にデバッグ表示を付け、player の <see cref="PlayerActionReader"/> を渡す。付いていれば渡し直すだけ。
        /// player か読み元が無いか、<see cref="Enabled"/> でなければ何もしない。
        /// </summary>
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
            Sync();
            AttachActionDebug(player);
        }

        private static void HandleHudModelChanged(HudModel model)
        {
            Sync();
        }

        private static void Sync()
        {
            var wanted = _enabled && (GameSignals.LocalPlayer.HasValue || GameSignals.HudModel.HasValue);
            if (!wanted)
            {
                DestroyCurrent();
                return;
            }

            if (Current != null || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            Current = Hud.Create(HudObjectName);
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
