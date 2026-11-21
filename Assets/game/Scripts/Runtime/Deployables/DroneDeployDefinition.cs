using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 置くとその場から操縦を始め (DroneControl)、置いたドローンがある間に能力を短く押すと戻す (C4 の起爆と同じ押し方)。
    [CreateAssetMenu(fileName = "DeployDrone", menuName = "Shooter/Abilities/Deploy Drone")]
    public sealed class DroneDeployDefinition : DeployAbilityDefinition
    {
        public const string BodyName = "Body";
        public const string RotorName = "Rotor";

        // 差し替えた見た目にこの名前の子があれば、操縦のカメラをそこに置く。無ければ胴の中心に置く。
        public const string CameraAnchorName = "CameraAnchor";

        public const float AnchorNearClipMargin = 0.05f;

        public const float BodyHeightFraction = 0.6f;

        public const float RotorSizeFraction = 0.35f;

        [SerializeField, Tooltip("名前を引く表 (Localization の items.json) のキー。空なら名前は Id")]
        private string _nameKey = DroneSpec.DefaultNameKey;

        [SerializeField, Min(0f), Tooltip("ドローンを置いているとき、押してからこの秒数より前に離すと戻す。押し続けると次のドローンを狙って置く")]
        private float _recallTapSeconds = DroneSpec.DefaultRecallTapSeconds;

        [SerializeField, Min(0.01f), Tooltip("飛ぶ速さ (m/秒)")]
        private float _speed = DroneSpec.DefaultSpeed;

        [SerializeField, Min(0.01f), Tooltip("置いた所から離れられる距離 (m)。越えると電波が届かず落ちる")]
        private float _controlRange = DroneSpec.DefaultControlRange;

        [SerializeField, Min(0.01f), Tooltip("電池の持つ秒数。降りている間も減り、切れると落ちる")]
        private float _batterySeconds = DroneSpec.DefaultBatterySeconds;

        [SerializeField, Min(0.01f), Tooltip("敵を見つける半径 (m)")]
        private float _spotRadius = DroneSpec.DefaultSpotRadius;

        [SerializeField, Min(0.01f), Tooltip("見回す間隔 (秒)。置いた時点で 1 回見回す")]
        private float _spotIntervalSeconds = DroneSpec.DefaultSpotIntervalSeconds;

        [SerializeField, Min(0.01f), Tooltip("見つけた敵をミニマップと印に出す秒数。見つけ続けている間は見回しのたびに延びる")]
        private float _revealSeconds = DroneSpec.DefaultRevealSeconds;

        // 基底の既定は盾の形と数なので、作った時点でドローンの既定に置き換える (アセットに保存された値があればそちらが勝つ)。
        public DroneDeployDefinition()
        {
            MaxActive = DroneSpec.DefaultMaxActive;
            MaxHealth = DroneSpec.DefaultMaxHealth;
            Size = new Vector3(DroneSpec.DefaultWidth, DroneSpec.DefaultHeight, DroneSpec.DefaultDepth);
        }

        public string NameKey
        {
            get => _nameKey;
            set => _nameKey = value;
        }

        public override string DisplayNameKey => _nameKey != null ? _nameKey.Trim() : string.Empty;

        public float RecallTapSeconds
        {
            get => _recallTapSeconds;
            set => _recallTapSeconds = value;
        }

        public float Speed
        {
            get => _speed;
            set => _speed = value;
        }

        public float ControlRange
        {
            get => _controlRange;
            set => _controlRange = value;
        }

        public float BatterySeconds
        {
            get => _batterySeconds;
            set => _batterySeconds = value;
        }

        public float SpotRadius
        {
            get => _spotRadius;
            set => _spotRadius = value;
        }

        public float SpotIntervalSeconds
        {
            get => _spotIntervalSeconds;
            set => _spotIntervalSeconds = value;
        }

        public float RevealSeconds
        {
            get => _revealSeconds;
            set => _revealSeconds = value;
        }

        public override bool Detonates => true;

        public override float DetonateTapSeconds => _recallTapSeconds;

        public DroneSpec ToDroneSpec()
        {
            if (!(_recallTapSeconds >= 0f) || float.IsInfinity(_recallTapSeconds))
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: recallTapSeconds must not be negative and must be finite");
            }

            try
            {
                return new DroneSpec(_speed, _controlRange, _batterySeconds, _spotRadius, _spotIntervalSeconds, _revealSeconds);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"deploy ability '{Id}' is invalid: {e.Message}", e);
            }
        }

        // 不正な値を、置いたときでなく枠に入れたとき (AbilityHost.Equip) に分かるようにする。
        public override IAbility CreateAbility()
        {
            ToDroneSpec();
            return base.CreateAbility();
        }

        public override int Detonate(IReadOnlyList<Deployable> placed, AbilityContext context)
        {
            return ReconDrone.RecallAll(placed);
        }

        public GameObject CreateLook()
        {
            return CreatePlaceholder(ToDeploySpec());
        }

        public static float CollisionRadiusOf(DeploySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Mathf.Min(spec.Height, Mathf.Min(spec.Width, spec.Depth)) / 2f;
        }

        // 胴にも羽にも当たり判定を持たせるのは、敵の弾で撃ち落とせるようにするため。
        protected override GameObject CreatePlaceholder(DeploySpec spec)
        {
            var root = new GameObject(Id);
            var bodyHeight = spec.Height * BodyHeightFraction;
            var rotorHeight = spec.Height - bodyHeight;
            var rotorSize = Mathf.Min(spec.Width, spec.Depth) * RotorSizeFraction;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = BodyName;
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, bodyHeight / 2f, 0f);
            body.transform.localScale = new Vector3(spec.Width - rotorSize, bodyHeight, spec.Depth - rotorSize);

            // 四隅の羽は置く物の幅と奥行きの内に収め、置けるかの判定 (DeploySurveyor) と同じ大きさにする。
            var x = (spec.Width - rotorSize) / 2f;
            var z = (spec.Depth - rotorSize) / 2f;
            var corners = new[] { new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z), new Vector3(-x, 0f, z), new Vector3(x, 0f, z) };
            for (var i = 0; i < corners.Length; i++)
            {
                // 円柱の当たり判定はカプセルで、平たくすると半球が置く物の高さからはみ出すので、羽は平たい箱にする。
                var rotor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rotor.name = RotorName + i;
                rotor.transform.SetParent(root.transform, false);
                rotor.transform.localPosition = corners[i] + new Vector3(0f, bodyHeight + (rotorHeight / 2f), 0f);
                rotor.transform.localScale = new Vector3(rotorSize, rotorHeight, rotorSize);
            }

            var anchor = new GameObject(CameraAnchorName);
            anchor.transform.SetParent(root.transform, false);
            // 壁との当たりの球 (CollisionRadiusOf) の内に収めるため、前へは胴の端まで出さず、半径から近クリップ分を引いた所までにする。
            var forward = Mathf.Min((spec.Depth - rotorSize) / 2f, Mathf.Max(0f, CollisionRadiusOf(spec) - AnchorNearClipMargin));
            anchor.transform.localPosition = new Vector3(0f, bodyHeight / 2f, forward);
            return root;
        }

        // 飛んで場所が変わるので、盾やタレットと違って NavMesh に穴は開けない。隠れ場所 (CoverPoint) にもしない。
        protected override void Configure(GameObject placed, DeploySpec spec)
        {
            if (placed.GetComponent<ReconDrone>() == null)
            {
                placed.AddComponent<ReconDrone>();
            }
        }

        // 操縦するのは自分で動かすプレイヤー (PlayerController を持つ) だけで、それ以外の持ち主のドローンは置いた所で見回すだけにする。
        protected override void OnPlaced(Deployable deployable, AbilityContext context)
        {
            var drone = deployable.GetComponent<ReconDrone>();
            if (drone == null)
            {
                throw new InvalidOperationException($"deploy ability '{Id}' placed '{deployable.name}' without a ReconDrone");
            }

            drone.Arm(ToDroneSpec(), CollisionRadiusOf(ToDeploySpec()));
            var owner = deployable.Owner;
            if (owner.GetComponent<PlayerController>() == null)
            {
                Debug.Log(DroneLog.PilotRefused(deployable.OwnerId, deployable.KindId, deployable.Serial, nameof(PlayerController)), deployable);
                return;
            }

            DroneControl.GetOrAdd(owner).Begin(drone);
        }
    }
}
