using System;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 自機の今のアクション (<see cref="PlayerActionReader.Current"/>) を仮の体の形で見せる自機モック。今は読み元を受け取るだけで、まだ何も動かさない。
    /// 読み元は <see cref="GameBootstrap"/> が <see cref="Bind"/> で渡す。渡されないまま Awake を迎えたら同じ GameObject から探し、
    /// 無ければエラーを出して自分を無効にする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMockBody : MonoBehaviour
    {
        public const string LogTag = "[MockBody]";

        [SerializeField, Tooltip("見せるアクションの読み元。GameBootstrap が配線する")]
        private PlayerActionReader _reader;

        /// <summary>見せるアクションの読み元。未配線なら null。</summary>
        public PlayerActionReader Reader => _reader;

        private void Awake()
        {
            if (_reader != null)
            {
                return;
            }

            _reader = GetComponent<PlayerActionReader>();
            if (_reader == null)
            {
                Debug.LogError($"{LogTag} '{name}' に PlayerActionReader がありません。見せるアクションはそこから読みます。", this);
                enabled = false;
            }
        }

        /// <summary>見せるアクションの読み元を渡す。</summary>
        public void Bind(PlayerActionReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            _reader = reader;
        }
    }
}
