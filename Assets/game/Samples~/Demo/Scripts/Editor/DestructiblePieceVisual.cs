using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // シーンを組み直すと前のシーンごと部品を捨てるので、壊れる物の見た目はシーンの外 (PrefabPath のプレハブ) から読む。
    // 3 つのビルダーで 1 つにしているのは、部品はどれも 1m の箱を拡大したもので、同じ見た目を拡大して使えるため。
    public static class DestructiblePieceVisual
    {
        public static string PrefabPath => ShooterKitRoot.OutputPath("Prefabs/DestructiblePiece.prefab");

        public static GameObject Load()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
    }
}
