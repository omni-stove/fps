using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// ネット対戦のカバーとブラインドファイア。予測とサーバーが同じ <see cref="NetMotorSimulation"/> でカバーに入り・壁沿いに動き・覗き・出て、
    /// どの tick でも同じ状態になる (照合で巻き戻されない) こと、カバーの段階が食い違えば巻き戻すこと、
    /// 巻き戻した tick から再シミュレーションしてもサーバーと同じになること、ブラインドファイアの始点をカバーの状態から決めることを確かめる。
    /// Edit Mode でも Physics の Raycast と CharacterController.Move はコライダーに当たるので、置いたら SyncTransforms してから動かす。
    /// </summary>
    public sealed class NetCoverTests
    {
        // NetSceneBuilder のプレイヤーと同じ寸法。本体の中心から目まで 0.6m、足元は中心から 1m 下
        private const float _height = 2f;
        private const float _radius = 0.5f;
        private const float _crouchHeight = 1.2f;
        private const float _eyeHeight = 0.6f;
        private const float _tolerance = 1e-4f;

        // +X (壁) を向く。壁に向いた右は -Z、左は +Z
        private const float _yawTowardX = Mathf.PI / 2f;

        // 壁の手前の面は x = 1.8。立ち位置 x = 1.0 なら壁まで 0.8m で入れる
        private const float _wallFaceX = 1.8f;
        private const float _startX = 1.0f;

        private static readonly Vector2 _right = new Vector2(1f, 0f);

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
        public void PredictionMatchesServerThroughALowWallCover()
        {
            CreateGround();
            CreateWall(new Vector3(2f, 0.4f, 0f), new Vector3(0.4f, 0.8f, 6f));
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, 20, Idle());
            inputs.Add(Input(Vector2.zero, cover: true));
            Repeat(inputs, 20, Idle());
            Repeat(inputs, 30, Input(_right));
            Repeat(inputs, 20, Input(Vector2.zero, aim: true));
            Repeat(inputs, 10, Idle());
            inputs.Add(Input(Vector2.zero, cover: true));
            Repeat(inputs, 10, Idle());

            var states = pair.Run(inputs);

            var enteredLow = false;
            var crouchedHidden = false;
            var stoodPeeking = false;
            var held = false;
            foreach (var state in states)
            {
                var cover = state.Cover;
                enteredLow |= cover.IsInCover && cover.Height == CoverHeight.Low;
                crouchedHidden |= cover.Phase == CoverPhase.In && state.IsCrouched;
                stoodPeeking |= cover.IsPeeking && !state.IsCrouched;
                held |= cover.Phase == CoverPhase.In && state.Position.x > _startX + 0.1f && state.Position.x < _wallFaceX - _radius + 0.01f;
                if (cover.IsInCover)
                {
                    Assert.That(cover.Normal.x, Is.EqualTo(-1f).Within(1e-3f), "the wall faces the player");
                    Assert.That(cover.TopY, Is.EqualTo(0.8f).Within(0.01f), "the low wall top is measured from above");
                }
            }

            Assert.That(enteredLow, Is.True, "pressing cover in front of the low wall must enter it");
            Assert.That(crouchedHidden, Is.True, "hiding behind a low wall crouches");
            Assert.That(stoodPeeking, Is.True, "peeking over a low wall stands up");
            Assert.That(held, Is.True, "entering slides the body towards the wall");

            // 壁沿いの右 (-Z) へしゃがみの速さで動き、壁から離れない
            var afterMove = states[20 + 1 + 20 + 30 - 1];
            Assert.That(afterMove.Cover.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(afterMove.Position.z, Is.LessThan(-0.5f));
            Assert.That(afterMove.Position.x, Is.EqualTo(states[20 + 1 + 20 - 1].Position.x).Within(0.02f));

            var last = states[states.Count - 1];
            Assert.That(last.Cover.Phase, Is.EqualTo(CoverPhase.Out), "pressing cover again leaves it");
            Assert.That(last.IsCrouched, Is.False, "leaving a low wall stands up again");
        }

        [Test]
        public void PredictionMatchesServerWhilePeekingFromAHighWallEdge()
        {
            CreateGround();

            // z = -1〜1 の高い壁。z = 0.85 に立つと左 (+Z) の端
            CreateWall(new Vector3(2f, 1.25f, 0f), new Vector3(0.4f, 2.5f, 2f));
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0.85f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, 20, Idle());
            inputs.Add(Input(Vector2.zero, cover: true));
            Repeat(inputs, 20, Idle());
            Repeat(inputs, 30, Input(Vector2.zero, aim: true));
            var peekedAt = inputs.Count - 1;
            Repeat(inputs, 30, Idle());

            var states = pair.Run(inputs);
            var hidden = states[20 + 1 + 20 - 1];
            var peeked = states[peekedAt];
            var back = states[states.Count - 1];

            Assert.That(hidden.Cover.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(hidden.Cover.Height, Is.EqualTo(CoverHeight.High));
            Assert.That(hidden.Cover.Edges & CoverEdge.Left, Is.EqualTo(CoverEdge.Left));
            Assert.That(hidden.IsCrouched, Is.False, "a high wall keeps the crouch input (none here)");

            Assert.That(peeked.Cover.IsPeeking, Is.True);
            Assert.That(peeked.Cover.AppliedPeek, Is.EqualTo(-CoverProbe.PeekSideDistance).Within(0.01f), "peeks out of the left edge");
            Assert.That(peeked.Position.z - hidden.Position.z, Is.EqualTo(CoverProbe.PeekSideDistance).Within(0.01f));

            Assert.That(back.Cover.Phase, Is.EqualTo(CoverPhase.In), "releasing aim goes back into cover");
            Assert.That(back.Cover.AppliedPeek, Is.EqualTo(0f).Within(0.01f));
            Assert.That(back.Position.z, Is.EqualTo(hidden.Position.z).Within(0.01f));
        }

        [Test]
        public void ResimulatingFromAnyCoverTickReproducesTheServer()
        {
            CreateGround();
            CreateWall(new Vector3(2f, 1.25f, 0f), new Vector3(0.4f, 2.5f, 2f));
            var start = new Vector3(_startX, 1.1f, 0.85f);
            var pair = CreatePair(start);

            var inputs = new List<MotorInput>();
            Repeat(inputs, 20, Idle());
            inputs.Add(Input(Vector2.zero, cover: true));
            Repeat(inputs, 5, Idle());
            Repeat(inputs, 10, Input(Vector2.zero, aim: true));
            Repeat(inputs, 10, Input(_right));
            Repeat(inputs, 5, Idle());

            var server = pair.Run(inputs);

            // 入りかけ・覗いている途中・壁沿いの移動中の tick から、確定状態を当てて残りを再シミュレーションする
            foreach (var from in new[] { 22, 30, 40 })
            {
                var prediction = new PredictionBuffer<MotorInput, MotorState>(
                    pair.Predicted.Step, MotorState.PositionError, NetworkPlayerMotor.ReconcileTolerance);
                prediction.Reset(from + 1, server[from]);
                Assert.That(server[from].Cover.IsInCover, Is.True, "tick " + (from + 1) + " must be in cover");

                for (var i = from + 1; i < inputs.Count; i++)
                {
                    var mine = prediction.Predict(i + 1, inputs[i]);
                    AssertSame(mine, server[i], "resimulated from tick " + (from + 1) + " at tick " + (i + 1));
                }
            }
        }

        [Test]
        public void CoverMismatchIsReconciledEvenAtTheSamePosition()
        {
            var outside = new MotorState(Vector3.one, Vector3.zero, true, false);
            var inside = new MotorState(Vector3.one, Vector3.zero, true, false, InCover(CoverPhase.In, CoverHeight.High));
            var lowInside = new MotorState(Vector3.one, Vector3.zero, true, false, InCover(CoverPhase.In, CoverHeight.Low));
            var peeking = new MotorState(Vector3.one, Vector3.zero, true, false, InCover(CoverPhase.Peeking, CoverHeight.High));

            Assert.That(MotorState.PositionError(inside, inside), Is.EqualTo(0f));
            Assert.That(MotorState.PositionError(outside, inside), Is.EqualTo(MotorState.StanceMismatchError));
            Assert.That(MotorState.PositionError(inside, peeking), Is.EqualTo(MotorState.StanceMismatchError));
            Assert.That(MotorState.PositionError(inside, lowInside), Is.EqualTo(MotorState.StanceMismatchError));
        }

        [Test]
        public void ServerWithoutTheCoverRuleCorrectsAPredictionThatEnteredCover()
        {
            CreateGround();
            CreateWall(new Vector3(2f, 0.4f, 0f), new Vector3(0.4f, 0.8f, 6f));
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0f));
            var prediction = new PredictionBuffer<MotorInput, MotorState>(
                pair.Predicted.Step, MotorState.PositionError, NetworkPlayerMotor.ReconcileTolerance);
            var initial = new MotorState(pair.Start, Vector3.zero, false, false);
            prediction.Reset(0, initial);
            var serverState = initial;

            var inputs = new List<MotorInput>();
            Repeat(inputs, 20, Idle());
            inputs.Add(Input(Vector2.zero, cover: true));
            Repeat(inputs, 3, Idle());

            ReconcileResult result = default;
            for (var i = 0; i < inputs.Count; i++)
            {
                prediction.Predict(i + 1, inputs[i]);

                // ルールで Off のサーバーは Cover の押下を捨てる (NetworkPlayerMotor.ApplyAuthoritative)
                serverState = pair.Server.Step(serverState, inputs[i].WithoutCover());
                result = prediction.Reconcile(i + 1, serverState);
                if (inputs[i].Cover)
                {
                    break;
                }
            }

            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Corrected));
            Assert.That(result.Error, Is.EqualTo(MotorState.StanceMismatchError));
            Assert.That(prediction.Current.Cover.IsInCover, Is.False);
        }

        [Test]
        public void GoingDownLeavesCover()
        {
            CreateGround();
            CreateWall(new Vector3(2f, 0.4f, 0f), new Vector3(0.4f, 0.8f, 6f));
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, 20, Idle());
            inputs.Add(Input(Vector2.zero, cover: true));
            Repeat(inputs, 20, Idle());
            inputs.Add(new MotorInput(Vector2.zero, _yawTowardX, false, false, false, true, false, false));

            var states = pair.Run(inputs);

            Assert.That(states[states.Count - 2].Cover.IsInCover, Is.True);
            Assert.That(states[states.Count - 1].Cover.IsInCover, Is.False);
        }

        [Test]
        public void WithoutCoverDropsOnlyTheCoverPress()
        {
            var input = new MotorInput(new Vector2(0.5f, 0.5f), 1f, true, false, true, false, true, true);
            var stripped = input.WithoutCover();

            Assert.That(stripped.Cover, Is.False);
            Assert.That(stripped.Aim, Is.True);
            Assert.That(stripped.Move, Is.EqualTo(input.Move));
            Assert.That(stripped.Crouch, Is.True);
            Assert.That(stripped.Sprint, Is.True);

            var downed = new MotorInput(Vector2.one, 1f, true, true, false, true, true, true).Restrained();
            Assert.That(downed.Cover, Is.False);
            Assert.That(downed.Aim, Is.False);
        }

        [Test]
        public void BlindFireFromALowWallStartsAboveTheWall()
        {
            CreateGround();
            CreateWall(new Vector3(2f, 0.4f, 0f), new Vector3(0.4f, 0.8f, 6f));
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0f));
            var inputs = new List<MotorInput>();
            Repeat(inputs, 20, Idle());
            inputs.Add(Input(Vector2.zero, cover: true));
            Repeat(inputs, 20, Idle());
            var cover = pair.Run(inputs)[inputs.Count - 1].Cover;
            var eye = new Vector3(1.25f, 0.4f, 0f);

            var aim = NetBlindFire.Resolve(true, cover, eye, _radius, out var origin);

            Assert.That(aim, Is.EqualTo(BlindFireAim.Blind));
            Assert.That(origin.y, Is.EqualTo(0.8f + BlindFireShot.LowWallClearance).Within(0.01f));
            Assert.That(origin.x, Is.EqualTo(eye.x - BlindFireShot.LowWallNormalOffset).Within(1e-3f), "steps back from the wall along its normal");
            Assert.That(origin.z, Is.EqualTo(eye.z).Within(1e-3f));

            Assert.That(NetBlindFire.Resolve(false, cover, eye, _radius, out var off), Is.EqualTo(BlindFireAim.Normal), "the rule is off");
            Assert.That(off, Is.EqualTo(eye));
            Assert.That(NetBlindFire.Resolve(true, NetCoverState.Out, eye, _radius, out _), Is.EqualTo(BlindFireAim.Normal), "not in cover");
        }

        [Test]
        public void BlindFireFromAHighWallNeedsAnEdge()
        {
            var eye = new Vector3(1.25f, 1.6f, 0f);
            var middle = InCover(CoverPhase.In, CoverHeight.High, CoverEdge.None);
            var leftEdge = InCover(CoverPhase.In, CoverHeight.High, CoverEdge.Left);
            var peeking = InCover(CoverPhase.Peeking, CoverHeight.High, CoverEdge.Left);

            Assert.That(NetBlindFire.Resolve(true, middle, eye, _radius, out _), Is.EqualTo(BlindFireAim.Blocked));
            Assert.That(NetBlindFire.Resolve(true, peeking, eye, _radius, out _), Is.EqualTo(BlindFireAim.Normal), "peeking aims normally");

            Assert.That(NetBlindFire.Resolve(true, leftEdge, eye, _radius, out var origin), Is.EqualTo(BlindFireAim.Blind));

            // 壁に向いた左は +Z。体の半径 + 余白だけ横に出て、高さは目のまま
            Assert.That(origin.z, Is.EqualTo(_radius + BlindFireShot.HighWallSideMargin).Within(1e-3f));
            Assert.That(origin.y, Is.EqualTo(eye.y).Within(1e-4f));
        }

        [Test]
        public void BlindFireSpreadIsTheSameForTheSameSeedAndStaysInTheCone()
        {
            var spec = new WeaponSpec(
                "rifle", "Test", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 10, 100f, DamageFalloff.None,
                blindFireSpreadDegrees: 8f);
            var aim = Vector3.forward;

            var first = NetBlindFire.Spread(aim, spec, 3, 7);
            var again = NetBlindFire.Spread(aim, spec, 3, 7);
            var other = NetBlindFire.Spread(aim, spec, 3, 8);

            Assert.That(again, Is.EqualTo(first), "the owner and the server must pick the same direction");
            Assert.That(other, Is.Not.EqualTo(first));
            Assert.That(Vector3.Angle(aim, first), Is.LessThanOrEqualTo(8f + 1e-3f));

            // 武器のばらつきとは別の系列なので、同じ種でも同じ向きに重ならない
            var weaponSpread = NetPlayer.SpreadDirection(aim, 8f, 3, 7);
            Assert.That(Vector3.Angle(weaponSpread, first), Is.GreaterThan(1e-3f));
        }

        [Test]
        public void CoverLogShowsPhaseAndHeight()
        {
            Assert.That(
                NetworkPlayerMotor.FormatCoverLog(1, 42, CoverPhase.In, CoverHeight.Low),
                Is.EqualTo("[Net] cover owner=1 tick=42 phase=In height=Low"));
            Assert.That(
                NetworkPlayerMotor.FormatCoverLog(2, 7, CoverPhase.Out, null),
                Is.EqualTo("[Net] cover owner=2 tick=7 phase=Out"));
            Assert.That(
                NetBlindFire.FormatLog(1, 3, 8f, new Vector3(0f, 1.25f, 0.1f)),
                Is.EqualTo("[Net] blind-fire attacker=1 index=3 spread=8 origin=(0.00,1.25,0.10)"));
        }

        private static NetCoverState InCover(CoverPhase phase, CoverHeight height, CoverEdge edges = CoverEdge.None)
        {
            var snapshot = new CoverSnapshot(
                phase, height, new System.Numerics.Vector3(-1f, 0f, 0f), 0f, CoverState.EnterSeconds, 0f, 1f);
            return new NetCoverState(snapshot, height == CoverHeight.Low ? 0.8f : 2.5f, edges, 0f);
        }

        private static MotorInput Idle()
        {
            return Input(Vector2.zero);
        }

        private static MotorInput Input(Vector2 move, bool cover = false, bool aim = false)
        {
            return new MotorInput(move, _yawTowardX, false, false, false, false, cover, aim);
        }

        private static void Repeat(List<MotorInput> inputs, int count, MotorInput input)
        {
            for (var i = 0; i < count; i++)
            {
                inputs.Add(input);
            }
        }

        private static void AssertSame(MotorState mine, MotorState server, string context)
        {
            Assert.That(mine.Cover.Phase, Is.EqualTo(server.Cover.Phase), "cover phase differs " + context);
            Assert.That(mine.IsCrouched, Is.EqualTo(server.IsCrouched), "stance differs " + context);
            Assert.That(Vector3.Distance(mine.Position, server.Position), Is.LessThan(_tolerance), "position differs " + context);
            Assert.That(mine.Cover.AppliedPeek, Is.EqualTo(server.Cover.AppliedPeek).Within(_tolerance), "peek differs " + context);
        }

        // 予測するクライアントとサーバーを別の本体で動かす。NetworkPlayerMotor と同じくプレイヤー同士は衝突させない
        private SimulationPair CreatePair(Vector3 start)
        {
            var predicted = CreatePlayer("Predicted", start);
            var server = CreatePlayer("Server", start);
            Physics.IgnoreCollision(predicted, server, true);
            Physics.SyncTransforms();
            return new SimulationPair(start, CreateSimulation(predicted), CreateSimulation(server));
        }

        private NetMotorSimulation CreateSimulation(CharacterController controller)
        {
            // NetworkPlayerMotor の既定値と同じ
            return NetTestWorld.CreateSimulation(controller, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f), _crouchHeight, _created);
        }

        private CharacterController CreatePlayer(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _created.Add(go);
            var controller = go.AddComponent<CharacterController>();
            controller.height = _height;
            controller.radius = _radius;
            controller.center = Vector3.zero;

            var eye = new GameObject(NetPlayer.ViewName).transform;
            eye.SetParent(go.transform, false);
            eye.localPosition = new Vector3(0f, _eyeHeight, 0f);

            Physics.SyncTransforms();
            return controller;
        }

        private void CreateGround()
        {
            CreateBox("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
        }

        private void CreateWall(Vector3 position, Vector3 scale)
        {
            CreateBox("Wall", position, scale);
        }

        private void CreateBox(string name, Vector3 position, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            _created.Add(box);
            Physics.SyncTransforms();
        }

        private sealed class SimulationPair
        {
            public SimulationPair(Vector3 start, NetMotorSimulation predicted, NetMotorSimulation server)
            {
                Start = start;
                Predicted = predicted;
                Server = server;
            }

            public Vector3 Start { get; }

            public NetMotorSimulation Predicted { get; }

            public NetMotorSimulation Server { get; }

            /// <summary>
            /// 同じ入力を予測 (PredictionBuffer) とサーバーで動かし、tick ごとに同じ状態で照合が一致することを確かめて、サーバーの状態を返す
            /// (戻り値の i 番目が tick i + 1)。
            /// </summary>
            public List<MotorState> Run(IReadOnlyList<MotorInput> inputs)
            {
                var prediction = new PredictionBuffer<MotorInput, MotorState>(
                    Predicted.Step, MotorState.PositionError, NetworkPlayerMotor.ReconcileTolerance);
                var initial = new MotorState(Start, Vector3.zero, false, false);
                prediction.Reset(0, initial);
                var serverState = initial;
                var states = new List<MotorState>();
                for (var i = 0; i < inputs.Count; i++)
                {
                    var tick = i + 1;
                    var mine = prediction.Predict(tick, inputs[i]);
                    serverState = Server.Step(serverState, inputs[i]);
                    AssertSame(mine, serverState, "at tick " + tick);
                    Assert.That(prediction.Reconcile(tick, serverState).Outcome, Is.EqualTo(ReconcileOutcome.Matched), "tick " + tick);
                    states.Add(serverState);
                }

                return states;
            }
        }
    }
}
