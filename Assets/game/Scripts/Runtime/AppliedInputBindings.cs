using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 設定画面で変えた割り当ての上書き (<see cref="InputBindingOverridesStore"/>) を、プロジェクト全体の Input Actions (<see cref="InputSystem.actions"/>) に入れる。
    /// オフラインのプレイヤー (<see cref="PlayerController"/> など) もオンラインのプレイヤー (Fps.Net の NetPlayer など) も同じアセットのアクションを使うので、
    /// シーンを読む前 (どのプレイヤーの Awake よりも前) に 1 回入れれば両方に効く。
    /// </summary>
    public static class AppliedInputBindings
    {
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static bool _loaded;

        /// <summary>この Play で読み込みを済ませたか。</summary>
        public static bool IsLoaded => _loaded;

        /// <summary>
        /// Play 中で、まだ読み込んでいなければ、既定のキーから <see cref="InputSystem.actions"/> に読み込む。
        /// シーンを読む前に自動で呼ぶが、プレイヤーが入力を解決する前にも呼び、読み込みの順番をそこで確かにする。
        /// </summary>
        public static void EnsureLoaded()
        {
            // Edit Mode のテストがプロジェクト全体のアクションを使うので、開発者が保存した上書きをそこへ入れない。
            if (_loaded || !Application.isPlaying)
            {
                return;
            }

            _loaded = true;
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning(
                    $"{PlayerBindings.LogTag} project-wide Input Actions are not set (Project Settings > Input System Package); saved bindings are not applied");
                return;
            }

            var store = new InputBindingOverridesStore();
            try
            {
                if (store.Load(actions))
                {
                    Debug.Log($"{PlayerBindings.LogTag} applied saved binding overrides from '{store.Key}'");
                }
            }
            catch (Exception e)
            {
                // 読めなくても既定の割り当てで遊べるので、起動は止めない。
                Debug.LogError($"{PlayerBindings.LogTag} could not apply saved binding overrides from '{store.Key}': {e.Message}");
                Debug.LogException(e);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _loaded = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnPlay()
        {
            EnsureLoaded();
        }
    }
}
