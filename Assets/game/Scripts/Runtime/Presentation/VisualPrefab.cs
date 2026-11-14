using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Runtime
{
    /// <summary>
    /// 仮の形の代わりに差し替えたプレハブを置く。見た目だけの物は弾・視線・移動を遮ってはいけないので、当たり判定を外して置ける。
    /// </summary>
    public static class VisualPrefab
    {
        /// <summary>
        /// prefab を parent の子に置き (ローカルの位置・向き・大きさはプレハブのまま)、名前を objectName にする。
        /// stripColliders なら子孫の当たり判定をすべて外す。
        /// </summary>
        public static GameObject Instantiate(GameObject prefab, Transform parent, string objectName, bool stripColliders)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = string.IsNullOrEmpty(objectName) ? prefab.name : objectName;
            if (stripColliders)
            {
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                {
                    // Destroy は次のフレームまで残り、その間に射撃や移動の判定に当たるので、すぐ消す。
                    Object.DestroyImmediate(collider);
                }
            }

            return instance;
        }

        /// <summary>root と子孫すべてのレイヤーを layer にする。</summary>
        public static void SetLayer(GameObject root, int layer)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }
    }
}
