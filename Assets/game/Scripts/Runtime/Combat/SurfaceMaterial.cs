using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 弾が当たった面の材質 (着弾の火花・音・弾痕を材質ごとに出し分けるための Id)。コライダーの GameObject か、その親に付ける。
    /// 親に付ければ子のコライダーすべてに効き、子に付けたものがあればそちらが勝つ (<see cref="IdOf"/>)。
    /// 付けていないコライダーの材質は <see cref="DefaultId"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurfaceMaterial : MonoBehaviour
    {
        /// <summary>材質を決めていない面の Id。</summary>
        public const string DefaultId = "default";

        [SerializeField, Tooltip("材質の Id (例: concrete, metal, wood)。空なら default")]
        private string _id = DefaultId;

        /// <summary>材質の Id。空白だけ・null を入れると <see cref="DefaultId"/> になる。前後の空白は除く。</summary>
        public string Id
        {
            get => Normalize(_id);
            set => _id = Normalize(value);
        }

        /// <summary>
        /// collider の材質の Id。collider の GameObject から親へ辿って最初に見つけた <see cref="SurfaceMaterial"/> の Id で、
        /// 見つからない・collider が null (破棄済みを含む) なら <see cref="DefaultId"/>。
        /// </summary>
        public static string IdOf(Collider collider)
        {
            if (collider == null)
            {
                return DefaultId;
            }

            var material = collider.GetComponentInParent<SurfaceMaterial>(true);
            return material != null ? material.Id : DefaultId;
        }

        /// <summary>id を材質の Id として揃える。null・空白だけなら <see cref="DefaultId"/>、それ以外は前後の空白を除いたもの。</summary>
        public static string Normalize(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? DefaultId : id.Trim();
        }
    }
}
