using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.NetV2
{
    // プレハブの見た目は立ったときの高さで作ってある前提なので、しゃがみと伏せは見た目を縦に縮めて下げて表す。
    [DisallowMultipleComponent]
    public sealed class NetV2RemotePlayer : MonoBehaviour
    {
        // プレハブはルートの原点を足元ではなく本体の位置 (移動の位置) に合わせて作る前提で、そのまま本体に重ねる。
        public const string BodyResourcePath = "NetV2/RemotePlayerBody";

        private const string _bodyName = "Body";

        private StanceGeometry _geometry;
        private Transform _body;
        private Vector3 _standingScale;
        private float _standingY;
        private PlayerStance _stance = PlayerStance.Standing;

        public int PlayerId { get; private set; }

        public static NetV2RemotePlayer Create(int playerId, StanceGeometry geometry, Transform parent)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var root = new GameObject("NetV2Remote" + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            root.transform.SetParent(parent, false);
            var remote = root.AddComponent<NetV2RemotePlayer>();
            remote.PlayerId = playerId;
            remote._geometry = geometry;
            remote._body = CreateBody(geometry, root.transform);
            remote._standingScale = remote._body.localScale;
            remote._standingY = remote._body.localPosition.y;
            NetV2LocalPlayer.AttachPresentation(root, false);
            return remote;
        }

        public void Show(RemotePose pose)
        {
            transform.SetPositionAndRotation(NetV2Math.ToUnity(pose.Position), NetV2Math.YawRotation(pose.Yaw));
            if (pose.Stance != _stance)
            {
                ApplyStance(pose.Stance);
            }
        }

        private static Transform CreateBody(StanceGeometry geometry, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(BodyResourcePath);
            GameObject body;
            if (prefab != null)
            {
                body = Instantiate(prefab, parent, false);
            }
            else
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.transform.SetParent(parent, false);

                // 当たりは .fpsmap とサーバーが取るので、見た目に Unity の物理を持たせない
                Destroy(body.GetComponent<Collider>());
                var standing = geometry.Standing;
                body.transform.localScale = new Vector3(geometry.Radius * 2f, standing.Height * 0.5f, geometry.Radius * 2f);
                body.transform.localPosition = new Vector3(0f, standing.CenterY, 0f);
            }

            body.name = _bodyName;
            return body.transform;
        }

        private void ApplyStance(PlayerStance stance)
        {
            _stance = stance;
            var standing = _geometry.Standing;
            var shape = _geometry.Shape(stance);
            var scale = _standingScale;
            scale.y *= shape.Height / standing.Height;
            _body.localScale = scale;
            _body.localPosition = new Vector3(0f, _standingY + shape.CenterY - standing.CenterY, 0f);
        }
    }
}
