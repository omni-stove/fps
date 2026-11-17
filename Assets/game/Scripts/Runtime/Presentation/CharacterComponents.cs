using System;
using UnityEngine;

namespace Shooter.Runtime
{
    // 実行時に組み立てる GameBootstrap と、シーン・プレハブの生成スクリプトが同じ組み合わせを付けるよう、付ける部品をここ 1 か所で決める。
    public static class CharacterComponents
    {
        public static void Attach(GameObject body, bool throwsGrenades)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            AddIfMissing<WeaponPresentation>(body);
            AddIfMissing<ShellEjector>(body);
            AddIfMissing<DryFireSound>(body);
            AddIfMissing<BloodPresenter>(body);
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
