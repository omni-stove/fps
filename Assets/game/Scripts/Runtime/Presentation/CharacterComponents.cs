using System;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キャラ (プレイヤー・NetPlayer・敵) の本体に付ける演出と行動のコンポーネントを決める。
    /// 実行時に組み立てる <see cref="GameBootstrap"/> と、シーン・プレハブの生成スクリプトが同じ組み合わせを付けるために使う。
    /// </summary>
    public static class CharacterComponents
    {
        /// <summary>
        /// body に <see cref="WeaponPresentation"/>・<see cref="CharacterAudio"/>・<see cref="CharacterAnimator"/> を付け、
        /// throwsGrenades なら <see cref="GrenadeThrower"/> も付ける。<see cref="ViewRig"/> を持つ本体 (プレイヤーと NetPlayer) には
        /// <see cref="CameraShake"/> も付ける。既に付いているものは付け直さない。
        /// </summary>
        /// <param name="body">キャラの本体 (ルート)。</param>
        /// <param name="throwsGrenades">手榴弾を投げるか。プレイヤーと NetPlayer は true、敵は false。</param>
        public static void Attach(GameObject body, bool throwsGrenades)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            AddIfMissing<WeaponPresentation>(body);
            AddIfMissing<CharacterAudio>(body);
            AddIfMissing<CharacterAnimator>(body);
            if (throwsGrenades)
            {
                AddIfMissing<GrenadeThrower>(body);
            }

            if (body.GetComponent<ViewRig>() != null)
            {
                AddIfMissing<CameraShake>(body);
            }
        }

        private static void AddIfMissing<T>(GameObject target)
            where T : Component
        {
            if (target.GetComponent<T>() == null)
            {
                target.AddComponent<T>();
            }
        }
    }
}
