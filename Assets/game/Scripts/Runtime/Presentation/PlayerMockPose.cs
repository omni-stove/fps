using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>部品 1 つの親のローカル座標での位置と向き。</summary>
    public readonly struct MockPartPose : IEquatable<MockPartPose>
    {
        public MockPartPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public bool Equals(MockPartPose other)
        {
            return Position == other.Position && Rotation == other.Rotation;
        }

        public override bool Equals(object obj)
        {
            return obj is MockPartPose other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Position, Rotation);
        }

        public override string ToString()
        {
            return $"pos={Position} rot={Rotation.eulerAngles}";
        }
    }

    /// <summary>
    /// 自機モック (<see cref="PlayerMockBody"/>) の寸法と、<see cref="PlayerActionState"/> から作る姿勢。状態を持たない計算だけを置く。
    /// 三人称の体は足元を原点とする本体のローカル座標 (+Z が正面)、一人称の銃 (ビューモデル) はカメラのローカル座標で表す。
    /// 同時に立つ状態の優先は ダウン &gt; 乗り越え &gt; 回避 &gt; カバー。しゃがみはそれらと重ねて胴を縮める。
    /// </summary>
    public static class PlayerMockPose
    {
        /// <summary>胴 (カプセル) の幅・奥行き・高さ (m)。立っているとき。</summary>
        public const float TorsoWidth = 0.6f;
        public const float TorsoDepth = 0.4f;
        public const float TorsoHeight = 1.4f;

        /// <summary>胴の下端の足元からの高さ (m)。</summary>
        public const float TorsoBottom = 0.05f;

        /// <summary>頭 (球) の直径 (m)。</summary>
        public const float HeadDiameter = 0.36f;

        /// <summary>
        /// しゃがんだときに胴の高さに掛ける倍率。<see cref="PlayerController.DefaultCrouchHeight"/> と
        /// <see cref="GameBootstrap.PlayerHeight"/> の比に揃え、当たり判定の縮み方と見た目を合わせる。
        /// </summary>
        public const float CrouchScale = PlayerController.DefaultCrouchHeight / GameBootstrap.PlayerHeight;

        /// <summary>立っているときの目の足元からの高さ (m)。ADS で銃をここまで上げる。</summary>
        public const float EyeHeight = 1.6f;

        /// <summary>銃 (箱) の幅・高さ・長さ (m)。三人称と一人称で同じ寸法を使う。</summary>
        public static readonly Vector3 GunSize = new Vector3(0.08f, 0.12f, 0.55f);

        public const float DodgeLeanDegrees = 15f;
        public const float VaultLeanDegrees = 25f;
        public const float CoverLeanDegrees = 5f;

        /// <summary>ダウンで横に倒す角度。真横 (90) だと体が床にめり込むので少し手前で止める。</summary>
        public const float DownedRollDegrees = 80f;

        /// <summary>カバー中に体を壁へ寄せる距離 (m)。</summary>
        public const float CoverShift = 0.2f;

        /// <summary>1 発撃つたびに銃を後ろへ引く距離 (m) と、跳ね上げる角度。三人称の体。</summary>
        public const float BodyKickDistance = 0.08f;
        public const float BodyKickDegrees = 6f;

        /// <summary>1 発撃つたびに銃を後ろへ引く距離 (m) と、跳ね上げる角度。一人称のビューモデル。</summary>
        public const float ViewKickDistance = 0.06f;
        public const float ViewKickDegrees = 8f;

        /// <summary>撃った反動が戻りきるまでの秒数。</summary>
        public const float KickRecoverSeconds = 0.12f;

        /// <summary>姿勢を目標へ寄せる速さ (1/秒)。大きいほど早く追いつく。</summary>
        public const float BlendRate = 14f;

        /// <summary>三人称の銃の位置 (足元からの、立っているとき)。高さはしゃがみで縮める。</summary>
        public static readonly Vector3 BodyHipGunPosition = new Vector3(0.25f, 1.15f, 0.3f);

        /// <summary>三人称で ADS したときの銃の位置。目の高さの少し下、体の中心寄り。</summary>
        public static readonly Vector3 BodyAimGunPosition = new Vector3(0.12f, EyeHeight - 0.08f, 0.35f);

        public static readonly Vector3 BodyReloadGunPosition = new Vector3(0.25f, 0.95f, 0.2f);
        public static readonly Vector3 BodyReloadGunEuler = new Vector3(35f, 0f, -25f);

        /// <summary>一人称の銃の位置 (カメラのローカル)。腰だめは右下。</summary>
        public static readonly Vector3 ViewHipPosition = new Vector3(0.22f, -0.2f, 0.45f);

        /// <summary>一人称で ADS したときの銃の位置。画面の中央の真下。</summary>
        public static readonly Vector3 ViewAimPosition = new Vector3(0f, -0.1f, 0.32f);

        public static readonly Vector3 ViewReloadPosition = new Vector3(0.22f, -0.36f, 0.4f);
        public static readonly Vector3 ViewReloadEuler = new Vector3(30f, 0f, -20f);

        /// <summary>胴の高さに掛ける倍率。しゃがみなら <see cref="CrouchScale"/>、それ以外は 1。</summary>
        public static float StanceScale(PlayerActionState state)
        {
            return state.IsCrouching && !state.IsDowned ? CrouchScale : 1f;
        }

        /// <summary>stanceScale の高さのときの胴の位置 (足元から) と拡縮 (Unity のカプセルは直径 1・高さ 2)。</summary>
        public static void Torso(float stanceScale, out Vector3 position, out Vector3 scale)
        {
            RequirePositiveFinite(stanceScale, nameof(stanceScale));
            var height = TorsoHeight * stanceScale;
            position = new Vector3(0f, (TorsoBottom * stanceScale) + (height / 2f), 0f);
            scale = new Vector3(TorsoWidth, height / 2f, TorsoDepth);
        }

        /// <summary>stanceScale の高さのときの頭の中心 (足元から)。胴の上に載せる。</summary>
        public static Vector3 Head(float stanceScale)
        {
            RequirePositiveFinite(stanceScale, nameof(stanceScale));
            return new Vector3(0f, ((TorsoBottom + TorsoHeight) * stanceScale) + (HeadDiameter / 2f), 0f);
        }

        /// <summary>
        /// 体全体の傾き (足元を軸に回す)。ダウンは横へ倒し、乗り越えは前へ、回避は localMove の向きへ、カバーは壁 (localCoverNormal の逆) へ傾ける。
        /// localMove は本体のローカルの移動の向き (水平成分が無ければ前とみなす)、localCoverNormal は壁の面の向き (壁から離れる向き)。
        /// </summary>
        public static Quaternion BodyLean(PlayerActionState state, Vector3 localMove, Vector3 localCoverNormal)
        {
            if (state.IsDowned)
            {
                return Quaternion.Euler(0f, 0f, DownedRollDegrees);
            }

            if (state.IsVaulting)
            {
                return Quaternion.Euler(VaultLeanDegrees, 0f, 0f);
            }

            if (state.IsDodging)
            {
                var direction = Horizontal(localMove);
                return LeanToward(direction == Vector3.zero ? Vector3.forward : direction, DodgeLeanDegrees);
            }

            if (state.IsInCover)
            {
                var wall = -Horizontal(localCoverNormal);
                return wall == Vector3.zero ? Quaternion.identity : LeanToward(wall, CoverLeanDegrees);
            }

            return Quaternion.identity;
        }

        /// <summary>体全体をずらす量。カバー中 (ダウンを除く) は壁へ <see cref="CoverShift"/> 寄せる。</summary>
        public static Vector3 BodyShift(PlayerActionState state, Vector3 localCoverNormal)
        {
            if (!state.IsInCover || state.IsDowned)
            {
                return Vector3.zero;
            }

            return -Horizontal(localCoverNormal) * CoverShift;
        }

        /// <summary>
        /// 三人称の銃の姿勢 (反動を除く)。リロード中とダウン中は下げて傾け、ADS なら目の高さへ上げ、それ以外は腰だめ。
        /// 下げていないときは aimPitch (目の上下の向き) に合わせて銃口を上下させる。
        /// </summary>
        public static MockPartPose BodyGun(PlayerActionState state, Quaternion aimPitch)
        {
            var scale = StanceScale(state);
            if (state.IsReloading || state.IsDowned)
            {
                return new MockPartPose(ScaleHeight(BodyReloadGunPosition, scale), Quaternion.Euler(BodyReloadGunEuler));
            }

            var position = state.IsAiming ? BodyAimGunPosition : BodyHipGunPosition;
            return new MockPartPose(ScaleHeight(position, scale), aimPitch);
        }

        /// <summary>一人称の銃の姿勢 (反動を除く)。リロード中とダウン中は下げて傾け、ADS なら中央へ寄せ、それ以外は右下。</summary>
        public static MockPartPose ViewModel(PlayerActionState state)
        {
            if (state.IsReloading || state.IsDowned)
            {
                return new MockPartPose(ViewReloadPosition, Quaternion.Euler(ViewReloadEuler));
            }

            return new MockPartPose(state.IsAiming ? ViewAimPosition : ViewHipPosition, Quaternion.identity);
        }

        /// <summary>
        /// pose に反動を足す。kick は 0 (なし) 〜 1 (撃った瞬間)。銃の向きの後ろへ distance 引き、銃口を degrees 跳ね上げる。
        /// </summary>
        public static MockPartPose WithKick(MockPartPose pose, float kick, float distance, float degrees)
        {
            if (!(kick >= 0f && kick <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(kick), kick, "kick must be between 0 and 1");
            }

            if (kick == 0f)
            {
                return pose;
            }

            var back = pose.Rotation * Vector3.back * (distance * kick);
            return new MockPartPose(pose.Position + back, pose.Rotation * Quaternion.Euler(-degrees * kick, 0f, 0f));
        }

        /// <summary>反動を deltaSeconds だけ戻した値。<see cref="KickRecoverSeconds"/> で 1 から 0 へ戻る。</summary>
        public static float RecoverKick(float kick, float deltaSeconds)
        {
            RequireDelta(deltaSeconds);
            return Mathf.Clamp01(kick - (deltaSeconds / KickRecoverSeconds));
        }

        /// <summary>deltaSeconds の間に目標へ寄せる割合 (0〜1)。フレームの長さが変わっても同じ速さで寄るよう指数で決める。</summary>
        public static float BlendFactor(float deltaSeconds)
        {
            RequireDelta(deltaSeconds);
            return 1f - Mathf.Exp(-BlendRate * deltaSeconds);
        }

        /// <summary>水平成分だけを単位ベクトルにして返す。水平成分が無い (か有限でない) なら zero。</summary>
        public static Vector3 Horizontal(Vector3 vector)
        {
            var flat = new Vector3(vector.x, 0f, vector.z);
            var length = flat.magnitude;
            return length > 1e-4f && !float.IsInfinity(length) && !float.IsNaN(length) ? flat / length : Vector3.zero;
        }

        // 上を direction へ傾ける。軸を up × direction に取ると、正の角度で頭が direction 側へ倒れる。
        private static Quaternion LeanToward(Vector3 direction, float degrees)
        {
            return Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.up, direction));
        }

        private static Vector3 ScaleHeight(Vector3 position, float scale)
        {
            return new Vector3(position.x, position.y * scale, position.z);
        }

        private static void RequireDelta(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }
        }

        private static void RequirePositiveFinite(float value, string paramName)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "must be positive and finite");
            }
        }
    }
}
