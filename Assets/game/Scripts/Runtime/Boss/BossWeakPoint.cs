using System;
using UnityEngine;

namespace Fps.Runtime
{
    // 隠すときはコンポーネントでなく GameObject ごと切る。TargetDummy などの復活は子のコライダーと描画を一斉に有効へ戻すので、
    // コンポーネントの有効だけで隠すと、復活のたびに弱点が出てしまう。
    [DisallowMultipleComponent]
    public sealed class BossWeakPoint : MonoBehaviour
    {
        [SerializeField, Tooltip("段階 (BossPhaseEntry) が出す弱点を指す名前。空なら GameObject の名前")]
        private string _id = string.Empty;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;

        public bool IsExposed => gameObject.activeSelf;

        public void Initialize(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            _id = id;
        }

        public bool SetExposed(bool exposed)
        {
            if (gameObject.activeSelf == exposed)
            {
                return false;
            }

            gameObject.SetActive(exposed);
            return true;
        }
    }
}
