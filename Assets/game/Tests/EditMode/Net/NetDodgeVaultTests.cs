using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// ネット対戦の回避と乗り越え。予測とサーバーが同じ <see cref="NetMotorSimulation"/> (オフラインと同じ <see cref="DodgeMotion"/>・<see cref="VaultProbe"/>)
    /// で転がり・乗り越えて、どの tick でも同じ状態になる (照合で巻き戻されない) こと、転がっているか・乗り越えているかが食い違えば巻き戻すこと、
    /// 巻き戻した tick から再シミュレーションしてもサーバーと同じになること、その最中の射撃を捨てることを確かめる。
    /// Edit Mode でも Physics の Raycast と CharacterController.Move はコライダーに当たるので、置いたら SyncTransforms してから動かす。
    /// </summary>
    public sealed class NetDodgeVaultTests
    {
        // NetSceneBuilder のプレイヤーと同じ寸法。本体の中心から目まで 0.6m、足元は中心から 1m 下
        private const float _height = 2f;
        private const float _radius = 0.5f;
        private const float _crouchHeight = 1.2f;
        private const float _eyeHeight = 0.6f;
        private const float _tolerance = 1e-4f;

        // +X を向く。移動入力の前 (Y) は +X、右 (X) は -Z
        private const float _yawTowardX = Mathf.PI / 2f;

        // 低い壁 (上端 0.8m・厚さ 0.4m) の手前の面は x = 1.8、向こうの面は x = 2.2。立ち位置 x = 1.0 なら手前の面まで 0.8m で届く
        private const float _startX = 1.0f;
        private const float _wallFarX = 2.2f;

        private const int _settleTicks = 20;

        private static readonly Vector2 _forward = new Vector2(0f, 1f);

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
        public void PredictionMatchesServerThroughADodge()
        {
            CreateGround();
            var pair = CreatePair(new Vector3(0f, 1.1f, 0f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());
            inputs.Add(Input(_forward, dodge: true));
            Repeat(inputs, 60, Idle());

            var states = pair.Run(inputs);
            var before = states[_settleTicks - 1];
            var started = states[_settleTicks];
            var last = states[states.Count - 1];

            Assert.That(before.IsGrounded, Is.True, "the player must be standing on the ground before dodging");
            Assert.That(started.IsDodging, Is.True, "pressing dodge on the ground starts rolling in the same tick");
            Assert.That(started.Velocity.x, Is.EqualTo(pair.Server.DodgeDistance / pair.Server.DodgeSeconds).Within(1e-3f));
            Assert.That(last.IsDodging, Is.False, "the roll ends after its duration");
            Assert.That(last.Position.x - before.Position.x, Is.EqualTo(pair.Server.DodgeDistance).Within(0.01f), "rolls the full distance forward");
            Assert.That(last.Position.z, Is.EqualTo(before.Position.z).Within(1e-3f));
            Assert.That(last.IsGrounded, Is.True);

            var rollTicks = 0;
            foreach (var state in states)
            {
                rollTicks += state.IsDodging ? 1 : 0;
            }

            // 終わる tick の状態は転がっていないので、数えるのは秒数より 1 tick 少ない。丸めでもう 1 tick ずれうる
            Assert.That(rollTicks * NetworkPlayerMotor.TickSeconds, Is.EqualTo(pair.Server.DodgeSeconds).Within(2f * NetworkPlayerMotor.TickSeconds));
        }

        [Test]
        public void DodgeWithoutMoveInputRollsBackward()
        {
            CreateGround();
            var pair = CreatePair(new Vector3(0f, 1.1f, 0f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());
            inputs.Add(Input(Vector2.zero, dodge: true));
            Repeat(inputs, 40, Idle());

            var states = pair.Run(inputs);

            var moved = states[states.Count - 1].Position.x - states[_settleTicks - 1].Position.x;
            Assert.That(moved, Is.EqualTo(-pair.Server.DodgeDistance).Within(0.01f));
        }

        [Test]
        public void CooldownBlocksTheNextDodgeUntilItRunsOut()
        {
            CreateGround();
            var pair = CreatePair(new Vector3(-5f, 1.1f, 0f));
            var rollTicks = Mathf.CeilToInt(pair.Server.DodgeSeconds / NetworkPlayerMotor.TickSeconds);
            var cooldownTicks = Mathf.CeilToInt(pair.Server.DodgeCooldownSeconds / NetworkPlayerMotor.TickSeconds);

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());
            inputs.Add(Input(_forward, dodge: true));
            Repeat(inputs, rollTicks + 1, Idle());
            var tooEarly = inputs.Count;
            inputs.Add(Input(_forward, dodge: true));
            Repeat(inputs, cooldownTicks + 1, Idle());
            var ready = inputs.Count;
            inputs.Add(Input(_forward, dodge: true));
            Repeat(inputs, 5, Idle());

            var states = pair.Run(inputs);

            Assert.That(states[tooEarly - 1].IsDodging, Is.False, "the first roll is over");
            Assert.That(states[tooEarly - 1].Dodge.CooldownRemaining, Is.GreaterThan(0f));
            Assert.That(states[tooEarly].IsDodging, Is.False, "the cooldown blocks the next roll");
            Assert.That(states[ready - 1].Dodge.CooldownRemaining, Is.EqualTo(0f));
            Assert.That(states[ready].IsDodging, Is.True, "a roll starts once the cooldown has run out");
        }

        [Test]
        public void DodgeNeedsTheGround()
        {
            CreateGround();
            var pair = CreatePair(new Vector3(0f, 1.1f, 0f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());
            inputs.Add(Input(Vector2.zero, jump: true));
            inputs.Add(Input(_forward, dodge: true));

            var states = pair.Run(inputs);

            Assert.That(states[_settleTicks].IsGrounded, Is.False, "jumped");
            Assert.That(states[_settleTicks + 1].IsDodging, Is.False, "cannot start rolling in the air");
        }

        [Test]
        public void DodgeStandsUpAndStopsSprinting()
        {
            CreateGround();
            var pair = CreatePair(new Vector3(-5f, 1.1f, 0f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Input(Vector2.zero, crouch: true));

            // しゃがみ・ダッシュを押したまま転がっても、最中は立ってダッシュの速さにならない
            inputs.Add(new MotorInput(_forward, _yawTowardX, true, false, true, false, false, false, true, false));
            Repeat(inputs, 5, new MotorInput(_forward, _yawTowardX, true, false, true, false, false, false, false, false));

            var states = pair.Run(inputs);

            Assert.That(states[_settleTicks - 1].IsCrouched, Is.True);
            for (var i = _settleTicks; i < states.Count; i++)
            {
                Assert.That(states[i].IsDodging, Is.True, "tick " + (i + 1));
                Assert.That(states[i].IsCrouched, Is.False, "rolling stands up at tick " + (i + 1));
            }
        }

        [Test]
        public void PredictionMatchesServerThroughAVault()
        {
            CreateGround();
            CreateLowWall();
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());
            inputs.Add(Input(Vector2.zero, jump: true, vaultAllowed: true));
            Repeat(inputs, 60, Input(Vector2.zero, vaultAllowed: true));

            var states = pair.Run(inputs);
            var started = states[_settleTicks];
            var last = states[states.Count - 1];

            Assert.That(pair.Server.LastVaultProbe.Verdict, Is.EqualTo(VaultVerdict.Vault));
            Assert.That(started.IsVaulting, Is.True, "jumping in front of a low wall vaults instead");
            Assert.That(started.Vault.Land.x, Is.GreaterThan(0f), "the path lands on the far side");

            var highest = float.MinValue;
            foreach (var state in states)
            {
                highest = Mathf.Max(highest, state.Position.y);
            }

            Assert.That(highest - started.Position.y, Is.GreaterThan(0.6f), "rises over the wall");
            Assert.That(last.IsVaulting, Is.False, "the vault ends");
            Assert.That(last.Position.x, Is.GreaterThan(_wallFarX + _radius), "lands beyond the wall");
            Assert.That(last.IsGrounded, Is.True);
            Assert.That(last.Velocity.x, Is.EqualTo(0f).Within(1e-3f), "the vault does not carry momentum into the landing");
        }

        [Test]
        public void JumpWithoutTheVaultRuleIsAJump()
        {
            CreateGround();
            CreateLowWall();
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());
            inputs.Add(Input(Vector2.zero, jump: true));

            var states = pair.Run(inputs);
            var jumped = states[states.Count - 1];

            Assert.That(jumped.IsVaulting, Is.False);
            Assert.That(jumped.Velocity.y, Is.GreaterThan(0f), "a plain jump");
        }

        [Test]
        public void TooHighAWallIsAJump()
        {
            CreateGround();
            CreateBox("High Wall", new Vector3(2f, 1.25f, 0f), new Vector3(0.4f, 2.5f, 6f));
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());
            inputs.Add(Input(Vector2.zero, jump: true, vaultAllowed: true));

            var states = pair.Run(inputs);

            Assert.That(pair.Server.LastVaultProbe.Verdict, Is.EqualTo(VaultVerdict.TooHigh));
            Assert.That(states[states.Count - 1].IsVaulting, Is.False);
            Assert.That(states[states.Count - 1].Velocity.y, Is.GreaterThan(0f));
        }

        [Test]
        public void ResimulatingFromAnyDodgeOrVaultTickReproducesTheServer()
        {
            CreateGround();
            CreateLowWall();
            var pair = CreatePair(new Vector3(-3f, 1.1f, 0f));

            // 転がって壁の手前まで寄り、待ち時間が明けてから乗り越える
            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());
            var dodgeAt = inputs.Count;
            inputs.Add(Input(_forward, dodge: true, vaultAllowed: true));
            Repeat(inputs, 30, Input(Vector2.zero, vaultAllowed: true));
            Repeat(inputs, 40, Input(_forward * 0.3f, vaultAllowed: true));
            var vaultAt = inputs.Count;
            inputs.Add(Input(Vector2.zero, jump: true, vaultAllowed: true));
            Repeat(inputs, 50, Input(Vector2.zero, vaultAllowed: true));

            var server = pair.Run(inputs);
            Assert.That(server[dodgeAt].IsDodging, Is.True);
            Assert.That(server[vaultAt].IsVaulting, Is.True, "the dodge and the walk end in front of the wall: x=" + server[vaultAt - 1].Position.x);

            // 転がりの途中・待ち時間の途中・乗り越えの途中の tick から、確定状態を当てて残りを再シミュレーションする
            foreach (var from in new[] { dodgeAt + 5, dodgeAt + 25, vaultAt + 3, vaultAt + 20 })
            {
                var prediction = new PredictionBuffer<MotorInput, MotorState>(
                    pair.Predicted.Step, MotorState.PositionError, NetworkPlayerMotor.ReconcileTolerance);
                prediction.Reset(from + 1, server[from]);

                for (var i = from + 1; i < inputs.Count; i++)
                {
                    var mine = prediction.Predict(i + 1, inputs[i]);
                    AssertSame(mine, server[i], "resimulated from tick " + (from + 1) + " at tick " + (i + 1));
                }
            }
        }

        [Test]
        public void DodgeOrVaultMismatchIsReconciledEvenAtTheSamePosition()
        {
            var still = new MotorState(Vector3.one, Vector3.zero, true, false);
            var rolling = new MotorState(
                Vector3.one, Vector3.zero, true, false, NetCoverState.Out,
                new NetDodgeState(new DodgeSnapshot(true, new System.Numerics.Vector2(1f, 0f), 0.1f, 0f)), NetVaultState.None);
            var cooling = new MotorState(
                Vector3.one, Vector3.zero, true, false, NetCoverState.Out,
                new NetDodgeState(new DodgeSnapshot(false, System.Numerics.Vector2.Zero, 0f, 0.3f)), NetVaultState.None);
            var path = VaultProbe.PlanPath(new System.Numerics.Vector3(1f, 0f, 0f), 0.8f, 0.8f, 0.4f, _radius);
            var vaulting = new MotorState(
                Vector3.one, Vector3.zero, true, false, NetCoverState.Out, NetDodgeState.Ready, new NetVaultState(path, 0.1f));

            Assert.That(MotorState.PositionError(rolling, rolling), Is.EqualTo(0f));
            Assert.That(MotorState.PositionError(still, rolling), Is.EqualTo(MotorState.StanceMismatchError));
            Assert.That(MotorState.PositionError(rolling, still), Is.EqualTo(MotorState.StanceMismatchError));
            Assert.That(MotorState.PositionError(still, vaulting), Is.EqualTo(MotorState.StanceMismatchError));
            Assert.That(MotorState.PositionError(vaulting, vaulting), Is.EqualTo(0f));
            Assert.That(MotorState.PositionError(still, cooling), Is.EqualTo(0f), "a cooldown difference alone does not rewind");
        }

        [Test]
        public void ServerWithoutTheDodgeRuleCorrectsAPredictionThatRolled()
        {
            CreateGround();
            var pair = CreatePair(new Vector3(0f, 1.1f, 0f));
            var prediction = new PredictionBuffer<MotorInput, MotorState>(
                pair.Predicted.Step, MotorState.PositionError, NetworkPlayerMotor.ReconcileTolerance);
            var initial = new MotorState(pair.Start, Vector3.zero, false, false);
            prediction.Reset(0, initial);
            var serverState = initial;

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());
            inputs.Add(Input(_forward, dodge: true));

            ReconcileResult result = default;
            for (var i = 0; i < inputs.Count; i++)
            {
                prediction.Predict(i + 1, inputs[i]);

                // ルールで Off のサーバーは Dodge の押下を捨てる (NetworkPlayerMotor.ApplyAuthoritative)
                serverState = pair.Server.Step(serverState, NetworkPlayerMotor.ApplyMoveActionRules(inputs[i], false, false));
                result = prediction.Reconcile(i + 1, serverState);
            }

            Assert.That(serverState.IsDodging, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Corrected));
            Assert.That(result.Error, Is.EqualTo(MotorState.StanceMismatchError));
            Assert.That(prediction.Current.IsDodging, Is.False);
        }

        [Test]
        public void ServerWithoutTheVaultRuleCorrectsAPredictionThatVaulted()
        {
            CreateGround();
            CreateLowWall();
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0f));
            var prediction = new PredictionBuffer<MotorInput, MotorState>(
                pair.Predicted.Step, MotorState.PositionError, NetworkPlayerMotor.ReconcileTolerance);
            var initial = new MotorState(pair.Start, Vector3.zero, false, false);
            prediction.Reset(0, initial);
            var serverState = initial;

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());

            // オーナーの同期されたルールが On のまま、サーバーのルールは Off
            inputs.Add(Input(Vector2.zero, jump: true, vaultAllowed: true));

            ReconcileResult result = default;
            for (var i = 0; i < inputs.Count; i++)
            {
                prediction.Predict(i + 1, inputs[i]);
                serverState = pair.Server.Step(serverState, NetworkPlayerMotor.ApplyMoveActionRules(inputs[i], false, false));
                result = prediction.Reconcile(i + 1, serverState);
            }

            Assert.That(serverState.IsVaulting, Is.False, "the server jumps instead");
            Assert.That(result.Outcome, Is.EqualTo(ReconcileOutcome.Corrected));
            Assert.That(prediction.Current.IsVaulting, Is.False);
        }

        [Test]
        public void GoingDownStopsTheDodgeAndTheVault()
        {
            CreateGround();
            CreateLowWall();
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0f));
            var downed = new MotorInput(Vector2.zero, _yawTowardX, false, false, false, true, false, false, true, true);

            var vaultInputs = new List<MotorInput>();
            Repeat(vaultInputs, _settleTicks, Idle());
            vaultInputs.Add(Input(Vector2.zero, jump: true, vaultAllowed: true));
            Repeat(vaultInputs, 5, Input(Vector2.zero, vaultAllowed: true));
            vaultInputs.Add(downed);

            var states = pair.Run(vaultInputs);
            Assert.That(states[states.Count - 2].IsVaulting, Is.True);
            Assert.That(states[states.Count - 1].IsVaulting, Is.False, "going down stops the vault");
            Assert.That(states[states.Count - 1].Velocity.x, Is.EqualTo(0f).Within(1e-3f), "falls from where the vault stopped");

            var rolling = new MotorState(
                states[_settleTicks - 1].Position, Vector3.zero, true, false, NetCoverState.Out,
                new NetDodgeState(new DodgeSnapshot(true, new System.Numerics.Vector2(-1f, 0f), 0.05f, 0f)), NetVaultState.None);
            var afterDown = pair.Server.Step(rolling, downed);
            Assert.That(afterDown.IsDodging, Is.False, "going down stops the roll");
            Assert.That(afterDown.Dodge.CooldownRemaining, Is.GreaterThan(0f), "stopping the roll starts the cooldown");
        }

        [Test]
        public void CoverIsNotEnteredWhileRollingAndDodgeIsNotStartedInCover()
        {
            CreateGround();
            CreateLowWall();
            var pair = CreatePair(new Vector3(_startX, 1.1f, 0f));

            var inputs = new List<MotorInput>();
            Repeat(inputs, _settleTicks, Idle());

            // 後ろへ転がっている最中の Cover は捨てる
            inputs.Add(Input(Vector2.zero, dodge: true));
            inputs.Add(new MotorInput(Vector2.zero, _yawTowardX, false, false, false, false, true, false));
            Repeat(inputs, 60, Idle());

            var states = pair.Run(inputs);
            Assert.That(states[_settleTicks + 1].IsDodging, Is.True);
            Assert.That(states[_settleTicks + 1].Cover.IsInCover, Is.False);

            var coverInputs = new List<MotorInput>();
            Repeat(coverInputs, _settleTicks, Idle());
            coverInputs.Add(new MotorInput(Vector2.zero, _yawTowardX, false, false, false, false, true, false));
            Repeat(coverInputs, 20, Idle());
            coverInputs.Add(Input(Vector2.zero, dodge: true));

            // 1 組目は後ろへ転がって x = -2 付近にいるので、同じ立ち位置で 2 組目を置いても重ならない
            var inCover = CreatePair(new Vector3(_startX, 1.1f, 0f)).Run(coverInputs);
            Assert.That(inCover[inCover.Count - 2].Cover.IsInCover, Is.True);
            Assert.That(inCover[inCover.Count - 1].IsDodging, Is.False, "cover blocks the dodge");
        }

        [Test]
        public void InputHelpersKeepOrDropOnlyWhatTheyShould()
        {
            var input = new MotorInput(new Vector2(0.5f, 0.5f), 1f, true, true, true, false, true, true, true, true);

            var noDodge = input.WithoutDodge();
            Assert.That(noDodge.Dodge, Is.False);
            Assert.That(noDodge.Cover, Is.True);
            Assert.That(noDodge.VaultAllowed, Is.True);
            Assert.That(noDodge.Jump, Is.True);

            var noCover = input.WithoutCover();
            Assert.That(noCover.Dodge, Is.True, "dropping cover keeps the dodge");
            Assert.That(noCover.VaultAllowed, Is.True);

            Assert.That(input.WithVaultAllowed(false).VaultAllowed, Is.False);
            Assert.That(input.WithVaultAllowed(false).Dodge, Is.True);

            var restrained = new MotorInput(Vector2.one, 1f, true, true, false, true, true, true, true, true).Restrained();
            Assert.That(restrained.Dodge, Is.False);
            Assert.That(restrained.VaultAllowed, Is.False);
            Assert.That(restrained.Jump, Is.False);

            var ruled = NetworkPlayerMotor.ApplyMoveActionRules(input.WithVaultAllowed(false), false, true);
            Assert.That(ruled.Dodge, Is.False);
            Assert.That(ruled.VaultAllowed, Is.True, "the server decides whether jumping may vault");
            Assert.That(NetworkPlayerMotor.ApplyMoveActionRules(input, true, false).Dodge, Is.True);
            Assert.That(NetworkPlayerMotor.ApplyMoveActionRules(input, true, false).VaultAllowed, Is.False);
        }

        [Test]
        public void ShotsWhileDodgingOrVaultingAreRejected()
        {
            Assert.That(NetPlayer.CheckMoveActionShot(false, false), Is.EqualTo(ShotRejection.None));
            Assert.That(NetPlayer.CheckMoveActionShot(true, false), Is.EqualTo(ShotRejection.Dodging));
            Assert.That(NetPlayer.CheckMoveActionShot(false, true), Is.EqualTo(ShotRejection.Vaulting));
        }

        [Test]
        public void DodgeAndVaultLogsShowTheOwnerAndTick()
        {
            Assert.That(NetworkPlayerMotor.FormatDodgeLog(1, 42, true), Is.EqualTo("[Net] dodge owner=1 tick=42 dodging=true"));
            Assert.That(NetworkPlayerMotor.FormatVaultLog(2, 7, false), Is.EqualTo("[Net] vault owner=2 tick=7 vaulting=false"));
        }

        private static MotorInput Idle()
        {
            return Input(Vector2.zero);
        }

        private static MotorInput Input(
            Vector2 move, bool jump = false, bool crouch = false, bool dodge = false, bool vaultAllowed = false)
        {
            return new MotorInput(move, _yawTowardX, false, jump, crouch, false, false, false, dodge, vaultAllowed);
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
            Assert.That(mine.IsDodging, Is.EqualTo(server.IsDodging), "dodge differs " + context);
            Assert.That(mine.IsVaulting, Is.EqualTo(server.IsVaulting), "vault differs " + context);
            Assert.That(mine.IsCrouched, Is.EqualTo(server.IsCrouched), "stance differs " + context);
            Assert.That(Vector3.Distance(mine.Position, server.Position), Is.LessThan(_tolerance), "position differs " + context);
            Assert.That(mine.Dodge.CooldownRemaining, Is.EqualTo(server.Dodge.CooldownRemaining).Within(_tolerance), "cooldown differs " + context);
            Assert.That(mine.Vault.Elapsed, Is.EqualTo(server.Vault.Elapsed).Within(_tolerance), "vault progress differs " + context);
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

        // z = -1〜1 の低い壁 (NetSceneBuilder の Cover Low と同じ高さと厚さ)
        private void CreateLowWall()
        {
            CreateBox("Low Wall", new Vector3(2f, 0.4f, 0f), new Vector3(0.4f, 0.8f, 2f));
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
