using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 予測・再シミュレーション・サーバーで同じ処理を通すので、同じ状態と入力と世界からは同じ結果になる。
    /// 姿勢の判断は状態と入力だけから行い、体に残った前の tick の姿勢には頼らない (巻き戻しで状態が飛んでも同じ答えにするため)。
    /// カバー中に寄せるときに向きを回さないのは、向きは持ち主の視点操作が決めて入力に乗せるもので、シミュレーションからは書けないため。
    /// </summary>
    public sealed class CharacterMotorSimulation
    {
        public const string ReasonDowned = "downed";

        public const float PeekSlideSeconds = 0.15f;

        private readonly PlayerMotor _motor;
        private readonly CharacterCoverSensor _coverSensor;
        private readonly CharacterVaultSensor _vaultSensor;

        // 状態から毎 tick 戻して使う作業用。tick をまたいで中身を持ち越さない
        private readonly CoverState _cover = new CoverState();
        private readonly DodgeMotion _dodge;

        private readonly ProneInput _proneInput = new ProneInput();
        private readonly ProneDive _dive;

        // 状態の頂点から作った軌跡の使い回し。同じ頂点の間は毎 tick 作り直さない
        private VaultPath _vaultPath;

        private IReadOnlyList<MapLadder> _ladders = Array.Empty<MapLadder>();

        public CharacterMotorSimulation(CharacterBody body, PlayerMotor motor, float tickSeconds, float killPlaneY)
            : this(body, motor, tickSeconds, killPlaneY, ProneSpec.Default)
        {
        }

        /// <summary>prone は伏せの速さと飛び込み。伏せの高さは body の <see cref="StanceGeometry.Prone"/> を使う。</summary>
        public CharacterMotorSimulation(CharacterBody body, PlayerMotor motor, float tickSeconds, float killPlaneY, ProneSpec prone)
        {
            Body = body ?? throw new ArgumentNullException(nameof(body));
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            if (!(tickSeconds > 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be positive and finite");
            }

            if (float.IsNaN(killPlaneY))
            {
                throw new ArgumentOutOfRangeException(nameof(killPlaneY), killPlaneY, "killPlaneY must not be NaN");
            }

            TickSeconds = tickSeconds;
            KillPlaneY = killPlaneY;
            _coverSensor = new CharacterCoverSensor(body);
            _vaultSensor = new CharacterVaultSensor(body);
            _dodge = new DodgeMotion(motor.Spec);
            ProneSpec = prone ?? throw new ArgumentNullException(nameof(prone));
            _dive = new ProneDive(prone);
        }

        public CharacterBody Body { get; }

        public MotorSpec Spec => _motor.Spec;

        public ProneSpec ProneSpec { get; }

        /// <summary>つかまれるはしご。状態ははしごをこの並びの番号で指すので、予測とサーバーで同じ並びを渡す。</summary>
        public IReadOnlyList<MapLadder> Ladders
        {
            get => _ladders;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                for (var i = 0; i < value.Count; i++)
                {
                    if (value[i] == null)
                    {
                        throw new ArgumentException("ladders must not contain null", nameof(value));
                    }
                }

                _ladders = value;
            }
        }

        public float TickSeconds { get; }

        public float KillPlaneY { get; }

        public Vector3 SpawnPosition { get; set; }

        public CharacterCoverSensor CoverSensor => _coverSensor;

        public CharacterVaultSensor VaultSensor => _vaultSensor;

        public VaultProbeResult LastVaultProbe { get; private set; }

        public static Vector3 YawForward(float yaw)
        {
            return new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        }

        public CharacterMotorState Step(CharacterMotorState state, CharacterMotorInput input)
        {
            input = input.Restrained();
            Place(state);

            _dodge.Restore(state.Dodge);
            RestoreProne(state.Prone);
            var vault = state.Vault;
            var startVelocity = state.Velocity;
            var ladder = AdvanceRegrab(state.Ladder);
            if (input.Downed)
            {
                _dodge.Cancel();
                if (vault.IsVaulting)
                {
                    // オフラインの FinishVault と同じく、やめた所から速度 0 で落ちる
                    vault = CharacterVaultState.None;
                    startVelocity = Vector3.Zero;
                }
            }

            if (!input.ProneAllowed)
            {
                ReleaseProne();
            }

            if (ladder.IsClimbing && (!input.LadderAllowed || ladder.Index >= _ladders.Count))
            {
                ladder = Leave(ladder);
            }

            var wasProne = _proneInput.IsActive || state.IsProne;

            // 回避・乗り越え・伏せ・はしごの最中はカバーに入らない (寄せる動きと転がり・乗り越えの軌跡がぶつかるため。CoverController と同じ)
            var busy = _dodge.IsRolling || vault.IsVaulting || wasProne || _dive.IsDiving || ladder.IsClimbing;
            var cover = StepCover(state.Cover, busy && !state.Cover.IsInCover ? input.WithoutCover() : input);

            if (input.Dodge && state.IsGrounded && !vault.IsVaulting && !cover.IsInCover && _dodge.IsReady
                && !ladder.IsClimbing && !wasProne && !_dive.IsDiving)
            {
                _dodge.TryStart(DodgeMotion.ResolveDirection(input.Move, input.Yaw));
            }

            UpdateProne(state, input, ladder.IsClimbing || cover.IsInCover, startVelocity);

            var move = input.Move;
            var sprint = input.Sprint;
            var jump = input.Jump && !wasProne;
            var wantsCrouch = input.Crouch;
            if (_dodge.IsRolling || vault.IsVaulting)
            {
                sprint = false;
                jump = false;
                wantsCrouch = false;
            }

            // PlayerController の FilterInputForProne と同じく、伏せている (伏せたい) 間はダッシュもジャンプもしない
            if (state.IsProne || _proneInput.IsActive)
            {
                sprint = false;
                jump = false;
            }

            var placed = ladder.IsClimbing;
            // カバーに張り付いている間は、寄せる動きとはしごの置き直しがぶつかるのでつかまらない
            if (!ladder.IsClimbing && input.LadderAllowed && ladder.RegrabRemaining <= 0f && !cover.IsInCover)
            {
                ladder = Grab(startVelocity);
                if (ladder.IsClimbing)
                {
                    // LadderMovement が選ばれたときの PlayerController.BeginMovementOverride と同じく、回避・乗り越え・伏せをやめる
                    _dodge.Cancel();
                    vault = CharacterVaultState.None;
                    ReleaseProne();
                }
            }

            if (ladder.IsClimbing)
            {
                // 回避は選ばれた時点でやめているので、ここで進むのは次の回避までの待ち時間だけ
                _dodge.Step(TickSeconds);
                var climbed = Climb(state.Stance, ladder, move.Y, jump, placed, state.IsGrounded, out var climbVelocity);
                return Finish(climbVelocity, Body.Stance, cover, vault, input, climbed);
            }

            if (cover.IsInCover)
            {
                sprint = false;
                jump = false;
                if (cover.Height == CoverHeight.Low)
                {
                    wantsCrouch = !cover.IsPeeking;
                }
            }

            var stance = ResolveStance(state.Stance, wantsCrouch, _proneInput.IsActive);
            var crouched = stance == PlayerStance.Crouching;
            if (cover.IsInCover)
            {
                // 寄せている途中と覗いている間は動かさない
                move = cover.Phase == CoverPhase.In ? AlongWall(cover.Normal, input.Move.X, input.Yaw, crouched) : Vector2.Zero;
            }

            if (jump && input.VaultAllowed && state.IsGrounded && stance == PlayerStance.Standing && !cover.IsInCover)
            {
                vault = TryStartVault(input.Yaw);
                if (vault.IsVaulting)
                {
                    jump = false;
                }
            }

            Vector3 velocity;
            if (vault.IsVaulting)
            {
                velocity = FollowVault(ref vault);
            }
            else if (_dodge.IsRolling)
            {
                velocity = Roll(startVelocity, state.IsGrounded);
            }
            else if (_dive.IsDiving)
            {
                _dodge.Step(TickSeconds);
                velocity = Dive(startVelocity, state.IsGrounded);
            }
            else
            {
                // 転がっていない間も待ち時間を減らす
                _dodge.Step(TickSeconds);
                _motor.SetVelocity(startVelocity);
                var displacement = stance == PlayerStance.Prone
                    ? _motor.StepAtSpeed(move, input.Yaw, ProneSpec.Speed, false, state.IsGrounded, TickSeconds)
                    : _motor.Step(move, input.Yaw, sprint, crouched, jump, state.IsGrounded, TickSeconds);
                Body.Move(displacement);
                velocity = _motor.Velocity;
            }

            return Finish(velocity, stance, cover, vault, input, ladder);
        }

        // 落下の下限を確かめ、作業用に戻した回避・伏せの中身を状態へ写す
        private CharacterMotorState Finish(
            Vector3 velocity, PlayerStance stance, CharacterCoverState cover, CharacterVaultState vault, CharacterMotorInput input,
            CharacterLadderState ladder)
        {
            var position = Body.Position;
            var grounded = Body.IsGrounded;
            if (position.Y < KillPlaneY)
            {
                position = SpawnPosition;
                velocity = Vector3.Zero;
                grounded = false;
                stance = PlayerStance.Standing;
                cover = CharacterCoverState.Out;
                _dodge.Reset();
                vault = CharacterVaultState.None;
                ReleaseProne();
                ladder = CharacterLadderState.None;
                Place(new CharacterMotorState(position, velocity, grounded, false));
            }

            var prone = new CharacterProneState(
                _proneInput.IsActive, _proneInput.WasSprinting, input.Crouch, _dive.IsDiving, _dive.Direction, _dive.Elapsed);
            return new CharacterMotorState(position, velocity, grounded, stance, cover, _dodge.Capture(), vault, prone, ladder);
        }

        public void Place(CharacterMotorState state)
        {
            if (Body.Stance != state.Stance)
            {
                Body.ApplyStance(state.Stance);
            }

            Body.Place(state.Position, state.IsGrounded);
        }

        // 前方を調べ、乗り越えられれば進み 0 の状態を返す。乗り越えられなければ None (Jump はふつうのジャンプになる)
        private CharacterVaultState TryStartVault(float yaw)
        {
            LastVaultProbe = _vaultSensor.Probe(Body.Position, yaw, _motor.Spec.VaultMaxHeight);
            if (!LastVaultProbe.CanVault)
            {
                return CharacterVaultState.None;
            }

            _vaultPath = LastVaultProbe.Path;
            return CharacterVaultState.Following(_vaultPath, 0f);
        }

        // 軌跡を 1 tick たどる。たどり終えたら乗り越えをやめ、着地で跳ね返らないよう速度を 0 にする (PlayerController と同じ)
        private Vector3 FollowVault(ref CharacterVaultState vault)
        {
            if (_vaultPath == null || !vault.HasSamePath(_vaultPath))
            {
                _vaultPath = vault.ToPath();
            }

            var before = vault.Elapsed;
            var after = Math.Min(_vaultPath.Duration, before + TickSeconds);
            var displacement = _vaultPath.Displacement(Math.Min(before, after), after);
            Body.Move(displacement);

            if (after >= _vaultPath.Duration)
            {
                vault = CharacterVaultState.None;
                return Vector3.Zero;
            }

            vault = vault.WithElapsed(after);

            // 射撃のばらつきなど速度を読む側のために、たどっている速度を入れておく
            return displacement / TickSeconds;
        }

        // 縦は PlayerMotor と同じ扱い (接地中は地面へ吸着、空中は重力の台形則) にし、段差から転がり落ちても浮かない。
        // 転がり終えて空中にいたら、その勢いのまま次の tick から PlayerMotor の空中制御に引き継がせる (PlayerController と同じ)
        private Vector3 Roll(Vector3 startVelocity, bool grounded)
        {
            var horizontalVelocity = _dodge.Velocity;
            var horizontal = _dodge.Step(TickSeconds);
            var verticalStart = grounded ? PlayerMotor.GroundedVerticalSpeed : startVelocity.Y;
            var verticalEnd = grounded ? verticalStart : verticalStart - (_motor.Spec.Gravity * TickSeconds);
            Body.Move(new Vector3(horizontal.X, (verticalStart + verticalEnd) * 0.5f * TickSeconds, horizontal.Y));
            return new Vector3(horizontalVelocity.X, verticalEnd, horizontalVelocity.Y);
        }

        // 体は Place(state) で前の tick の位置にある
        private CharacterCoverState StepCover(CharacterCoverState previous, CharacterMotorInput input)
        {
            if (!previous.IsInCover && !input.Cover)
            {
                return previous;
            }

            _cover.Restore(previous.Snapshot);
            if (input.Downed)
            {
                _cover.Release(ReasonDowned);
                return new CharacterCoverState(_cover.Capture(), 0f, CoverEdge.None, 0f);
            }

            var position = Body.Position;
            var appliedPeek = previous.AppliedPeek;
            CoverProbeResult probe;
            float topY;
            if (previous.IsInCover)
            {
                // 端から横に出ている間は、出る前の位置で壁を調べる (出た先にはもう壁が無い)
                var into = -previous.Normal;
                var right = CoverProbe.RightAlong(previous.Normal);
                probe = _coverSensor.Probe(position - (right * appliedPeek), into, into, out topY);
            }
            else
            {
                var forward = YawForward(input.Yaw);
                probe = _coverSensor.Probe(position, forward, forward, out topY);
            }

            var progressBefore = _cover.EnterProgress;
            var transition = _cover.Step(input.Cover, input.Aim, input.Move, probe, TickSeconds);
            if (!_cover.IsInCover)
            {
                // 横に出たまま出ても戻さない (撃ち合いの最中に勝手に動かさない。CoverController と同じ)
                return new CharacterCoverState(_cover.Capture(), 0f, CoverEdge.None, 0f);
            }

            var normal = _cover.Normal;
            if (transition == CoverTransition.Enter)
            {
                appliedPeek = 0f;
            }
            else if (_cover.Phase == CoverPhase.Entering || (progressBefore < 1f && _cover.EnterProgress >= 1f))
            {
                SlideIn(normal, probe.Distance, progressBefore, _cover.EnterProgress);
            }

            appliedPeek = SlidePeek(normal, appliedPeek, _cover.PeekOffset);
            return new CharacterCoverState(_cover.Capture(), topY, probe.Edges, appliedPeek);
        }

        // 寄せ終わる時刻までの残りを等分して進むので、最後の tick でちょうど目標の距離になる
        private void SlideIn(Vector3 normal, float distance, float progressBefore, float progressAfter)
        {
            if (progressBefore >= 1f)
            {
                return;
            }

            var fraction = Clamp01((progressAfter - progressBefore) / (1f - progressBefore));
            if (fraction <= 0f)
            {
                return;
            }

            var gap = distance - CoverProbe.HoldDistance(Body.Radius);
            Body.Move(-normal * (gap * fraction));
        }

        // 横に障害物があれば出られた分だけを覚え、戻るときに出た分だけ戻す
        private float SlidePeek(Vector3 normal, float applied, float target)
        {
            if (Approximately(target, applied))
            {
                return applied;
            }

            var speed = CoverProbe.PeekSideDistance / PeekSlideSeconds;
            var step = MoveTowards(applied, target, speed * TickSeconds) - applied;
            var right = CoverProbe.RightAlong(normal);
            var before = Body.Position;
            Body.Move(right * step);
            var moved = Body.Position - before;
            return applied + (moved.X * right.X) + (moved.Y * right.Y) + (moved.Z * right.Z);
        }

        // 壁沿いの横の入力を、yaw で回す前の移動入力 (X 右・Y 前) にする。立ったまま動く (高い壁) ときもしゃがみの速さにする
        private Vector2 AlongWall(Vector3 normal, float side, float yaw, bool crouched)
        {
            var along = CoverProbe.RightAlong(normal) * Math.Max(-1f, Math.Min(1f, side));
            var forward = YawForward(yaw);
            var right = new Vector3(forward.Z, 0f, -forward.X);
            var local = new Vector2(
                (along.X * right.X) + (along.Y * right.Y) + (along.Z * right.Z),
                (along.X * forward.X) + (along.Y * forward.Y) + (along.Z * forward.Z));
            return local * SpeedScale(crouched);
        }

        private float SpeedScale(bool crouched)
        {
            var spec = _motor.Spec;
            if (crouched)
            {
                return 1f;
            }

            return spec.WalkSpeed > 0f ? Math.Min(1f, spec.CrouchSpeed / spec.WalkSpeed) : 0f;
        }

        // PlayerController.UpdateStance と同じく、低い姿勢へはすぐ移り、高い姿勢へは望む高さから順に、頭上が空いている最も高いものへ戻る
        private PlayerStance ResolveStance(PlayerStance current, bool wantsCrouch, bool wantsProne)
        {
            var target = wantsProne ? PlayerStance.Prone : wantsCrouch ? PlayerStance.Crouching : PlayerStance.Standing;
            if (target == current)
            {
                return current;
            }

            if (target > current)
            {
                Body.ApplyStance(target);
                return target;
            }

            for (var stance = target; stance < current; stance++)
            {
                if (Body.HasHeadroomFor(stance))
                {
                    Body.ApplyStance(stance);
                    return stance;
                }
            }

            return current;
        }

        private void RestoreProne(CharacterProneState prone)
        {
            _proneInput.Restore(prone.IsActive, prone.WasSprinting);
            _dive.Restore(prone.IsDiving, prone.DiveDirection, prone.DiveElapsed);
        }

        // ProneInput.Release と同じく頭上は確かめない。実際に起き上がるのは頭上が空いているとき (ResolveStance)
        private void ReleaseProne()
        {
            _proneInput.Restore(false, _proneInput.WasSprinting);
            _dive.Cancel();
        }

        // PlayerController.UpdateProne と同じ。しゃがみの押下は、しゃがみの入力が立ち上がった tick とみなす
        private void UpdateProne(CharacterMotorState state, CharacterMotorInput input, bool held, Vector3 startVelocity)
        {
            var canStart = input.ProneAllowed && state.IsGrounded && !_dodge.IsRolling && !state.Vault.IsVaulting && !_dive.IsDiving && !held;
            var crouchPressed = input.Crouch && !state.Prone.WasCrouching;
            var wasActive = _proneInput.IsActive;
            _proneInput.Update(canStart, input.Prone, crouchPressed, input.Jump, input.Sprint, out var dive);
            if (wasActive && !_proneInput.IsActive)
            {
                _dive.Cancel();
            }

            if (dive)
            {
                StartDive(startVelocity, input.Move, input.Yaw);
            }
        }

        // 勢いがあればその向き、無ければ移動入力、それも無ければ向いている前へ飛び込む (PlayerController.StartDive と同じ)
        private void StartDive(Vector3 velocity, Vector2 move, float yaw)
        {
            if (_dive.Distance <= 0f)
            {
                return;
            }

            var direction = new Vector2(velocity.X, velocity.Z);
            if (direction.LengthSquared() < 1e-4f)
            {
                var input = move.LengthSquared() > 1e-4f ? move : new Vector2(0f, 1f);
                direction = DodgeMotion.ResolveDirection(input, yaw);
            }

            _dive.TryStart(direction);
        }

        // 縦は回避と同じく PlayerMotor の扱い (接地中は地面へ吸着、空中は重力の台形則) にする
        private Vector3 Dive(Vector3 startVelocity, bool grounded)
        {
            var horizontalVelocity = _dive.Velocity;
            var horizontal = _dive.Step(TickSeconds);
            var verticalStart = grounded ? PlayerMotor.GroundedVerticalSpeed : startVelocity.Y;
            var verticalEnd = grounded ? verticalStart : verticalStart - (_motor.Spec.Gravity * TickSeconds);
            Body.Move(new Vector3(horizontal.X, (verticalStart + verticalEnd) * 0.5f * TickSeconds, horizontal.Y));
            return new Vector3(horizontalVelocity.X, verticalEnd, horizontalVelocity.Y);
        }

        private CharacterLadderState AdvanceRegrab(CharacterLadderState ladder)
        {
            if (ladder.IsClimbing || ladder.RegrabRemaining <= 0f)
            {
                return ladder;
            }

            return CharacterLadderState.Off(Math.Max(0f, ladder.RegrabRemaining - TickSeconds));
        }

        private CharacterLadderState Leave(CharacterLadderState ladder)
        {
            var regrab = ladder.IsClimbing && ladder.Index < _ladders.Count ? _ladders[ladder.Index].Spec.RegrabSeconds : 0f;
            return CharacterLadderState.Off(regrab);
        }

        // LadderMovement.FindLadder と同じく、並びの先頭からつかまれる最初のはしごにつかまる
        private CharacterLadderState Grab(Vector3 velocity)
        {
            var feet = Feet();
            for (var i = 0; i < _ladders.Count; i++)
            {
                var ladder = _ladders[i];
                var entry = ladder.Span.Probe(feet, velocity, Body.Radius, ladder.Spec.GrabDistance);
                if (entry == LadderEntry.None)
                {
                    continue;
                }

                var climb = new LadderClimb(ladder.Span, ladder.Spec, entry, ladder.Span.HeightOf(feet));
                return CharacterLadderState.Climbing(i, climb.Entry, climb.Height, climb.IsInputLatched, climb.LatchedDownSign);
            }

            return CharacterLadderState.Off(0f);
        }

        // LadderMovement.Step と同じ動き。つかまった tick は足元をはしごの前へ置き直し、以後ははしごの前の足元へ向けて動かす。
        // 向きは持ち主の視点操作が決めるので、オフラインのようにはしごへ向き直らせない
        private CharacterLadderState Climb(
            PlayerStance stance, CharacterLadderState state, float forward, bool jump, bool placed, bool grounded, out Vector3 velocity)
        {
            var ladder = _ladders[state.Index];
            var span = ladder.Span;
            var radius = Body.Radius;
            var climb = LadderClimb.Resume(span, ladder.Spec, state.Entry, state.Height, state.IsInputLatched, state.LatchedDownSign);
            // 置く前の接地は、つかまる前に立っていた床 (上の足場など) のものなので読まない
            var outcome = climb.Step(forward, jump, placed && grounded, TickSeconds);
            ResolveStance(stance, false, false);
            switch (outcome)
            {
                case LadderOutcome.ExitTop:
                    Land(span.TopExit(radius));
                    velocity = Vector3.Zero;
                    return Leave(state);
                case LadderOutcome.ExitBottom:
                    Land(climb.Feet(radius));
                    velocity = Vector3.Zero;
                    return Leave(state);
                case LadderOutcome.JumpOff:
                    velocity = span.JumpOffVelocity(ladder.Spec.JumpOffSpeed);
                    Body.Move(velocity * TickSeconds);
                    return Leave(state);
                default:
                    var target = climb.Feet(radius);
                    velocity = LadderSpan.Up * climb.VerticalSpeed;
                    if (placed)
                    {
                        Body.Move(target - Feet());
                    }
                    else
                    {
                        Body.Place(RootFor(target), false);
                        Body.Move(Vector3.Zero);
                    }

                    return CharacterLadderState.Climbing(state.Index, climb.Entry, climb.Height, climb.IsInputLatched, climb.LatchedDownSign);
            }
        }

        // 降りた足元を床から少し浮かせて置き、下へ動かして床に着ける (LadderMovement と同じ)
        private void Land(Vector3 feet)
        {
            Body.Place(RootFor(feet + (LadderSpan.Up * LadderClimb.LandingLift)), false);
            Body.Move(-LadderSpan.Up * LadderClimb.LandingDrop);
        }

        private Vector3 Feet()
        {
            var position = Body.Position;
            return new Vector3(position.X, Body.FeetY(position), position.Z);
        }

        // 足元と本体の差は姿勢を変えても保たれる (低い姿勢は足元を残して縮む)
        private Vector3 RootFor(Vector3 feet)
        {
            return new Vector3(feet.X, feet.Y - Body.Current.BottomY, feet.Z);
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : (value > 1f ? 1f : value);
        }

        // Unity の Mathf.Approximately と同じ判定にして、Unity 版と同じ tick で覗きの寄せを止める
        private static bool Approximately(float a, float b)
        {
            return Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8f);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            return Math.Abs(target - current) <= maxDelta ? target : current + (Math.Sign(target - current) * maxDelta);
        }
    }
}
