using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class GameBootstrap
    {
        private static GameObject CreatePlayer(
            IReadOnlyList<WeaponDefinition> weaponDefinitions,
            IReadOnlyList<AbilityDefinition> abilityDefinitions,
            GameRules rules,
            float switchSeconds,
            PlayerTuning tuning,
            out CharacterController controller,
            out ViewRig rig,
            out Camera camera,
            out FirstPersonLook look,
            out PlayerController mover,
            out WeaponController weapon,
            out AimDownSights ads,
            out Damageable damageable,
            out HealthRegen regen,
            out PlayerActionReader actionReader,
            out GrenadeThrower grenades,
            out PlayerDeathCamera deathCamera,
            out AbilityHost abilities)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                ConfigurePlayer(
                    player, weaponDefinitions, rules, switchSeconds, tuning, out controller, out rig, out camera, out look, out mover, out weapon, out ads);

                // 撃たれる側の受け皿。CharacterController のカプセルをそのまま胴の当たり判定にする。
                damageable = player.AddComponent<Damageable>();
                damageable.Initialize(PlayerName, tuning.MaxHealth);
                damageable.ApplyRules(rules);
                damageable.IsLocalPlayer = true;
                player.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
                CharacterComponents.Attach(player, throwsGrenades: true);

                // CharacterComponents は敵とネットのプレハブにも付けるので、一人称で手元に出すビューモデルは手元のプレイヤーにだけここで付ける。
                player.AddComponent<ViewmodelPresenter>();
                AttachPlayerFeedback(player, rig, camera, damageable, tuning.Traversal?.Swim);
                WireDeathAndRespawn(damageable, mover, ads);
                KnockbackMovement.Attach(mover, damageable);

                // 使ったときに足す体力と同じ GameObject に置き、持ち物の重さを武器の重さと一緒に移動の遅さに数える
                var inventory = player.AddComponent<PlayerInventory>();
                inventory.Bind(weapon);
                mover.CarryLoad = new PlayerCarryLoad(weapon, inventory);

                // 回復しないルール (RegenPerSecond = 0) でも付けておくのは、キャンペーンが難易度のルールに差し替えられるようにするため。
                regen = player.AddComponent<HealthRegen>();
                regen.Bind(damageable, rules);
                grenades = player.GetComponent<GrenadeThrower>();
                WireGrenades(grenades, mover, weapon, ads);

                // Attach が出す rule のログに入れた能力が載るよう、Attach より前に枠へ入れる。
                // ルールが Off でも入れておくのは、キャンペーンや差し替えたルールで On になったときにそのまま使えるようにするため。
                abilities = PlayerActions.GetOrAdd<AbilityHost>(player);
                abilities.Equip(abilityDefinitions);
                AttachVision(player, damageable);
                PlayerActions.Attach(player, rules);
                AttachVehicleRider(player, mover, weapon);
                mover.ApplyRules(rules);

                mover.BindSprintRelease(weapon, damageable);

                actionReader = AttachActionReader(player, mover, weapon, ads, damageable, rig);

                // LockOnAim・AimAssist と並べて ConfigurePlayer で付けないのは、Play 中は付けた Awake で自分の Damageable を探すため。
                StreakController.Apply(player, rules);

                // ルールが None でも付けておくのは、キャンペーンが難易度のルールに差し替えられるようにするため (HealthRegen と同じ)。
                deathCamera = player.AddComponent<PlayerDeathCamera>();
                deathCamera.ApplyRules(rules);
            }
            catch
            {
                DestroySafely(player);
                throw;
            }

            return player;
        }

        // GameSignals.Vision は画面に 1 つの値なので、敵やネットの体にも付ける CharacterComponents でなく、手元のプレイヤーにだけ付ける。
        private static VisionModeController AttachVision(GameObject player, Damageable damageable)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            var vision = player.GetComponent<VisionModeController>();
            if (vision == null)
            {
                vision = player.AddComponent<VisionModeController>();
            }

            vision.BindDamageable(damageable);
            var thermal = player.GetComponent<ThermalHighlighter>();
            if (thermal == null)
            {
                thermal = player.AddComponent<ThermalHighlighter>();
            }

            thermal.Controller = vision;
            return vision;
        }

        private static void ConfigurePlayer(
            GameObject player,
            IReadOnlyList<WeaponDefinition> weaponDefinitions,
            GameRules rules,
            float switchSeconds,
            PlayerTuning tuning,
            out CharacterController controller,
            out ViewRig rig,
            out Camera camera,
            out FirstPersonLook look,
            out PlayerController mover,
            out WeaponController weapon,
            out AimDownSights ads)
        {
            mover = null;
            player.name = PlayerName;
            player.tag = PlayerTag;
            player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.identity);

            // CharacterController 自体がカプセルのコライダーなので、プリミティブ付属のコライダーは二重になる。
            DestroySafely(player.GetComponent<CapsuleCollider>());

            controller = player.AddComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.radius = PlayerRadius;
            controller.center = Vector3.zero;

            var eye = new GameObject(EyeName).transform;
            eye.SetParent(player.transform, false);
            eye.localPosition = EyeOffset;
            eye.localRotation = Quaternion.identity;

            var cameraObject = new GameObject(CameraName);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(eye, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
            camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();

            // 視点を先に決めておくと、Play 中の AddComponent で走る OnEnable が最初からその視点でカメラを置く。
            // Edit Mode では Awake / OnEnable が走らないので、参照と見た目はここで直接整える。
            rig = player.AddComponent<ViewRig>();
            rig.ThirdPersonOffset = tuning.ThirdPersonOffset;
            rig.MinBoomLength = tuning.MinBoomLength;
            rig.Mode = rules.ViewMode;
            if (!rig.IsInitialized)
            {
                rig.Initialize(eye, camera);
            }

            // 一人称なら自分の体が視界に入らないよう影だけ落とし、三人称なら描く。
            rig.ApplyBodyVisibility(true);
            rig.UpdateCameraPosition();

            // カメラと ViewRig を先に置いておくと、Play 中の AddComponent で走る Awake が自力で pitch の軸 (目) を解決できる。
            // Awake が走らない Edit Mode (シーン生成やテスト) では入力の解決を実行時の Awake に任せる。
            look = player.AddComponent<FirstPersonLook>();
            // 専用サーバーでは FirstPersonLook が自分を止めて初期化しないので、未初期化を失敗にしない。
            if (Application.isPlaying && !look.IsInitialized && !NetStartup.IsDedicatedServerBuild)
            {
                throw new InvalidOperationException(
                    "FirstPersonLook could not resolve its camera or Look action; see the error above");
            }

            // FirstPersonLook と CharacterController を先に付けておくと、PlayerController の Awake が自力で参照を解決できる。
            mover = player.AddComponent<PlayerController>();
            if (Application.isPlaying && !mover.IsInitialized)
            {
                throw new InvalidOperationException(
                    "PlayerController could not resolve its references or Move/Sprint/Crouch/Jump actions; see the error above");
            }

            // Awake は付けた時点で既定の引き先から初期化しているので、シーンの調整値はその後に渡して作り直させる。
            if (tuning.MotorSettings != null)
            {
                mover.UseMotorSettings(tuning.MotorSettings);
            }

            if (tuning.Traversal != null)
            {
                mover.SwimTuning = tuning.Traversal.Swim;
                mover.WallRunTuning = tuning.Traversal.WallRun;
            }

            // 射撃は視点の向きで撃つのでカメラに付ける。参照の解決は FirstPersonLook と同じく Play 中の Awake に任せる。
            // Awake は定義から武器を作るので、カメラを一度無効にして付け、定義を渡してから有効にする (そこで Awake が走る)。
            var cameraWasActive = cameraObject.activeSelf;
            cameraObject.SetActive(false);
            try
            {
                weapon = cameraObject.AddComponent<WeaponController>();
                weapon.Definitions = weaponDefinitions;
                weapon.Rules = rules;
                weapon.SwitchSeconds = switchSeconds;
            }
            finally
            {
                cameraObject.SetActive(cameraWasActive);
            }

            if (Application.isPlaying && !weapon.IsInitialized)
            {
                throw new InvalidOperationException(
                    "WeaponController could not resolve its WeaponDefinitions, camera or Attack/Reload actions; see the error above");
            }

            // カメラと FirstPersonLook を先に付けておくと、Play 中の AddComponent で走る Awake が自力で参照を解決できる。
            ads = player.AddComponent<AimDownSights>();
            if (Application.isPlaying && !ads.IsInitialized && !NetStartup.IsDedicatedServerBuild)
            {
                throw new InvalidOperationException(
                    "AimDownSights could not resolve its camera, FirstPersonLook or Aim action; see the error above");
            }

            ads.TransitionSeconds = tuning.AdsTransitionSeconds;

            WireAds(ads, weapon, mover);
            WireSpreadAndRecoil(weapon, ads, mover, look);
            WireDodgeAndVault(mover, weapon, ads);
            WireAimToFire(weapon, ads, mover);
            LockOnAim.Attach(player, weapon, ads);
            AimAssist.Attach(player, look, ads, weapon);
            WindUpSound.Attach(player, weapon);

            // 乗り物の座席などが操作を引き取ったら、移動と一緒に撃つ・持ち替える入力も止まるよう、同じ口を見させる。
            weapon.Handoff = mover.Handoff;
            mover.CarryLoad = weapon;
        }

        // 使うかはルールで決まり、Off なら始まらないので、ここは常に配線しておく。
        private static void WireDodgeAndVault(PlayerController mover, WeaponController weapon, AimDownSights ads)
        {
            if (mover == null)
            {
                return;
            }

            if (weapon != null)
            {
                weapon.FireBlocker = () => mover != null && (mover.IsDodging || mover.IsVaulting || mover.IsMantling || mover.IsSwimming);
            }

            if (ads == null)
            {
                return;
            }

            mover.DodgeStarted += _ => ads.Release();
            mover.VaultStarted += _ => ads.Release();
            mover.MantleStarted += _ => ads.Release();
            mover.SwimmingChanged += swimming =>
            {
                if (swimming)
                {
                    ads.Release();
                }
            };
            ads.AimingChanged += aiming =>
            {
                if (aiming
                    && ((mover != null && (mover.IsDodging || mover.IsVaulting || mover.IsMantling || mover.IsSwimming))
                        || (weapon != null && weapon.IsHolstered)))
                {
                    ads.Release();
                }
            };
        }

        // ルールを撃つときに WeaponController.Rules から読むのは、キャンペーンが差し替えたルールにも従うため。
        // WireDodgeAndVault の射撃の止め方に足すので、その後に呼ぶ。
        private static void WireAimToFire(WeaponController weapon, AimDownSights ads, PlayerController mover)
        {
            if (weapon == null)
            {
                return;
            }

            BlindFireController blindFire = null;
            var previous = weapon.FireBlocker;
            weapon.FireBlocker = () =>
            {
                if (previous != null && previous())
                {
                    return true;
                }

                if (weapon == null || weapon.Rules.Actions.AimToFire != AimToFire.On || (ads != null && ads.IsAiming))
                {
                    return false;
                }

                // BlindFireController はこの後 PlayerActions.Attach が付けるので、撃つときに引く
                if (blindFire == null && mover != null)
                {
                    blindFire = mover.GetComponent<BlindFireController>();
                }

                return blindFire == null || !blindFire.IsBlindFiring;
            };
        }

        // 投げられるかはルールで決まり、Off なら投げないので、ここは常に配線しておく。
        // WireDodgeAndVault の射撃の止め方に足すので、その後に呼ぶ。
        private static void WireGrenades(GrenadeThrower grenades, PlayerController mover, WeaponController weapon, AimDownSights ads)
        {
            if (grenades == null)
            {
                return;
            }

            grenades.ThrowBlocker = () =>
                (mover != null && (mover.IsDodging || mover.IsVaulting || mover.IsMantling)) || (weapon != null && weapon.IsHolstered);

            if (weapon != null)
            {
                var previous = weapon.FireBlocker;
                weapon.FireBlocker = () => (previous != null && previous()) || (grenades != null && grenades.IsThrowing);

                // Thrown で直接 CancelReload しないのは、投げている間にリロードを始めることも近接と同じ modifier で止めるため。
                weapon.AddModifier(grenades);
            }

            if (ads != null)
            {
                grenades.Thrown += _ => ads.Release();
            }
        }

        // 読むのは実行時なので、未初期化のことがある Edit Mode でも配線だけしておく。
        private static void WireSpreadAndRecoil(WeaponController weapon, AimDownSights ads, PlayerController mover, FirstPersonLook look)
        {
            if (weapon == null)
            {
                return;
            }

            weapon.AdsProgressSource = () => ads != null ? ads.Progress : 0f;
            weapon.MoveFractionSource = () =>
            {
                var controller = mover != null ? mover.Controller : null;
                if (controller == null)
                {
                    return 0f;
                }

                var velocity = controller.velocity;
                return SpreadPattern.MoveFraction(new Vector2(velocity.x, velocity.z).magnitude, mover.WalkSpeed);
            };
            weapon.SpreadScaleSource = () => mover != null ? mover.SpreadScale : 1f;

            if (look != null)
            {
                look.Recoil = weapon.Recoil;
            }
        }

        // Edit Mode ではどれも未初期化のままのことがあるが、読むのは実行時なので配線だけしておく。
        private static void WireAds(AimDownSights ads, WeaponController weapon, PlayerController mover)
        {
            ads.WeaponSource = () => weapon != null && weapon.Weapon != null ? weapon.Weapon.Spec : null;
            // 泳ぎの状態は CharacterMotorState.CanAimDownSights と同じ答えにする (ネット対戦の予測と同じく、水の中では構えない)。
            ads.AdsBlocker = () =>
                (weapon != null && weapon.Weapon != null && weapon.Weapon.Trigger.BlocksAds)
                || (weapon != null && weapon.Weapon != null && weapon.Weapon.IsReloading && weapon.Rules.Actions.BlocksAimWhileReloading())
                || (mover != null && mover.TryGetMotorState(out var motor) && !motor.CanAimDownSights);
            ads.AimingChanged += aiming =>
            {
                if (aiming && mover != null)
                {
                    mover.ReleaseSprint(SprintInput.ReasonAim);
                }

                if (aiming && weapon != null)
                {
                    weapon.InterruptReload(ReloadInterrupt.Aim);
                }
            };

            if (weapon != null)
            {
                weapon.ReloadStarted += _ => ads.Release();
                weapon.WeaponChanged += _ => ads.Release();
            }
        }

        // カバーとブラインドファイア (PlayerActions.Attach) を読み元にするので、その後に呼ぶ。
        // Play 中は Reader の Awake が自力で読み元を解決するので、解決できなかったら失敗にする。Edit Mode では Awake が走らないのでここで渡す。
        private static PlayerActionReader AttachActionReader(
            GameObject player,
            PlayerController mover,
            WeaponController weapon,
            AimDownSights ads,
            Damageable damageable,
            ViewRig rig)
        {
            var reader = player.AddComponent<PlayerActionReader>();
            if (!reader.IsBound)
            {
                if (Application.isPlaying)
                {
                    throw new InvalidOperationException(
                        "PlayerActionReader could not resolve the player's components; see the error above");
                }

                reader.Bind(
                    mover, weapon, ads, player.GetComponent<CoverController>(), player.GetComponent<BlindFireController>(), damageable, rig);
            }

            player.AddComponent<PlayerMockBody>().Bind(reader);
            return reader;
        }

        // 手元のプレイヤーにだけ付ける。敵とネットのプレハブにも付く CharacterComponents に入れると、地図・振動・スティック・水中の画面が操作していない体の分まで動くため。
        // Play 中は付けた Awake / OnEnable が自力で参照を解決するが、Edit Mode では走らないので、要る参照はここで渡す。
        private static void AttachPlayerFeedback(GameObject player, ViewRig rig, Camera camera, Damageable damageable, SwimTuning swim)
        {
            player.AddComponent<PlayerMotorStateSource>();
            player.AddComponent<MinimapPublisher>();

            var rumble = player.AddComponent<GamepadRumble>();
            if (rumble.Rig == null && rig != null)
            {
                rumble.Bind(rig);
            }

            player.AddComponent<StickDeadzoneApplier>();

            var underwater = player.AddComponent<Traversal.UnderwaterPresenter>();
            if (camera != null)
            {
                underwater.Eye = camera.transform;
            }

            underwater.Damageable = damageable;

            // 息と溺れも PlayerController の泳ぎと同じシーンの値で数え、泳ぐ速さと息の長さが別々の調整値から来ないようにする。
            if (swim != null)
            {
                underwater.Swim = swim;
            }
        }

        // 乗っている間に止める覗き込み・カバー・グレネードを拾うので、それらを付けた後 (PlayerActions.Attach の後) に呼ぶ。
        // Edit Mode では Awake が走らないのでここで渡す。
        private static void AttachVehicleRider(GameObject player, PlayerController mover, WeaponController weapon)
        {
            var rider = player.AddComponent<PlayerVehicleRider>();
            if (rider.IsBound)
            {
                return;
            }

            if (Application.isPlaying)
            {
                throw new InvalidOperationException("PlayerVehicleRider could not resolve the player's components; see the error above");
            }

            rider.Bind(mover, weapon);
        }

        private static void WireDeathAndRespawn(Damageable damageable, PlayerController mover, AimDownSights ads)
        {
            damageable.Died += _ => ResetStance(mover, ads, CrouchInput.ReasonDeath, ProneInput.ReasonDeath);
            damageable.HealthReset += _ => ResetStance(mover, ads, CrouchInput.ReasonRespawn, ProneInput.ReasonRespawn);
            if (mover != null)
            {
                HoldControlWhileDead(damageable, mover);
            }
        }

        // 部品を止めるモードやキャンペーンが無い場面 (射撃場・機能シーン) でも、倒れたまま撃てないよう操作の口を引き取る。
        // 部品の有効・無効ではなく口で止めるのは、モードの SideMember.SetActing と取り合わないため。
        internal static object HoldControlWhileDead(Damageable damageable, PlayerController mover)
        {
            if (damageable == null)
            {
                throw new ArgumentNullException(nameof(damageable));
            }

            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            var holder = new DeadControlHold();
            damageable.Died += dead =>
            {
                if (mover != null && mover.Handoff.Claim(holder))
                {
                    Debug.Log(PlayerController.LogTag + " control held while " + dead.Id + " is dead", mover);
                }
            };
            damageable.HealthReset += _ =>
            {
                if (mover != null && mover.Handoff.Release(holder))
                {
                    Debug.Log(PlayerController.LogTag + " control returned to " + damageable.Id, mover);
                }
            };
            return holder;
        }

        private sealed class DeadControlHold
        {
        }

        private static void ResetStance(PlayerController mover, AimDownSights ads, string crouchReason, string proneReason)
        {
            if (mover != null)
            {
                mover.ReleaseCrouch(crouchReason);
                mover.ReleaseProne(proneReason);
            }

            // 専用サーバーや Edit Mode では ADS が初期化されておらず、戻す視野角も無い。
            if (ads != null && ads.IsInitialized)
            {
                ads.ResetView();
            }
        }
    }
}
