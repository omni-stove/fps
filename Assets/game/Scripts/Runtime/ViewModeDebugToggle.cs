using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // ルールは変えずシーンも読み直さないのは、FPS の射撃場でも TPS の射撃場でも、その場で同じ状況を見比べられるようにするため。
    // 死亡カメラや乗り物のカメラが視点を借りている間 (ViewRig が無効) は入れ替えずに断る。借り手は返すときに借りる前の視点へ戻すため。
    [DisallowMultipleComponent]
    public sealed class ViewModeDebugToggle : MonoBehaviour
    {
        public const string ObjectName = "ViewModeDebugToggle";

        // ルールのデバッグパネルの F1・行動のデバッグ表示の F3・爆発の範囲の F4 と同じく、入力アセットは変えず Keyboard.current を直接読む。
        public const Key ToggleKey = Key.F2;

        private static ViewModeDebugToggle _instance;

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

        public static ViewModeDebugToggle Instance => _instance;

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
