using System;
using System.Numerics;

namespace Shooter.Core
{
    // スタミナ・よろけ・制圧の 1 人分。オフラインは部品 (PlayerStamina・PlayerStagger・SuppressionReceiver) がそれぞれ持つが、
    // サーバーは体を持たずに入力と出来事だけを受けるので、3 つの判定と移動への効き方をここにまとめ、時間は Tick と Stepped で受ける。
    public sealed class PlayerStatusMeters
    {
        private StaminaMeter _stamina;
        private StaggerJudge _stagger;
        private SuppressionMeter _suppression;

        public PlayerStatusMeters(ActionRules rules, StaminaSpec stamina, StaggerSpec stagger, SuppressionSpec suppression)
        {
            Configure(rules, stamina, stagger, suppression);
        }

        public Stamina StaminaRule { get; private set; }

        public Stagger StaggerRule { get; private set; }

        public Suppression SuppressionRule { get; private set; }

        public StaminaMeter StaminaMeter => _stamina;

        public StaggerJudge StaggerJudge => _stagger;

        public SuppressionMeter SuppressionMeter => _suppression;

        public bool IsStaminaOn => StaminaRule == Stamina.On;

        public bool IsStaggerOn => StaggerRule == Stagger.On;

        public bool IsSuppressionOn => SuppressionRule == Suppression.On;

        // Off ではスタミナの欄を空にし、HUD に使わない量を出させない。
        public StaminaStatus? StaminaStatus => IsStaminaOn ? _stamina.Status : (StaminaStatus?)null;

        public StaggerKind StaggerKind => IsStaggerOn ? _stagger.Current : StaggerKind.None;

        public float SuppressionLevel => IsSuppressionOn ? _suppression.Level : 0f;

        public bool IsSuppressed => IsSuppressionOn && _suppression.IsSuppressed;

        public bool CanSprint => !IsStaminaOn || _stamina.CanSprint;

        // 調整値が同じでもルールを入れ直したら数え直す。前のルールでためたよろけや制圧を次の試合へ持ち越さないため。
        public void Configure(ActionRules rules, StaminaSpec stamina, StaggerSpec stagger, SuppressionSpec suppression)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _stamina = new StaminaMeter(stamina ?? throw new ArgumentNullException(nameof(stamina)));
            _stagger = new StaggerJudge(stagger ?? throw new ArgumentNullException(nameof(stagger)));
            _suppression = new SuppressionMeter(suppression ?? throw new ArgumentNullException(nameof(suppression)));
            StaminaRule = rules.Stamina;
            StaggerRule = rules.Stagger;
            SuppressionRule = rules.Suppression;
        }

        // オフラインの PlayerController と同じく、尽きている間はダッシュを、払えない回避は押していないことにする。
        // よろけている間は PlayerStagger と同じく歩きを弱め、ダッシュ・ジャンプ・回避をさせない。
        public CharacterMotorInput Restrain(CharacterMotorInput input)
        {
            var move = input.Move;
            var sprint = input.Sprint;
            var jump = input.Jump;
            var dodge = input.Dodge;
            if (IsStaminaOn)
            {
                sprint = sprint && _stamina.CanSprint;
                dodge = dodge && _stamina.CanAfford(StaminaUse.Dodge);
            }

            if (StaggerKind != StaggerKind.None)
            {
                move *= _stagger.Spec.MoveMultiplier;
                sprint = false;
                jump = false;
                dodge = false;
            }

            if (move == input.Move && sprint == input.Sprint && jump == input.Jump && dodge == input.Dodge)
            {
                return input;
            }

            return new CharacterMotorInput(
                move,
                input.Yaw,
                sprint,
                jump,
                input.Crouch,
                input.Downed,
                input.Cover,
                input.Aim,
                dodge,
                input.VaultAllowed,
                input.Prone,
                input.ProneAllowed,
                input.LadderAllowed,
                input.CarrySpeedMultiplier,
                input.JumpAllowed,
                input.Lean,
                input.Platform,
                input.OpenParachute,
                input.CloseParachute,
                input.ParachuteAllowed);
        }

        // 動いた後の状態で、走ったか・回避を始めたかを決めてスタミナを減らす。押しているだけで止まっている・しゃがんでいる・
        // 泳いでいる・はしごにいる tick は、オフラインの PlayerController と同じく走っていないとする。
        public void Stepped(CharacterMotorInput input, CharacterMotorState previous, CharacterMotorState current, float deltaSeconds)
        {
            if (!IsStaminaOn)
            {
                return;
            }

            if (!previous.IsDodging && current.IsDodging)
            {
                _stamina.TrySpend(StaminaUse.Dodge);
            }

            _stamina.Tick(deltaSeconds, IsSprinting(input, current));
        }

        public static bool IsSprinting(CharacterMotorInput input, CharacterMotorState state)
        {
            return input.Sprint
                && !input.Downed
                && !state.IsCrouched
                && !state.IsProne
                && !state.IsSwimming
                && !state.IsClimbing
                && !state.IsDodging
                && input.Move.LengthSquared() >= SprintInput.MoveThreshold * SprintInput.MoveThreshold;
        }

        // 怯みが解けた Tick にだけ、解けた種類を返す。
        public StaggerKind Tick(float deltaSeconds)
        {
            _suppression.Tick(deltaSeconds);
            return _stagger.Tick(deltaSeconds);
        }

        // Off なら null を返し、ダメージを数えない。
        public StaggerVerdict? Damaged(HitZone? zone, DamageKind kind, int amount)
        {
            if (!IsStaggerOn)
            {
                return null;
            }

            return _stagger.Hit(zone, kind, amount, 0f);
        }

        public bool AddNearMiss(float distance)
        {
            return IsSuppressionOn && _suppression.AddNearMiss(distance);
        }

        // 弾の線からの距離を測って、半径の内ならニアミスとして数える。
        public bool AddNearMiss(Vector3 origin, Vector3 direction, float length, Vector3 center, out float distance)
        {
            distance = 0f;
            if (!IsSuppressionOn
                || !NearMiss.TryDistance(origin, direction, length, center, out distance, out _)
                || !(distance < _suppression.Spec.NearMissRadius))
            {
                return false;
            }

            return _suppression.AddNearMiss(distance);
        }

        // 出現し直したときと倒れたとき。スタミナは満タンにし、よろけと制圧は解く。
        public void Reset()
        {
            _stamina.Refill();
            _stagger.Reset();
            _suppression.Reset();
        }
    }
}
