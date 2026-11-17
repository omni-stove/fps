using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Runtime
{
    // 見た目だけの物は弾・視線・移動を遮ってはいけないので、当たり判定を外して置けるようにする。
    public static class VisualPrefab
    {
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
