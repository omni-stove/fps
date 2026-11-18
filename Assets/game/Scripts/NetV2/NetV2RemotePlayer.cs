using System;
using Shooter.Core;
using Shooter.Runtime;
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
        private float _leanAngleDegrees;
        private Transform _body;
        private Vector3 _standingScale;
        private float _standingY;
        private PlayerStance _stance = PlayerStance.Standing;
        private Quaternion _tilt = Quaternion.identity;
        private bool _snapTilt = true;

        public int PlayerId { get; private set; }

        public Quaternion Tilt => _tilt;

        public Transform Body => _body;

        // スナップショットが載せてきた見た目の Id。体を替えられなかった (カタログに無い・絵が無い) ときも Id は残し、同じ Id で引き直さない。
        public string CosmeticId { get; private set; } = string.Empty;

        // 引く口を差し替えられるのは、テストと、見た目の引き方を変えたいゲームのため。
        public Func<string, GameObject> SkinSource { get; set; }

        // 傾ける角度はオフラインと同じ移動の値 (MotorSpec.LeanAngleDegrees) を、サーバーのルールから受け取る。
        public static NetV2RemotePlayer Create(int playerId, StanceGeometry geometry, MotorSpec motor, Transform parent)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            var root = new GameObject("NetV2Remote" + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            root.transform.SetParent(parent, false);
            var remote = root.AddComponent<NetV2RemotePlayer>();
            remote.PlayerId = playerId;
            remote._geometry = geometry;
            remote._leanAngleDegrees = Mathf.Clamp(motor.LeanAngleDegrees, 0f, 90f);
            remote.SetBody(CreateBody(null, geometry, root.transform));
            NetV2LocalPlayer.AttachPresentation(root, false);
            return remote;
        }

        public void ShowCosmetic(string cosmeticId)
        {
            var id = cosmeticId ?? string.Empty;
            if (string.Equals(id, CosmeticId, StringComparison.Ordinal))
            {
                return;
            }

            CosmeticId = id;
            var source = SkinSource ?? (Func<string, GameObject>)CosmeticLooks.BodyOf;
            var skin = id.Length == 0 ? null : source(id);
            var previous = _body;
            SetBody(CreateBody(skin, _geometry, transform));

            // Destroy はフレームの終わりまで残り、新しい体と重なって見えるので先に隠す
            previous.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(previous.gameObject);
            }
            else
            {
                DestroyImmediate(previous.gameObject);
            }

            Debug.Log(CosmeticLog.RemoteWorn(PlayerId, id, skin != null ? skin.name : null), this);
        }

        private void SetBody(Transform body)
        {
            _body = body;
            _standingScale = body.localScale;
            _standingY = body.localPosition.y;
            if (_stance != PlayerStance.Standing)
            {
                ApplyStance(_stance);
            }
            else
            {
                PlaceBody();
            }
        }

        public void Show(RemotePose pose)
        {
            transform.SetPositionAndRotation(NetV2Math.ToUnity(pose.Position), NetV2Math.YawRotation(pose.Yaw));
            if (pose.Stance != _stance)
            {
                ApplyStance(pose.Stance);
            }

            // 泳ぎと壁走りは補間されず 1 tick で切り替わるので、オフラインの CharacterAnimator と同じ速さで寄せる。
            // 出てきた直後は前の傾きが無いので、起き上がった姿から倒れ込まないよう、そのまま置く。
            var target = PlayerMockPose.TraversalLean(pose.Lean, pose.Swimming, pose.WallRun, _leanAngleDegrees);
            _tilt = _snapTilt ? target : Quaternion.Slerp(_tilt, target, PlayerMockPose.BlendFactor(Time.deltaTime));
            _snapTilt = false;
            PlaceBody();
        }

        private static Transform CreateBody(GameObject skin, StanceGeometry geometry, Transform parent)
        {
            var prefab = skin != null ? skin : Resources.Load<GameObject>(BodyResourcePath);
            GameObject body;
            if (prefab != null)
            {
                body = Instantiate(prefab, parent, false);
                if (skin != null)
                {
                    // スキンは他のゲームの体を流用して当たりが付いていることがあり、自分の体と射撃の Ray に混ざるので捨てる
                    foreach (var collider in body.GetComponentsInChildren<Collider>(true))
                    {
                        collider.enabled = false;
                        DestroyCollider(collider);
                    }
                }
            }
            else
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.transform.SetParent(parent, false);

                // 当たりは .fpsmap とサーバーが取るので、見た目に Unity の物理を持たせない
                DestroyCollider(body.GetComponent<Collider>());
                var standing = geometry.Standing;
                body.transform.localScale = new Vector3(geometry.Radius * 2f, standing.Height * 0.5f, geometry.Radius * 2f);
                body.transform.localPosition = new Vector3(0f, standing.CenterY, 0f);
            }

            body.name = _bodyName;
            return body.transform;
        }

        private static void DestroyCollider(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        private void ApplyStance(PlayerStance stance)
        {
            _stance = stance;
            var standing = _geometry.Standing;
            var shape = _geometry.Shape(stance);
            var scale = _standingScale;
            scale.y *= shape.Height / standing.Height;
            _body.localScale = scale;
            PlaceBody();
        }

        // オフラインの体と同じく足元を軸に回すため、見た目の位置も傾きで回す。見た目だけを回すと体の中心を軸に回り、足が地面から浮く。
        private void PlaceBody()
        {
            var standing = _geometry.Standing;
            var shape = _geometry.Shape(_stance);
            var upright = new Vector3(0f, _standingY + shape.CenterY - standing.CenterY, 0f);
            var feet = new Vector3(0f, shape.BottomY, 0f);
            _body.localPosition = feet + (_tilt * (upright - feet));
            _body.localRotation = _tilt;
        }
    }
}
