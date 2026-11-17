using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 実行中に <see cref="ToggleKey"/> で、この端末のプレイヤーの視点 (<see cref="ViewRig.Mode"/>) を一人称と三人称で入れ替える。
    /// ルールは変えずシーンも読み直さないので、FPS の射撃場でも TPS の射撃場でも、その場で同じ状況を見比べられる。
    /// Editor と Development Build だけで (<see cref="IsSupported"/>)、シーンをまたいで 1 つ常駐する。専用サーバーでは作らない。
    /// 死亡カメラや乗り物のカメラが視点を借りている間 (ViewRig が無効) は入れ替えずに断る。借り手は返すときに借りる前の視点へ戻すため。
    /// 次にシーンを読むとルールの <see cref="GameRules.ViewMode"/> に戻る。読み直しても残したいときはルールのデバッグパネル (F1) の視点で選ぶ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewModeDebugToggle : MonoBehaviour
    {
        /// <summary>常駐させる GameObject の名前。</summary>
        public const string ObjectName = "ViewModeDebugToggle";

        /// <summary>
        /// 入れ替えるキー。入力アセットは変えず <see cref="Keyboard.current"/> を直接読む
        /// (ルールのデバッグパネルの F1・行動のデバッグ表示の F3・爆発の範囲の F4 と同じく)。
        /// </summary>
        public const Key ToggleKey = Key.F2;

        private static ViewModeDebugToggle _instance;

        /// <summary>この Build で使えるか。Editor と Development Build だけ true。</summary>
        public static bool IsSupported
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>常駐しているもの。作っていなければ null。</summary>
        public static ViewModeDebugToggle Instance => _instance;

        /// <summary>
        /// rig の視点を入れ替え、<see cref="ViewModeSwitch.FormatSwitchLog"/> をログに出して <see cref="ViewModeSwitch.Refusal.None"/> を返す。
        /// rig が無いかこの端末の視点でなければ <see cref="ViewModeSwitch.Refusal.NoLocalView"/>、
        /// 無効 (死亡カメラや乗り物のカメラが借りている) なら <see cref="ViewModeSwitch.Refusal.ViewBorrowed"/> を返して視点は変えない。
        /// </summary>
        public static ViewModeSwitch.Refusal Toggle(ViewRig rig)
        {
            var refusal = Check(rig);
            if (refusal != ViewModeSwitch.Refusal.None)
            {
                Debug.Log(ViewModeSwitch.FormatRefusalLog(refusal));
                return refusal;
            }

            var from = rig.Mode;
            var to = ViewModeSwitch.Other(from);
            rig.Mode = to;
            Debug.Log(ViewModeSwitch.FormatSwitchLog(from, to), rig);
            return ViewModeSwitch.Refusal.None;
        }

        /// <summary>シーンにあるこの端末のプレイヤーの視点 (<see cref="ViewRig.IsLocalView"/>)。無ければ null。</summary>
        public static ViewRig FindLocalRig()
        {
            foreach (var rig in FindObjectsByType<ViewRig>(FindObjectsInactive.Exclude))
            {
                if (rig.IsLocalView)
                {
                    return rig;
                }
            }

            return null;
        }

        private static ViewModeSwitch.Refusal Check(ViewRig rig)
        {
            if (rig == null || !rig.IsLocalView)
            {
                return ViewModeSwitch.Refusal.NoLocalView;
            }

            return rig.enabled ? ViewModeSwitch.Refusal.None : ViewModeSwitch.Refusal.ViewBorrowed;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        // GameBootstrap を改変せずにどのシーンでも使えるよう、シーンとは別に自分で常駐する。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!IsSupported || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild) || _instance != null)
            {
                return;
            }

            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            host.AddComponent<ViewModeDebugToggle>();
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
            if (keyboard != null && keyboard[ToggleKey].wasPressedThisFrame)
            {
                Toggle(FindLocalRig());
            }
        }
    }
}
