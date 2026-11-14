using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net
{
    /// <summary>
    /// 専用サーバーで要らない描画・音声のコンポーネント (Camera / AudioListener / AudioSource / Animator と、演出の
    /// <see cref="WeaponPresentation"/> / <see cref="ShellEjector"/> / <see cref="DryFireSound"/> / <see cref="CharacterAudio"/> /
    /// <see cref="CharacterAnimator"/>) を止める。
    /// Animator はプレハブに置いたリグ (NetPlayer) のもの。当たり判定はリグの関節を使わないので、止めても試合は変わらない。
    /// 止めるかどうかは <see cref="Fps.Core.NetStartup.ShouldDisablePresentation"/> で決め、ここは止める処理だけを持つ。
    /// </summary>
    public static class HeadlessPresentation
    {
        /// <summary>
        /// <paramref name="scene"/> のすべての GameObject (非アクティブを含む) から該当コンポーネントを探して無効にする。
        /// </summary>
        /// <returns>新たに無効にしたコンポーネントの数。</returns>
        public static int DisableIn(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            var disabled = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                disabled += DisableUnder(root);
            }

            return disabled;
        }

        /// <summary><paramref name="root"/> とその子孫 (非アクティブを含む) の該当コンポーネントを無効にする。</summary>
        /// <returns>新たに無効にしたコンポーネントの数。</returns>
        public static int DisableUnder(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            return Disable(root.GetComponentsInChildren<Camera>(true))
                + Disable(root.GetComponentsInChildren<AudioListener>(true))
                + Disable(root.GetComponentsInChildren<AudioSource>(true))
                + Disable(root.GetComponentsInChildren<Animator>(true))
                + Disable(root.GetComponentsInChildren<WeaponPresentation>(true))
                + Disable(root.GetComponentsInChildren<ShellEjector>(true))
                + Disable(root.GetComponentsInChildren<DryFireSound>(true))
                + Disable(root.GetComponentsInChildren<CharacterAudio>(true))
                + Disable(root.GetComponentsInChildren<CharacterAnimator>(true));
        }

        private static int Disable(Behaviour[] behaviours)
        {
            var disabled = 0;
            foreach (var behaviour in behaviours)
            {
                if (behaviour != null && behaviour.enabled)
                {
                    behaviour.enabled = false;
                    disabled++;
                }
            }

            return disabled;
        }
    }
}
