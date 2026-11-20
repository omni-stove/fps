using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class Hud : MonoBehaviour
    {
        public const string LogTag = "[HUD]";

        public const string LayoutResourcePath = "Hud/Hud";

        public const string ThemeResourcePath = "Hud/HudTheme";

        public const string PanelSettingsName = "HudPanelSettings";

        // Hud.uss の寸法はこの解像度で決めてあり、画面の大きさに合わせて拡縮する。
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);

        private readonly HudModel _model = new HudModel();
        private HudModel _shownModel;
        private HudView _view;
        private HudModel _noticeModel;
        private WeaponController _weapon;
        private AimDownSights _ads;
        private PlayerController _mover;
        private ViewRig _rig;
        private PanelSettings _ownedPanelSettings;
        private Color _alphaColor = Color.white;
        private Color _bravoColor = Color.white;
        private bool _followsPalette;
        private Team? _paletteViewer;
        private ColorPalette _paintedPalette;
        private HudVisibility _visibility = HudVisibility.Full;
        private LocalizedTexts _texts;
        private AimBlockCatalog _aimBlockCatalog;
        private bool _aimBlockCatalogResolved;
        private AimBlockSettings _aimBlockSettings;

        public HudModel Model => _model;

        public HudModel ShownModel => _shownModel ?? _model;

        public event Action ShownModelChanged;

        public bool ShowsTeamScores => ShownModel.FirstScoreTeam.HasValue;

        public AimBlockCatalog AimBlockCatalog
        {
            get
            {
                if (!_aimBlockCatalogResolved)
                {
                    _aimBlockCatalog = AimBlockCatalog.Load();
                    _aimBlockCatalogResolved = true;
                }

                return _aimBlockCatalog;
            }
            set
            {
                _aimBlockCatalog = value;
                _aimBlockCatalogResolved = true;
                _aimBlockSettings = null;
                _view?.SetAimBlockMarker(value != null ? value.Marker : null);
            }
        }

        public HudView View => _view;

        public PanelSettings OwnedPanelSettings => _ownedPanelSettings;

        // UIDocument が有効になる前に参照をすべて設定するため、GameObject は無効で作ってから有効にする。
        public static Hud Create(string objectName)
        {
            var layout = Resources.Load<VisualTreeAsset>(LayoutResourcePath);
            if (layout == null)
            {
                throw new InvalidOperationException(
                    $"HUD layout 'Resources/{LayoutResourcePath}.uxml' was not found (expected under UI/Resources of the Demo sample)");
            }

            var theme = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
            if (theme == null)
            {
                throw new InvalidOperationException(
                    $"HUD theme 'Resources/{ThemeResourcePath}.tss' was not found (expected under UI/Resources of the Demo sample)");
            }

            PanelSettings panelSettings = null;
            GameObject hudObject = null;
            try
            {
                panelSettings = DemoPanelSettings.Create(PanelSettingsName, theme);

                hudObject = new GameObject(objectName);
                hudObject.SetActive(false);
                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;

                var hud = hudObject.AddComponent<Hud>();
                AddViews(hudObject);
                hud.Initialize(panelSettings);

                // 有効にすると UIDocument が木を作り、Play 中は Hud の OnEnable が Label を探す。
                // Edit Mode では Hud の OnEnable が走らないので、木ができていればここで探す。
                hudObject.SetActive(true);
                if (!Application.isPlaying)
                {
                    hud.RebuildView();
                }

                return hud;
            }
            catch
            {
                DestroySafely(hudObject);
                DestroySafely(panelSettings);
                throw;
            }
        }

        public static void AddViews(GameObject hudObject)
        {
            if (hudObject == null)
            {
                throw new ArgumentNullException(nameof(hudObject));
            }

            AddIfMissing<HitMarkerView>(hudObject);
            AddIfMissing<DamageNumberView>(hudObject);
            AddIfMissing<DamageDirectionView>(hudObject);
            AddIfMissing<GrenadeWarningView>(hudObject);
            AddIfMissing<DamageVignetteView>(hudObject);
            AddIfMissing<ScopeOverlayView>(hudObject);
            AddIfMissing<FlashOverlay>(hudObject);
            AddIfMissing<KillFeedView>(hudObject);
            AddIfMissing<SubtitleView>(hudObject);
            AddIfMissing<ScoreboardView>(hudObject);
            AddIfMissing<LoadoutSelectView>(hudObject);
            AddIfMissing<InventoryView>(hudObject);
            AddIfMissing<TutorialPromptView>(hudObject);
            AddIfMissing<InteractPromptView>(hudObject);
            AddIfMissing<ModeHudView>(hudObject);
            AddIfMissing<KillCamView>(hudObject);
            AddIfMissing<GrenadeCountView>(hudObject);
            AddIfMissing<AbilitySlotsView>(hudObject);
            AddIfMissing<StreakView>(hudObject);
            AddIfMissing<ControlPointsView>(hudObject);
            AddIfMissing<PingMarkersView>(hudObject);
            AddIfMissing<SquadOrderMarkerView>(hudObject);
            AddIfMissing<MinimapView>(hudObject);
            AddIfMissing<CompassView>(hudObject);
            AddIfMissing<ChatView>(hudObject);
            AddIfMissing<VoiceIndicatorView>(hudObject);
            AddIfMissing<SpectateView>(hudObject);
            AddIfMissing<MapVoteView>(hudObject);
            AddIfMissing<VoteKickView>(hudObject);
            AddIfMissing<WalletView>(hudObject);
            AddIfMissing<BuyMenuView>(hudObject);
            AddIfMissing<VisionOverlayView>(hudObject);
            AddIfMissing<DroneHudView>(hudObject);
            AddIfMissing<NetStatsView>(hudObject);
            AddIfMissing<NameplateView>(hudObject);
            AddIfMissing<PauseMenu>(hudObject);
            // PauseMenu の後に付けると、木を作り直したときも PauseMenu が「設定」のボタンを持ってから開く処理を渡せる。
            AddIfMissing<PauseSettingsView>(hudObject);
            AddIfMissing<PhotoModeView>(hudObject);
        }

        public HitMarkerView HitMarker => GetComponent<HitMarkerView>();

        public DamageDirectionView DamageDirection => GetComponent<DamageDirectionView>();

        public DamageVignetteView DamageVignette => GetComponent<DamageVignetteView>();

        public FlashOverlay FlashOverlay => GetComponent<FlashOverlay>();

        public ScopeOverlayView ScopeOverlay => GetComponent<ScopeOverlayView>();

        public AimDownSights Ads => _ads;

        public KillFeedView KillFeed => GetComponent<KillFeedView>();

        public SubtitleView Subtitles => GetComponent<SubtitleView>();

        public ScoreboardView Scoreboard => GetComponent<ScoreboardView>();

        public LoadoutSelectView LoadoutSelect => GetComponent<LoadoutSelectView>();

        public InventoryView InventoryScreen => GetComponent<InventoryView>();

        public TutorialPromptView TutorialPrompt => GetComponent<TutorialPromptView>();

        public InteractPromptView InteractPrompt => GetComponent<InteractPromptView>();

        public ModeHudView ModeHud => GetComponent<ModeHudView>();

        public KillCamView KillCam => GetComponent<KillCamView>();

        public GrenadeCountView GrenadeCount => GetComponent<GrenadeCountView>();

        public AbilitySlotsView AbilitySlots => GetComponent<AbilitySlotsView>();

        public StreakView Streak => GetComponent<StreakView>();

        public PingMarkersView PingMarkers => GetComponent<PingMarkersView>();

        public SquadOrderMarkerView SquadOrder => GetComponent<SquadOrderMarkerView>();

        public MinimapView Minimap => GetComponent<MinimapView>();

        public CompassView Compass => GetComponent<CompassView>();

        public ChatView Chat => GetComponent<ChatView>();

        public VoiceIndicatorView VoiceIndicator => GetComponent<VoiceIndicatorView>();

        public SpectateView Spectate => GetComponent<SpectateView>();

        public MapVoteView MapVote => GetComponent<MapVoteView>();

        public VoteKickView VoteKick => GetComponent<VoteKickView>();

        public WalletView Wallet => GetComponent<WalletView>();

        public BuyMenuView BuyMenu => GetComponent<BuyMenuView>();

        public VisionOverlayView VisionOverlay => GetComponent<VisionOverlayView>();

        public DroneHudView DroneHud => GetComponent<DroneHudView>();

        public NetStatsView NetStats => GetComponent<NetStatsView>();

        public NameplateView Nameplates => GetComponent<NameplateView>();

        public PauseMenu PauseMenu => GetComponent<PauseMenu>();

        public PauseSettingsView PauseSettings => GetComponent<PauseSettingsView>();

        public PhotoModeView PhotoMode => GetComponent<PhotoModeView>();

        public void SetPauseTimeMode(PauseTimeMode mode)
        {
            var pause = PauseMenu;
            if (pause != null)
            {
                pause.TimeMode = mode;
            }
        }

        public void SetTeamColors(Color alpha, Color bravo)
        {
            StopFollowingPalette();
            ApplyTeamColors(alpha, bravo);
        }

        public void UseTeamPalette(Team? viewer)
        {
            _paletteViewer = viewer;
            if (!_followsPalette)
            {
                AppliedControlSettings.Changed += HandleSettingsChanged;
                _followsPalette = true;
            }

            ApplyPaletteColors();
        }

        public bool FollowsTeamPalette => _followsPalette;

        public Color TeamColor(Team team)
        {
            return team == Team.Bravo ? _bravoColor : _alphaColor;
        }

        private void ApplyTeamColors(Color alpha, Color bravo)
        {
            _alphaColor = alpha;
            _bravoColor = bravo;
            _view?.SetTeamColors(alpha, bravo);

            var scoreboard = Scoreboard;
            if (scoreboard != null)
            {
                scoreboard.RefreshTeamColors();
            }

            var killFeed = KillFeed;
            if (killFeed != null)
            {
                killFeed.RefreshTeamColors();
            }

            var controlPoints = GetComponent<ControlPointsView>();
            if (controlPoints != null)
            {
                controlPoints.SetTeamColors(alpha, bravo);
            }
        }

        private void ApplyPaletteColors()
        {
            var palette = TeamColors.Current;
            _paintedPalette = palette.Kind;
            ApplyTeamColors(
                TeamColors.Of(palette, Team.Alpha, _paletteViewer),
                TeamColors.Of(palette, Team.Bravo, _paletteViewer));
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            // 破棄の後に届いた通知では消えた要素を塗らない。感度などほかの設定だけが変わったときも、キルログを作り直さない
            if (this == null || settings.Palette == _paintedPalette)
            {
                return;
            }

            ApplyPaletteColors();
        }

        private void StopFollowingPalette()
        {
            if (_followsPalette)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsPalette = false;
            }
        }

        public HudVisibility Visibility => _visibility;

        public void ApplyDensity(HudDensity density)
        {
            ApplyVisibility(HudVisibility.For(density));
        }

        public void ApplyVisibility(HudVisibility visibility)
        {
            if (visibility == null)
            {
                throw new ArgumentNullException(nameof(visibility));
            }

            var changed = visibility != _visibility;
            _visibility = visibility;
            ShownModel.SetReserveAmmoShown(visibility.ReserveAmmo);
            _view?.ApplyVisibility(visibility);
            ApplyVisibilityToElementViews();

            if (changed)
            {
                Debug.Log($"{LogTag} density={visibility.Density}", this);
            }
        }

        private void Initialize(PanelSettings ownedPanelSettings)
        {
            UnsubscribeSources();
            _ownedPanelSettings = ownedPanelSettings;
            SubscribeSignals();
            RebuildView();
        }

        // 覗き込み・移動・視点が player に無ければ武器の親の階層から探すのは、武器だけを渡すプレイヤーもあるため。
        private void BindLocalPlayer(LocalPlayerSignal player)
        {
            var weapon = player != null ? player.Weapon : null;
            _weapon = weapon;
            _ads = player != null && player.Ads != null ? player.Ads : weapon != null ? weapon.GetComponentInParent<AimDownSights>(true) : null;
            _mover = player != null && player.Mover != null ? player.Mover : weapon != null ? weapon.GetComponentInParent<PlayerController>(true) : null;
            _rig = weapon != null ? weapon.GetComponentInParent<ViewRig>(true) : null;
            var damageNumbers = GetComponent<DamageNumberView>();
            if (damageNumbers != null)
            {
                damageNumbers.Viewer = _ads != null ? _ads.Camera : null;
            }

            // オフラインのプレイヤーは持ち物を自分で持つ。ネット対戦は持ち主の HUD が届いた持ち物を後から渡す
            var inventoryView = InventoryScreen;
            var damageable = player != null ? player.Damageable : null;
            var ownInventory = damageable != null ? damageable.GetComponent<PlayerInventory>() : null;
            if (inventoryView != null && ownInventory != null)
            {
                inventoryView.Source = ownInventory;
                var progression = ProgressionBootstrap.Recorder;
                inventoryView.ShowUnlocks(progression?.Ledger, progression?.Profile);
            }

            if (_weapon == null)
            {
                _model.ResetCrosshair();
                ApplyAimBlock(_model, false, null);
            }
            else
            {
                RefreshCrosshair();
            }
        }

        private void HandleLocalPlayerChanged(LocalPlayerSignal player)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                UnsubscribeSignals();
                return;
            }

            BindLocalPlayer(player);
        }

        // 案内は出している値に書く。出す値が替わったら、前の値からは案内を外す
        private void BindNoticeTo(HudModel model)
        {
            if (_noticeModel == model)
            {
                return;
            }

            _noticeModel?.BindNotice(null);
            _noticeModel = model;
            _noticeModel?.BindNotice(CampaignSignals.Notice.Value);
        }

        private void HandleNoticeChanged(CampaignNotice notice)
        {
            if (this == null)
            {
                UnsubscribeSignals();
                return;
            }

            _noticeModel?.BindNotice(notice);
        }

        // UIDocument は有効にし直すたびに木を作り直すので、そのたびに Label を探し直す。
        public bool RebuildView()
        {
            DisposeView();

            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root == null || root.childCount == 0)
            {
                return false;
            }

            CreateView(root);
            _texts = LocalizedTexts.Bind(root);
            Localizer.Changed += HandleLanguageChanged;
            ApplyVisibilityToElementViews();
            foreach (var view in GetComponents<HudElementView>())
            {
                view.Bind(root);
            }

            return true;
        }

        private void CreateView(VisualElement root)
        {
            _view = new HudView(root, ShownModel, _visibility);
            _view.SetTeamColors(_alphaColor, _bravoColor);
            var aimBlockCatalog = AimBlockCatalog;
            _view.SetAimBlockMarker(aimBlockCatalog != null ? aimBlockCatalog.Marker : null);
            _view.SetAimHidden(GameSignals.StreakTargeting.Value.HasValue);
        }

        private void SubscribeSignals()
        {
            GameSignals.HudModel.Changed += HandleHudModelChanged;
            GameSignals.LocalPlayer.Changed += HandleLocalPlayerChanged;
            CampaignSignals.Notice.Changed += HandleNoticeChanged;
            GameSignals.HudDensity.Changed += HandleDensityChanged;
            GameSignals.PauseTimeMode.Changed += HandlePauseTimeModeChanged;
            GameSignals.PaletteViewer.Changed += HandlePaletteViewerChanged;
            GameSignals.StreakTargeting.Changed += HandleStreakTargetingChanged;
            HandleHudModelChanged(GameSignals.HudModel.Value);
            BindNoticeTo(ShownModel);
            BindLocalPlayer(GameSignals.LocalPlayer.Value);
            HandleDensityChanged(GameSignals.HudDensity.Value);
            HandlePauseTimeModeChanged(GameSignals.PauseTimeMode.Value);
            HandlePaletteViewerChanged(GameSignals.PaletteViewer.Value);
            HandleStreakTargetingChanged(GameSignals.StreakTargeting.Value);
        }

        private void UnsubscribeSignals()
        {
            GameSignals.HudModel.Changed -= HandleHudModelChanged;
            GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
            CampaignSignals.Notice.Changed -= HandleNoticeChanged;
            GameSignals.HudDensity.Changed -= HandleDensityChanged;
            GameSignals.PauseTimeMode.Changed -= HandlePauseTimeModeChanged;
            GameSignals.PaletteViewer.Changed -= HandlePaletteViewerChanged;
            GameSignals.StreakTargeting.Changed -= HandleStreakTargetingChanged;
        }

        private void HandleHudModelChanged(HudModel model)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                UnsubscribeSignals();
                return;
            }

            var shown = model != null && model != _model ? model : null;
            if (shown == _shownModel)
            {
                return;
            }

            _shownModel = shown;
            ShownModel.SetReserveAmmoShown(_visibility.ReserveAmmo);
            BindNoticeTo(ShownModel);
            if (_view != null)
            {
                var document = GetComponent<UIDocument>();
                _view.Dispose();
                _view = null;
                CreateView(document.rootVisualElement);
            }

            ShownModelChanged?.Invoke();
        }

        // 値が消されたときは今のままにする。出した側が片付けるのは HUD を消すときなので、戻す先が無いため。
        private void HandleDensityChanged(HudDensity? density)
        {
            if (this != null && density.HasValue)
            {
                ApplyDensity(density.Value);
            }
        }

        private void HandlePauseTimeModeChanged(PauseTimeMode? mode)
        {
            if (this != null && mode.HasValue)
            {
                SetPauseTimeMode(mode.Value);
            }
        }

        private void HandlePaletteViewerChanged(Team? viewer)
        {
            if (this != null && viewer.HasValue)
            {
                UseTeamPalette(viewer.Value);
            }
        }

        private void HandleStreakTargetingChanged(StreakTargetingStatus? targeting)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                UnsubscribeSignals();
                return;
            }

            _view?.SetAimHidden(targeting.HasValue);
        }

        private void OnEnable()
        {
            TryRebuildViewAndLog();
        }

        private void Start()
        {
            // OnEnable の時点で UIDocument の木がまだ無かった場合の取りこぼしを拾う。
            if (_view == null)
            {
                TryRebuildViewAndLog();
            }

            if (_view == null)
            {
                Debug.LogError($"{LogTag} the UIDocument has no visual tree; check its PanelSettings and source asset", this);
            }
        }

        private void LateUpdate()
        {
            // 自分のプレイヤーがいない HUD (ネット対戦) は出す側が Set 系で進みや照準を書くので、ここで読み直して消さない。
            if (_weapon != null)
            {
                ShownModel.RefreshReloadProgress();
                ShownModel.RefreshWeaponGauges();
                RefreshMagnification();
                RefreshCrosshair();
            }
        }

        // 倍率は覗き込みの状態で決まり、HudModel が持つ Weapon からは読めないので、ADS から読んで書く。
        private void RefreshMagnification()
        {
            var magnification = _ads != null ? _ads.Magnification : null;
            if (magnification.HasValue)
            {
                ShownModel.SetMagnification(magnification.Value);
            }
            else
            {
                ShownModel.ClearMagnification();
            }
        }

        public void RefreshCrosshair()
        {
            if (_weapon == null)
            {
                return;
            }

            var aiming = _ads != null && _ads.IsAiming;
            var sprinting = _mover != null && _mover.IsSprinting;
            var view = _rig != null ? _rig.Mode : ViewMode.FirstPerson;
            var throughSight = _ads != null && _ads.IsInitialized && _ads.SightView.ThroughSight;
            var shown = ShowsCrosshair(throughSight ? ViewMode.FirstPerson : view, aiming, sprinting)
                && SeatShowsCrosshair(GameSignals.SeatFiring.Value);
            ShownModel.SetCrosshair(_weapon.CrosshairSpreadFraction, CrosshairConeGap(), shown);
            RefreshAimBlock(shown);
        }

        private void RefreshAimBlock(bool crosshairShown)
        {
            var settings = AimBlockSettingsInUse();
            var aim = _weapon.Aim;
            var eye = _rig != null ? ViewRig.FindEye(_rig.transform) : null;
            var range = _weapon.HitscanRange;
            if (!crosshairShown || !settings.Enabled || aim == null || eye == null || aim.parent != eye
                || !(range > 0f) || float.IsInfinity(range))
            {
                ApplyAimBlock(ShownModel, false, null);
                return;
            }

            if (!ViewRig.TryFindAimBlock(aim, eye, range, aim.root, _weapon.HitMask, settings.Margin, out var point))
            {
                ApplyAimBlock(ShownModel, false, null);
                return;
            }

            var camera = _ads != null && _ads.Camera != null ? _ads.Camera : aim.GetComponent<Camera>();
            ApplyAimBlock(ShownModel, true, AimBlockViewport(camera, point));
        }

        public static System.Numerics.Vector2? AimBlockViewport(Camera camera, Vector3 world)
        {
            if (camera == null)
            {
                return null;
            }

            var viewport = camera.WorldToViewportPoint(world);
            if (!(viewport.z > 0f)
                || float.IsNaN(viewport.x) || float.IsInfinity(viewport.x)
                || float.IsNaN(viewport.y) || float.IsInfinity(viewport.y))
            {
                return null;
            }

            return new System.Numerics.Vector2(viewport.x, viewport.y);
        }

        private void ApplyAimBlock(HudModel model, bool blocked, System.Numerics.Vector2? viewport)
        {
            var changed = blocked != model.IsAimBlocked;
            if (blocked)
            {
                model.SetAimBlock(viewport);
            }
            else
            {
                model.ClearAimBlock();
            }

            if (changed)
            {
                Debug.Log($"{LogTag} aim blocked={blocked}", this);
            }
        }

        private AimBlockSettings AimBlockSettingsInUse()
        {
            if (_aimBlockSettings != null)
            {
                return _aimBlockSettings;
            }

            var catalog = AimBlockCatalog;
            if (catalog == null)
            {
                _aimBlockSettings = AimBlockSettings.Default;
                return _aimBlockSettings;
            }

            try
            {
                _aimBlockSettings = catalog.ToSettings();
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogWarning($"{LogTag} aim block catalog '{catalog.name}' is invalid; using the defaults: {e.Message}", this);
                _aimBlockSettings = AimBlockSettings.Default;
            }

            return _aimBlockSettings;
        }

        // 運転席や助手席のように撃てない席では、狙う物が無いので照準を出さない。
        public static bool SeatShowsCrosshair(VehicleSeatFiring? firing)
        {
            return firing != VehicleSeatFiring.None;
        }

        public static bool ShowsCrosshair(ViewMode view, bool aiming, bool sprinting)
        {
            // 三人称の ADS は肩越しに寄るだけでサイトを覗かないので、照準を消すと狙う点が分からなくなる。
            return !sprinting && (!aiming || view == ViewMode.ThirdPerson);
        }

        public float CrosshairConeGap()
        {
            var camera = _ads != null ? _ads.Camera : null;
            if (_weapon == null || camera == null || camera.orthographic)
            {
                return 0f;
            }

            var cone = _weapon.CrosshairConeDegrees;
            var fov = camera.fieldOfView;
            if (cone <= 0f || !(fov > 0f && fov < 180f))
            {
                return 0f;
            }

            return SpreadPattern.ScreenRadius(cone, fov, PanelHalfHeight());
        }

        private float PanelHalfHeight()
        {
            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            var height = root != null ? root.layout.height : float.NaN;
            return float.IsNaN(height) || height <= 0f ? ReferenceResolution.y * 0.5f : height * 0.5f;
        }

        private void TryRebuildViewAndLog()
        {
            try
            {
                if (RebuildView())
                {
                    var shown = ShownModel;
                    Debug.Log($"{LogTag} ready hp={HudNumberFormat.Health(shown)} weapon={shown.WeaponSpec?.Id} ammo={HudNumberFormat.Ammo(shown)} time={HudNumberFormat.Time(shown)}", this);
                }
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{LogTag} could not bind the HUD layout: {e.Message}", this);
            }
        }

        private void OnDisable()
        {
            DisposeView();
        }

        private void OnDestroy()
        {
            StopFollowingPalette();
            DisposeView();
            UnsubscribeSources();
            BindNoticeTo(null);
            _model.UnbindAll();

            if (_ownedPanelSettings != null)
            {
                Destroy(_ownedPanelSettings);
                _ownedPanelSettings = null;
            }
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void DisposeView()
        {
            foreach (var view in GetComponents<HudElementView>())
            {
                // 破棄の途中では先に消えたビューが null として返ることがある。
                if (view != null)
                {
                    view.Unbind();
                }
            }

            Localizer.Changed -= HandleLanguageChanged;
            _texts = null;
            if (_view == null)
            {
                return;
            }

            _view.Dispose();
            _view = null;
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            // 破棄の後に届いた通知では消えた要素に書かない。
            if (this == null)
            {
                return;
            }

            _texts?.Refresh();
            _view?.RefreshText();
        }

        // スコアボード・ロードアウト選択・ポーズは自分で開くものなので、HUD の量では隠さない
        private void ApplyVisibilityToElementViews()
        {
            var hitMarker = HitMarker;
            if (hitMarker != null)
            {
                hitMarker.SetAllowed(_visibility.HitMarker);
            }

            // ダメージの数値も当てた手応えなので、ハードコアで隠すヒットマーカーと一緒に出し入れする。
            var damageNumbers = GetComponent<DamageNumberView>();
            if (damageNumbers != null)
            {
                damageNumbers.SetAllowed(_visibility.HitMarker);
            }

            var damageDirection = DamageDirection;
            if (damageDirection != null)
            {
                damageDirection.SetAllowed(_visibility.DamageDirection);
            }

            // 近くのグレネードも被弾方向と同じく身の危険の向きを知らせるものなので、一緒に出し入れする。
            var grenadeWarning = GetComponent<GrenadeWarningView>();
            if (grenadeWarning != null)
            {
                grenadeWarning.SetAllowed(_visibility.DamageDirection);
            }

            var killFeed = KillFeed;
            if (killFeed != null)
            {
                killFeed.SetAllowed(_visibility.KillFeed);
            }

            // グレネードの数は持っている弾の一種なので、弾数と一緒に出し入れする。
            var grenades = GetComponent<GrenadeCountView>();
            if (grenades != null)
            {
                grenades.SetAllowed(_visibility.Ammo);
            }

            // 能力の回数も持っているものの残りなので、弾数と一緒に出し入れする。
            var abilities = GetComponent<AbilitySlotsView>();
            if (abilities != null)
            {
                abilities.SetAllowed(_visibility.Ammo);
            }

            var minimap = Minimap;
            if (minimap != null)
            {
                minimap.SetAllowed(_visibility.Minimap);
            }

            var compass = Compass;
            if (compass != null)
            {
                compass.SetAllowed(_visibility.Compass);
            }
        }

        private static void AddIfMissing<T>(GameObject target)
            where T : Component
        {
            if (target.GetComponent<T>() == null)
            {
                target.AddComponent<T>();
            }
        }

        private void UnsubscribeSources()
        {
            UnsubscribeSignals();
            _weapon = null;
            _ads = null;
            _mover = null;
            _rig = null;
        }
    }
}
