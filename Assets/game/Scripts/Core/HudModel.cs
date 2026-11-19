using System;

namespace Shooter.Core
{
    // どの項目も文字列にせず数や型の付いた値で持つのは、どう書くか (体力・弾数・残り時間の書式を含む) を HUD に任せるため。
    public sealed class HudModel
    {
        private Health _health;
        private Weapon _weapon;
        private Match _match;
        private IHudNoticeSource _notice;

        public event Action<HudField> Changed;

        public int? CurrentHealth { get; private set; }

        public int? MaxHealth { get; private set; }

        public bool IsHealthLow => CurrentHealth.HasValue && HudFormat.IsLowHealth(CurrentHealth.Value, MaxHealth.Value);

        public float? HealthFraction => CurrentHealth.HasValue ? Math.Min(1f, (float)CurrentHealth.Value / MaxHealth.Value) : (float?)null;

        public int? MagazineAmmo { get; private set; }

        public int? ReserveAmmo { get; private set; }

        public bool IsReserveInfinite { get; private set; }

        public bool ShowsReserveAmmo { get; private set; } = true;

        public bool IsReloading { get; private set; }

        public WeaponSpec WeaponSpec { get; private set; }

        public int? RemainingSeconds { get; private set; }

        public bool HasTime => RemainingSeconds.HasValue;

        public HudNotice Notice { get; private set; }

        public Team? FirstScoreTeam { get; private set; }

        public int? FirstScore { get; private set; }

        public Team? SecondScoreTeam => FirstScoreTeam.HasValue ? HudFormat.Opponent(FirstScoreTeam.Value) : (Team?)null;

        public int? SecondScore { get; private set; }

        public bool HasResult { get; private set; }

        public Team? Winner { get; private set; }

        public MatchOutcome? SoloResult { get; private set; }

        public string RestartKey { get; private set; } = string.Empty;

        public float? ReloadProgress { get; private set; }

        public float ReloadWindowStart { get; private set; }

        public float ReloadWindowEnd { get; private set; }

        public float CrosshairSpread { get; private set; }

        public float CrosshairConeGap { get; private set; }

        public bool IsCrosshairVisible { get; private set; } = true;

        public bool IsAimBlocked { get; private set; }

        public System.Numerics.Vector2? AimBlockPoint { get; private set; }

        public float? ChargeFraction { get; private set; }

        public float? HeatFraction { get; private set; }

        public AmmoTypeSpec AmmoType { get; private set; }

        public float? Magnification { get; private set; }

        public float? LockOnProgress { get; private set; }

        public bool IsLockedOn => LockOnProgress == 1f;

        public void BindHealth(Health health)
        {
            if (_health != null)
            {
                _health.Changed -= OnHealthChanged;
            }

            _health = health;
            if (_health != null)
            {
                _health.Changed += OnHealthChanged;
            }

            OnHealthChanged();
        }

        public void BindWeapon(Weapon weapon)
        {
            if (_weapon != null)
            {
                _weapon.Fired -= OnWeaponChanged;
                _weapon.Reloaded -= OnWeaponChanged;
                _weapon.ReserveAmmoAdded -= OnWeaponChanged;
                _weapon.ReserveAmmoChanged -= OnWeaponChanged;
                _weapon.AmmoTypeSwitched -= OnWeaponChanged;
            }

            _weapon = weapon;
            if (_weapon != null)
            {
                _weapon.Fired += OnWeaponChanged;
                _weapon.Reloaded += OnWeaponChanged;
                _weapon.ReserveAmmoAdded += OnWeaponChanged;
                _weapon.ReserveAmmoChanged += OnWeaponChanged;
                _weapon.AmmoTypeSwitched += OnWeaponChanged;
            }

            OnWeaponChanged();
            SetWeaponSpec(_weapon?.Spec);
        }

        public void BindMatch(Match match)
        {
            if (_match != null)
            {
                _match.RemainingSecondsChanged -= SetTime;
                _match.StateChanged -= OnMatchStateChanged;
            }

            _match = match;
            if (_match != null)
            {
                _match.RemainingSecondsChanged += SetTime;
                _match.StateChanged += OnMatchStateChanged;
                if (_match.HasTimeLimit)
                {
                    SetTime(_match.RemainingSeconds);
                }
                else
                {
                    ClearTime();
                }
            }
            else
            {
                ClearTime();
            }

            ApplySoloResult(_match?.Result, RestartKey);
        }

        public void SetRestartKey(string key)
        {
            ApplySoloResult(SoloResult, key ?? string.Empty);
        }

        public void BindNotice(IHudNoticeSource notice)
        {
            if (_notice != null)
            {
                _notice.Changed -= SetNotice;
            }

            _notice = notice;
            if (_notice != null)
            {
                _notice.Changed += SetNotice;
            }

            SetNotice(_notice != null ? _notice.Notice : HudNotice.None);
        }

        public void UnbindAll()
        {
            if (_health != null)
            {
                _health.Changed -= OnHealthChanged;
                _health = null;
            }

            if (_weapon != null)
            {
                _weapon.Fired -= OnWeaponChanged;
                _weapon.Reloaded -= OnWeaponChanged;
                _weapon.ReserveAmmoAdded -= OnWeaponChanged;
                _weapon.ReserveAmmoChanged -= OnWeaponChanged;
                _weapon.AmmoTypeSwitched -= OnWeaponChanged;
                _weapon = null;
            }

            if (_match != null)
            {
                _match.RemainingSecondsChanged -= SetTime;
                _match.StateChanged -= OnMatchStateChanged;
                _match = null;
            }

            if (_notice != null)
            {
                _notice.Changed -= SetNotice;
                _notice = null;
            }
        }

        public void SetHealth(int current, int max)
        {
            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must not be negative");
            }

            if (max < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be at least 1");
            }

            ApplyHealth(current, max);
        }

        public void ClearHealth()
        {
            ApplyHealth(null, null);
        }

        public void SetAmmo(int magazine, int reserve, bool reloading)
        {
            SetAmmo(magazine, reserve, reloading, false);
        }

        public void SetAmmo(int magazine, int reserve, bool reloading, bool infiniteReserve)
        {
            if (magazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazine), magazine, "magazine must not be negative");
            }

            if (reserve < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserve), reserve, "reserve must not be negative");
            }

            ApplyAmmo(magazine, reserve, reloading, infiniteReserve);
        }

        public void ClearAmmo()
        {
            ApplyAmmo(null, null, false, false);
        }

        public void SetReserveAmmoShown(bool shown)
        {
            if (shown == ShowsReserveAmmo)
            {
                return;
            }

            ShowsReserveAmmo = shown;
            Changed?.Invoke(HudField.Ammo);
        }

        public void SetWeaponSpec(WeaponSpec spec)
        {
            // 同じ武器を Weapon ごと作り直して Bind し直しても、名前が変わらなければ欄を描き直させない。
            var same = spec == null ? WeaponSpec == null : WeaponSpec != null && spec.Id == WeaponSpec.Id && string.Equals(spec.NameKey, WeaponSpec.NameKey, StringComparison.Ordinal);
            WeaponSpec = spec;
            if (!same)
            {
                Changed?.Invoke(HudField.WeaponName);
            }
        }

        public void ClearWeaponSpec()
        {
            SetWeaponSpec(null);
        }

        public void SetTime(float remainingSeconds)
        {
            ApplyTime(HudFormat.DisplayedSeconds(remainingSeconds));
        }

        public void ClearTime()
        {
            ApplyTime(null);
        }

        public void SetNotice(HudNotice notice)
        {
            if (notice == Notice)
            {
                return;
            }

            Notice = notice;
            Changed?.Invoke(HudField.Notice);
        }

        public void ClearNotice()
        {
            SetNotice(HudNotice.None);
        }

        public void SetRespawnNotice(RespawnMode mode, int seconds)
        {
            SetNotice(HudNotice.ForRespawn(mode, seconds));
        }

        public void SetScores(Team ownTeam, int alphaScore, int bravoScore)
        {
            var opponent = HudFormat.Opponent(ownTeam);
            if (alphaScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaScore), alphaScore, "score must not be negative");
            }

            if (bravoScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bravoScore), bravoScore, "score must not be negative");
            }

            ApplyScores(ownTeam, ownTeam == Team.Alpha ? alphaScore : bravoScore, opponent == Team.Alpha ? alphaScore : bravoScore);
        }

        public void ClearScores()
        {
            ApplyScores(null, null, null);
        }

        public void SetResult(Team? winner)
        {
            ApplyResult(true, winner);
        }

        public void ClearResult()
        {
            ApplyResult(false, null);
        }

        public void SetReloadProgress(float progress, float windowStart, float windowEnd)
        {
            if (!IsFraction(progress))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "progress must be between 0 and 1");
            }

            if (!IsFraction(windowStart))
            {
                throw new ArgumentOutOfRangeException(nameof(windowStart), windowStart, "windowStart must be between 0 and 1");
            }

            if (!IsFraction(windowEnd))
            {
                throw new ArgumentOutOfRangeException(nameof(windowEnd), windowEnd, "windowEnd must be between 0 and 1");
            }

            if (windowStart > windowEnd)
            {
                throw new ArgumentOutOfRangeException(nameof(windowStart), windowStart, "windowStart must not exceed windowEnd");
            }

            ApplyReloadProgress(progress, windowStart, windowEnd);
        }

        public void ClearReloadProgress()
        {
            ApplyReloadProgress(null, 0f, 0f);
        }

        public void SetCrosshair(float spread, bool visible)
        {
            SetCrosshair(spread, 0f, visible);
        }

        // 広がりも縁も時間で変わりイベントが無いので、Bind せず HUD が毎フレーム呼ぶ。
        public void SetCrosshair(float spread, float coneGap, bool visible)
        {
            if (!IsFraction(spread))
            {
                throw new ArgumentOutOfRangeException(nameof(spread), spread, "spread must be between 0 and 1");
            }

            if (float.IsNaN(coneGap) || float.IsInfinity(coneGap) || coneGap < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(coneGap), coneGap, "coneGap must be finite and not negative");
            }

            if (spread == CrosshairSpread && coneGap == CrosshairConeGap && visible == IsCrosshairVisible)
            {
                return;
            }

            CrosshairSpread = spread;
            CrosshairConeGap = coneGap;
            IsCrosshairVisible = visible;
            Changed?.Invoke(HudField.Crosshair);
        }

        public void ResetCrosshair()
        {
            SetCrosshair(0f, true);
        }

        // 狙う向きは毎フレーム変わりイベントが無いので、Bind せず HUD が毎フレーム呼ぶ。
        public void SetAimBlock(System.Numerics.Vector2? viewport)
        {
            if (viewport.HasValue && !IsFinite(viewport.Value.X, viewport.Value.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(viewport), viewport, "viewport must be finite");
            }

            ApplyAimBlock(true, viewport);
        }

        public void ClearAimBlock()
        {
            ApplyAimBlock(false, null);
        }

        private void ApplyAimBlock(bool blocked, System.Numerics.Vector2? viewport)
        {
            if (blocked == IsAimBlocked && viewport == AimBlockPoint)
            {
                return;
            }

            IsAimBlocked = blocked;
            AimBlockPoint = viewport;
            Changed?.Invoke(HudField.AimBlock);
        }

        private static bool IsFinite(float x, float y)
        {
            return !float.IsNaN(x) && !float.IsInfinity(x) && !float.IsNaN(y) && !float.IsInfinity(y);
        }

        // 進みは時間で変わりイベントが無いので、HUD が毎フレーム呼ぶ。
        public void RefreshReloadProgress()
        {
            if (_weapon == null || !_weapon.HasActiveReload || !_weapon.IsReloading)
            {
                ClearReloadProgress();
                return;
            }

            if (_weapon.HasJudgedActiveReload)
            {
                // 失敗で時間が延びた後は、幅の位置が進みの尺と合わなくなるため出さない。
                SetReloadProgress(_weapon.ReloadProgress, 0f, 0f);
                return;
            }

            var spec = _weapon.Spec;
            SetReloadProgress(
                _weapon.ReloadProgress,
                spec.ActiveReloadWindowStart,
                spec.ActiveReloadWindowStart + spec.ActiveReloadWindowLength);
        }

        public void SetCharge(float fraction)
        {
            ApplyGauge(HudField.Charge, Fraction(fraction, nameof(fraction)));
        }

        public void ClearCharge()
        {
            ApplyGauge(HudField.Charge, null);
        }

        public void SetHeat(float fraction)
        {
            ApplyGauge(HudField.Heat, Fraction(fraction, nameof(fraction)));
        }

        public void ClearHeat()
        {
            ApplyGauge(HudField.Heat, null);
        }

        public void SetAmmoType(AmmoTypeSpec ammoType)
        {
            var same = ammoType == null ? AmmoType == null : AmmoType != null && ammoType.Id == AmmoType.Id && string.Equals(ammoType.NameKey, AmmoType.NameKey, StringComparison.Ordinal);
            AmmoType = ammoType;
            if (!same)
            {
                Changed?.Invoke(HudField.AmmoType);
            }
        }

        public void ClearAmmoType()
        {
            SetAmmoType(null);
        }

        public void SetMagnification(float magnification)
        {
            if (!(magnification > 0f) || float.IsInfinity(magnification))
            {
                throw new ArgumentOutOfRangeException(nameof(magnification), magnification, "magnification must be positive and finite");
            }

            ApplyGauge(HudField.Magnification, magnification);
        }

        public void ClearMagnification()
        {
            ApplyGauge(HudField.Magnification, null);
        }

        public void SetLockOn(float progress)
        {
            ApplyGauge(HudField.LockOn, Fraction(progress, nameof(progress)));
        }

        public void ClearLockOn()
        {
            ApplyGauge(HudField.LockOn, null);
        }

        // 溜め・熱・弾薬の種類は時間や入力で変わり Weapon にイベントが無いので、リロードの進みと同じく HUD が毎フレーム呼ぶ。
        public void RefreshWeaponGauges()
        {
            if (_weapon == null)
            {
                ClearCharge();
                ClearHeat();
                ClearAmmoType();
                return;
            }

            // 溜めの無い撃ち方も ChargeFraction は 0 を返すので、Spec に溜めがあるかで出すかを決める。
            if (_weapon.Spec.Trigger.Charge != null)
            {
                SetCharge(_weapon.Trigger.ChargeFraction);
            }
            else if (_weapon.MeleeCharge != null && _weapon.MeleeCharge.Spec.CanCharge)
            {
                SetCharge(_weapon.MeleeCharge.Fraction);
            }
            else
            {
                ClearCharge();
            }

            var heat = _weapon.Ammo.HeatFraction;
            if (heat.HasValue)
            {
                SetHeat(heat.Value);
            }
            else
            {
                ClearHeat();
            }

            SetAmmoType(_weapon.AmmoTypes.Current);
        }

        private void OnHealthChanged()
        {
            if (_health != null)
            {
                SetHealth(_health.Current, _health.Max);
            }
            else
            {
                ClearHealth();
            }
        }

        private void OnWeaponChanged()
        {
            // 熱で撃つ武器は弾倉を減らさないので、弾数を出すと撃っても変わらない数が並ぶ。撃てるかは熱のゲージで見せる。近接武器は弾を使わない。
            if (_weapon != null && !_weapon.Ammo.HeatFraction.HasValue && !_weapon.Spec.IsMelee)
            {
                SetAmmo(_weapon.AmmoInMagazine, _weapon.ReserveAmmo, _weapon.IsReloading, _weapon.HasInfiniteReserve);
            }
            else
            {
                ClearAmmo();
            }

            // 次のフレームを待たずに、リロードが終わった・武器が替わった時点で進みを消すため。
            RefreshReloadProgress();
            RefreshWeaponGauges();
        }

        private void ApplyHealth(int? current, int? max)
        {
            if (current == CurrentHealth && max == MaxHealth)
            {
                return;
            }

            CurrentHealth = current;
            MaxHealth = max;
            Changed?.Invoke(HudField.Health);
        }

        private void ApplyAmmo(int? magazine, int? reserve, bool reloading, bool infiniteReserve)
        {
            if (magazine == MagazineAmmo && reserve == ReserveAmmo && reloading == IsReloading && infiniteReserve == IsReserveInfinite)
            {
                return;
            }

            MagazineAmmo = magazine;
            ReserveAmmo = reserve;
            IsReloading = reloading;
            IsReserveInfinite = infiniteReserve;
            Changed?.Invoke(HudField.Ammo);
        }

        private void ApplyTime(int? seconds)
        {
            if (seconds == RemainingSeconds)
            {
                return;
            }

            RemainingSeconds = seconds;
            Changed?.Invoke(HudField.Time);
        }

        private void OnMatchStateChanged(GameModeState state)
        {
            ApplySoloResult(_match?.Result, RestartKey);
        }

        private void ApplySoloResult(MatchOutcome? result, string restartKey)
        {
            if (result == SoloResult && restartKey == RestartKey)
            {
                return;
            }

            SoloResult = result;
            RestartKey = restartKey;
            Changed?.Invoke(HudField.Result);
        }

        private void ApplyScores(Team? firstTeam, int? first, int? second)
        {
            if (firstTeam == FirstScoreTeam && first == FirstScore && second == SecondScore)
            {
                return;
            }

            FirstScoreTeam = firstTeam;
            FirstScore = first;
            SecondScore = second;
            Changed?.Invoke(HudField.Score);
        }

        private void ApplyResult(bool hasResult, Team? winner)
        {
            if (hasResult == HasResult && winner == Winner)
            {
                return;
            }

            HasResult = hasResult;
            Winner = winner;
            Changed?.Invoke(HudField.Result);
        }

        private void ApplyReloadProgress(float? progress, float windowStart, float windowEnd)
        {
            if (progress == ReloadProgress && windowStart == ReloadWindowStart && windowEnd == ReloadWindowEnd)
            {
                return;
            }

            ReloadProgress = progress;
            ReloadWindowStart = windowStart;
            ReloadWindowEnd = windowEnd;
            Changed?.Invoke(HudField.ReloadProgress);
        }

        private void ApplyGauge(HudField field, float? value)
        {
            switch (field)
            {
                case HudField.Charge when value != ChargeFraction:
                    ChargeFraction = value;
                    break;
                case HudField.Heat when value != HeatFraction:
                    HeatFraction = value;
                    break;
                case HudField.Magnification when value != Magnification:
                    Magnification = value;
                    break;
                case HudField.LockOn when value != LockOnProgress:
                    LockOnProgress = value;
                    break;
                default:
                    return;
            }

            Changed?.Invoke(field);
        }

        private static float Fraction(float value, string paramName)
        {
            if (!IsFraction(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be between 0 and 1");
            }

            return value;
        }

        private static bool IsFraction(float value)
        {
            return value >= 0f && value <= 1f;
        }
    }
}
