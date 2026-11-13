using System;
using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// ネット対戦のしゃがみ。予測とサーバーが同じ <see cref="NetMotorSimulation"/> で同じ結果になること、
    /// しゃがみが食い違えば巻き戻すこと、巻き戻した時点のしゃがみのカプセルで部位を決めることを確かめる。
    /// Edit Mode でも Physics の重なり判定と Raycast はコライダーに当たるので、置いたら SyncTransforms してから動かす。
    /// </summary>
    public sealed class NetStanceTests
    {
        // NetSceneBuilder のプレイヤーと同じ寸法。本体の中心から目まで 0.6m
        private const float _height = 2f;
        private const float _radius = 0.5f;
        private const float _crouchHeight = 1.2f;
        private const float _eyeHeight = 0.6f;
        private const float _tolerance = 1e-4f;
        private const float _shotRange = 100f;

        // +X へ進む向き
        private const float _yawTowardX = Mathf.PI / 2f;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void StanceLogShowsOwnerTickAndCrouch()
        {
            Assert.That(NetworkPlayerMotor.FormatStanceLog(1, 42, true), Is.EqualTo("[Net] stance owner=1 tick=42 crouched=true"));
            Assert.That(NetworkPlayerMotor.FormatStanceLog(0, 7, false), Is.EqualTo("[Net] stance owner=0 tick=7 crouched=false"));
        }

        [Test]
        public void FallDamageLogShowsOwnerTickSpeedDamageAndOutcome()
        {
            Assert.That(
                NetworkPlayerMotor.FormatFallDamageLog(1, 42, 17.9f, 45, Fps.Core.DamageOutcome.Damaged),
                Is.EqualTo("[Net] fall owner=1 tick=42 speed=17.90 damage=45 outcome=Damaged"));
        }

        [Test]
        public void ReconcileLogShowsBothStances()
        {
            var result = new ReconcileResult(ReconcileOutcome.Corrected, 12, MotorState.StanceMismatchError, 3);

            Assert.That(
                NetworkPlayerMotor.FormatReconcileLog(result, true, false),
                Is.EqualTo("[Net] reconcile tick=12 error=Infinity resimulated=3 crouched=true predictedCrouched=false"));
        }

        [Test]
        public void ApplyShrinksCapsuleLowersEyeAndSquashesBodyKeepingFeet()
        {
            var player = CreatePlayer("Player", new Vector3(0f, 1f, 0f), withEyeAndBody: true);
            var stance = NetStance.FromHierarchy(player, _crouchHeight);
            var eye = player.transform.Find(NetPlayer.ViewName);
            var body = player.transform.Find(NetStance.BodyName);

            Assert.That(stance.Eye, Is.SameAs(eye));
            Assert.That(stance.Body, Is.SameAs(body));
            Assert.That(stance.EyePosition.y, Is.EqualTo(1f + _eyeHeight).Within(_tolerance));

            stance.Apply(true);

            // 足元 (y = 0) は変えず、頭のてっぺんと目が 0.8m 下がる
            Assert.That(stance.IsCrouched, Is.True);
            Assert.That(player.height, Is.EqualTo(_crouchHeight).Within(_tolerance));
            Assert.That(player.center.y, Is.EqualTo(-0.4f).Within(_tolerance));
            Assert.That(player.transform.position.y + player.center.y - (player.height / 2f), Is.EqualTo(0f).Within(_tolerance));
            Assert.That(eye.localPosition.y, Is.EqualTo(_eyeHeight - 0.8f).Within(_tolerance));
            Assert.That(stance.EyePosition.y, Is.EqualTo(eye.position.y).Within(_tolerance));
            Assert.That(body.localScale.y, Is.EqualTo(_crouchHeight / _height).Within(_tolerance));
            Assert.That(body.localPosition.y, Is.EqualTo(-0.4f).Within(_tolerance));

            stance.Apply(false);

            Assert.That(player.height, Is.EqualTo(_height).Within(_tolerance));
            Assert.That(player.center.y, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(eye.localPosition.y, Is.EqualTo(_eyeHeight).Within(_tolerance));
            Assert.That(body.localScale.y, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(body.localPosition.y, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void LyingLaysTheBodyOnTheFloorWithoutChangingTheCapsule()
        {
            var player = CreatePlayer("Player", new Vector3(0f, 1f, 0f), withEyeAndBody: true);
            var stance = NetStance.FromHierarchy(player, _crouchHeight);
            var body = player.transform.Find(NetStance.BodyName);
            var standingScale = body.localScale;

            // ダウン中はしゃがみのカプセルで動かすので、しゃがんだ後に寝かせても見た目は立ちの大きさのまま
            stance.Apply(true);
            stance.SetLying(true);

            Assert.That(stance.IsLying, Is.True);
            Assert.That(player.height, Is.EqualTo(_crouchHeight).Within(_tolerance), "the capsule keeps the stance");
            Assert.That(Quaternion.Angle(body.localRotation, Quaternion.Euler(90f, 0f, 0f)), Is.LessThan(0.01f));
            Assert.That(body.localScale, Is.EqualTo(standingScale));

            // 足元 (本体の中心から 1m 下) に半径ぶん浮かせて置く
            Assert.That(body.localPosition.y, Is.EqualTo(-_height / 2f + _radius).Within(_tolerance));

            // 寝かせたまま姿勢が変わっても、見た目は寝かせたまま
            stance.Apply(false);
            Assert.That(body.localPosition.y, Is.EqualTo(-_height / 2f + _radius).Within(_tolerance));

            stance.SetLying(false);

            Assert.That(stance.IsLying, Is.False);
            Assert.That(Quaternion.Angle(body.localRotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(body.localPosition.y, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(body.localScale.y, Is.EqualTo(1f).Within(_tolerance));
        }

        [Test]
        public void EyeWithoutTransformUsesEyeHeightRule()
        {
            var player = CreatePlayer("Player", new Vector3(0f, 1f, 0f), withEyeAndBody: false);
            var stance = NetStance.FromHierarchy(player, _crouchHeight);

            Assert.That(stance.Eye, Is.Null);
            Assert.That(stance.EyePosition.y, Is.EqualTo(1f + NetPlayer.EyeHeight).Within(_tolerance));

            stance.Apply(true);

            Assert.That(stance.EyePosition.y, Is.EqualTo(1f + NetPlayer.EyeHeight - 0.8f).Within(_tolerance));
        }

        [Test]
        public void RejectsInvalidCrouchHeight()
        {
            var player = CreatePlayer("Player", Vector3.zero, withEyeAndBody: false);

            Assert.Throws<ArgumentOutOfRangeException>(() => NetStance.FromHierarchy(player, _height + 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => NetStance.FromHierarchy(player, _radius));
            Assert.Throws<ArgumentNullException>(() => NetStance.FromHierarchy(null, _crouchHeight));
        }

        [Test]
        public void HeadroomIgnoresOtherPlayersButNotWalls()
        {
            var player = CreatePlayer("Player", new Vector3(0f, 1f, 0f), withEyeAndBody: false);
            var stance = NetStance.FromHierarchy(player, _crouchHeight);
            stance.Apply(true);

            // 他のプレイヤーは予測するクライアントでは無効なので、サーバーでも数えない
            CreatePlayer("Other", new Vector3(0f, 2.2f, 0f), withEyeAndBody: false);
            Physics.SyncTransforms();
            Assert.That(stance.HasHeadroomToStand(), Is.True);

            CreateCeiling(new Vector3(0f, 1.8f, 0f), new Vector3(3f, 0.4f, 3f));
            Assert.That(stance.HasHeadroomToStand(), Is.False);
        }

        [Test]
        public void PredictionMatchesServerWithCrouchInputs()
        {
            CreateGround();

            // しゃがんだ頭 (y = 1.2) と立った頭 (y = 2) の間に、x = 2〜4 の天井を置く
            CreateCeiling(new Vector3(3f, 1.8f, 0f), new Vector3(2f, 0.4f, 4f));

            // 予測するクライアントとサーバーを別の本体で動かす。NetworkPlayerMotor と同じくプレイヤー同士は衝突させない
            var start = new Vector3(0f, 1.1f, 0f);
            var predicted = CreatePlayer("Predicted", start, withEyeAndBody: true);
            var server = CreatePlayer("Server", start, withEyeAndBody: true);
            Physics.IgnoreCollision(predicted, server, true);
            Physics.SyncTransforms();

            var predictedSimulation = CreateSimulation(predicted);
            var serverSimulation = CreateSimulation(server);
            var prediction = new PredictionBuffer<MotorInput, MotorState>(
                predictedSimulation.Step, MotorState.PositionError, NetworkPlayerMotor.ReconcileTolerance);
            var initial = new MotorState(start, Vector3.zero, false, false);
            prediction.Reset(0, initial);
            var serverState = initial;

            var heldByCeiling = false;
            var crouchedTicks = 0;
            var inputs = Inputs();
            for (var i = 0; i < inputs.Count; i++)
            {
                var tick = i + 1;
                var input = inputs[i];
                var mine = prediction.Predict(tick, input);
                serverState = serverSimulation.Step(serverState, input);

                Assert.That(mine.IsCrouched, Is.EqualTo(serverState.IsCrouched), "stance differs at tick " + tick);
                Assert.That(Vector3.Distance(mine.Position, serverState.Position), Is.LessThan(_tolerance), "position differs at tick " + tick);
                Assert.That(prediction.Reconcile(tick, serverState).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);

                // 両方の本体の高さも状態どおり
                Assert.That(predicted.height, Is.EqualTo(mine.IsCrouched ? _crouchHeight : _height).Within(_tolerance));
                Assert.That(server.height, Is.EqualTo(serverState.IsCrouched ? _crouchHeight : _height).Within(_tolerance));

                heldByCeiling |= !input.Crouch && serverState.IsCrouched;
                crouchedTicks += serverState.IsCrouched ? 1 : 0;
            }

            Assert.That(crouchedTicks, Is.GreaterThan(0), "crouch input must crouch");
            Assert.That(heldByCeiling, Is.True, "releasing crouch under the ceiling must keep both crouched");
            Assert.That(serverState.IsCrouched, Is.False, "after leaving the ceiling both must stand up");
            Assert.That(serverState.Position.x, Is.GreaterThan(5f));
        }

        [Test]
        public void StanceMismatchIsReconciledEvenAtTheSamePosition()
        {
            var standing = new MotorState(new Vector3(1f, 1f, 1f), Vector3.zero, true, false);
            var crouched = new MotorState(new Vector3(1f, 1f, 1f), Vector3.zero, true, true);

            Assert.That(MotorState.PositionError(standing, standing), Is.EqualTo(0f));
            Assert.That(MotorState.PositionError(standing, crouched), Is.EqualTo(MotorState.StanceMismatchError));

            var player = CreatePlayer("Player", new Vector3(1f, 1f, 1f), withEyeAndBody: false);
            var simulation = CreateSimulation(player);
            var prediction = new PredictionBuffer<MotorInput, MotorState>(
                simulation.Step, MotorState.PositionError, NetworkPlayerMotor.ReconcileTolerance);
            prediction.Reset(0, standing);
            var atTick1 = prediction.Predict(1, new MotorInput(Vector2.zero, 0f, false, false, false));
            prediction.Predict(2, new MotorInput(Vector2.zero, 0f, false, false, true));

            // 予測では tick 1 に立っていたが、サーバーは同じ位置でしゃがんでいた
            var authoritative = new MotorState(atTick1.Position, atTick1.Velocity, atTick1.IsGrounded, true);
            var result = prediction.Reconcile(1, authoritative);

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Corrected));
            Assert.That(result.Error, Is.EqualTo(MotorState.StanceMismatchError));
            Assert.That(result.ResimulatedTicks, Is.EqualTo(1));
            Assert.That(prediction.Current.IsCrouched, Is.True);

            simulation.Place(prediction.Current);
            Assert.That(player.height, Is.EqualTo(_crouchHeight).Within(_tolerance));
        }

        [Test]
        public void CrouchedHeadIsHeadAndStandingHeadHeightMisses()
        {
            // 今は立っているが、tick 10 (撃った側が見ていた時点) ではしゃがんでいた
            var target = CreatePlayer("Target", new Vector3(0f, 0f, 10f), withEyeAndBody: true);
            var stance = NetStance.FromHierarchy(target, _crouchHeight);
            var compensator = new LagCompensator();
            compensator.Record(10, 1, new System.Numerics.Vector3(0f, 0f, 10f), true);
            compensator.Record(20, 1, new System.Numerics.Vector3(0f, 0f, 10f), false);
            Physics.SyncTransforms();

            var targets = new[] { new RewindTarget(1, target.transform, true, stance) };
            var rewinder = new HitboxRewinder();

            // しゃがんだ頭 (足元から 1.1m、カプセルの 92%) は頭
            var crouchedHead = new Vector3(0f, 0.1f, 0f);
            Assert.That(
                rewinder.Raycast(targets, compensator.Rewind(10f), compensator.RewindCrouched(10f), crouchedHead, Vector3.forward, _shotRange, null, out var hit),
                Is.True);
            Assert.That(hit.TargetId, Is.EqualTo(1));
            Assert.That(hit.TargetCrouched, Is.True);
            Assert.That(NetTeamDeathmatch.ZoneOf(stance.Geometry.Shape(hit.TargetCrouched), 1f, hit), Is.EqualTo(HitZone.Head));

            // 立った頭の高さはしゃがんだカプセルの上を素通りする
            var standingHead = new Vector3(0f, 0.9f, 0f);
            Assert.That(
                rewinder.Raycast(targets, compensator.Rewind(10f), compensator.RewindCrouched(10f), standingHead, Vector3.forward, _shotRange, null, out _),
                Is.False);

            // 立っていた時点 (tick 20) なら同じ高さが頭
            Assert.That(
                rewinder.Raycast(targets, compensator.Rewind(20f), compensator.RewindCrouched(20f), standingHead, Vector3.forward, _shotRange, null, out var standingHit),
                Is.True);
            Assert.That(standingHit.TargetCrouched, Is.False);
            Assert.That(NetTeamDeathmatch.ZoneOf(stance.Geometry.Shape(standingHit.TargetCrouched), 1f, standingHit), Is.EqualTo(HitZone.Head));

            // 判定の後は今の姿勢 (立ち) に戻っている
            Assert.That(stance.IsCrouched, Is.False);
            Assert.That(target.height, Is.EqualTo(_height).Within(_tolerance));
            Assert.That(target.center.y, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(Physics.Raycast(standingHead, Vector3.forward, _shotRange), Is.True);
        }

        [Test]
        public void RewindRestoresCrouchWhenTheTargetIsCrouchedNow()
        {
            var target = CreatePlayer("Target", new Vector3(0f, 0f, 10f), withEyeAndBody: false);
            var stance = NetStance.FromHierarchy(target, _crouchHeight);
            stance.Apply(true);
            Physics.SyncTransforms();

            var rewound = new Dictionary<int, System.Numerics.Vector3> { { 1, new System.Numerics.Vector3(0f, 0f, 10f) } };
            var rewoundCrouched = new Dictionary<int, bool> { { 1, false } };
            var targets = new[] { new RewindTarget(1, target.transform, true, stance) };

            // 立っていた時点の頭に当たり、判定の後はしゃがみに戻る
            Assert.That(
                new HitboxRewinder().Raycast(targets, rewound, rewoundCrouched, new Vector3(0f, 0.9f, 0f), Vector3.forward, _shotRange, null, out var hit),
                Is.True);
            Assert.That(hit.TargetCrouched, Is.False);
            Assert.That(stance.IsCrouched, Is.True);
            Assert.That(target.height, Is.EqualTo(_crouchHeight).Within(_tolerance));
        }

        [Test]
        public void WithoutStanceHistoryTheCurrentStanceIsUsed()
        {
            var target = CreatePlayer("Target", new Vector3(0f, 0f, 10f), withEyeAndBody: false);
            var stance = NetStance.FromHierarchy(target, _crouchHeight);
            stance.Apply(true);
            Physics.SyncTransforms();

            var rewound = new Dictionary<int, System.Numerics.Vector3> { { 1, new System.Numerics.Vector3(0f, 0f, 10f) } };
            var targets = new[] { new RewindTarget(1, target.transform, true, stance) };

            Assert.That(
                new HitboxRewinder().Raycast(targets, rewound, null, new Vector3(0f, 0.1f, 0f), Vector3.forward, _shotRange, null, out var hit),
                Is.True);
            Assert.That(hit.TargetCrouched, Is.True);
            Assert.That(NetTeamDeathmatch.ZoneOf(stance.Geometry.Shape(hit.TargetCrouched), 1f, hit), Is.EqualTo(HitZone.Head));
        }

        private static List<MotorInput> Inputs()
        {
            var inputs = new List<MotorInput>();
            var forward = new Vector2(0f, 1f);

            // 着地を待ち、しゃがんで天井の下まで進み、天井の下でしゃがみを離し、そのまま天井の外へ歩いて出る
            Repeat(inputs, 20, new MotorInput(Vector2.zero, _yawTowardX, false, false, false));
            Repeat(inputs, 70, new MotorInput(forward, _yawTowardX, false, false, true));
            Repeat(inputs, 10, new MotorInput(forward, _yawTowardX, true, false, false));
            Repeat(inputs, 120, new MotorInput(forward, _yawTowardX, false, false, false));
            return inputs;
        }

        private static void Repeat(List<MotorInput> inputs, int count, MotorInput input)
        {
            for (var i = 0; i < count; i++)
            {
                inputs.Add(input);
            }
        }

        private static NetMotorSimulation CreateSimulation(CharacterController controller)
        {
            // NetworkPlayerMotor の既定値と同じ
            var motor = new PlayerMotor(new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            var stance = NetStance.FromHierarchy(controller, _crouchHeight);
            return new NetMotorSimulation(
                controller, motor, stance, NetworkPlayerMotor.TickSeconds, NetworkPlayerMotor.KillPlaneY);
        }

        private CharacterController CreatePlayer(string name, Vector3 position, bool withEyeAndBody)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _created.Add(go);
            var controller = go.AddComponent<CharacterController>();
            controller.height = _height;
            controller.radius = _radius;
            controller.center = Vector3.zero;

            if (withEyeAndBody)
            {
                var eye = new GameObject(NetPlayer.ViewName).transform;
                eye.SetParent(go.transform, false);
                eye.localPosition = new Vector3(0f, _eyeHeight, 0f);

                // NetSceneBuilder と同じく、見た目のプリミティブのコライダーは外す
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = NetStance.BodyName;
                Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
                body.transform.SetParent(go.transform, false);
            }

            Physics.SyncTransforms();
            return controller;
        }

        private void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(5f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 10f);
            _created.Add(ground);
            Physics.SyncTransforms();
        }

        private void CreateCeiling(Vector3 position, Vector3 scale)
        {
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.position = position;
            ceiling.transform.localScale = scale;
            _created.Add(ceiling);
            Physics.SyncTransforms();
        }
    }
}
