using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    private sealed class TurretRuntimeState
    {
        public FlyingAircraft Target;
        public float NextTargetRefreshTime;
        public float NextShotTime;
        public float BurstEndTime;
        public float ReadyTime;
        public int NextBarrelIndex;
        public int BazookaShotsRemaining;
        public bool IsBursting;
    }

    private sealed class TurretUpgradeSlot
    {
        public GameObject Root;
        public TowerTurret Turret;
        public TowerTurret.UpgradeType Type;
        public bool IsPurchased;
    }

    public static GameManager Instance { get; private set; }
    private static Material tracerMaterial;

    [Header("Character materials")]
    [SerializeField] private Material armMaterial;
    [SerializeField] private Material beardMaterial;
    [SerializeField] private Material hatMaterial;
    [SerializeField] private Material headMaterial;
    [SerializeField] private Material shirtMaterial;
    [SerializeField] private Material eyeWhiteMaterial;
    [SerializeField] private Material pupilsMaterial;

    [Header("Customization")]
    [SerializeField] private Light lightToDisableAfterCustomization;

    [Header("Coin rewards")]
    [SerializeField, Min(0)] private int startingCoins;
    [SerializeField, Min(0)] private int coinsForFastKill = 300;
    [SerializeField, Min(0f)] private float fastKillTimeSeconds = 15f;
    [SerializeField, Min(0)] private int coinsLostPerTimeStep = 20;
    [SerializeField, Min(0.01f)] private float killRewardTimeStepSeconds = 5f;
    [SerializeField, Min(0f)] private float coinsLostPerTowerHealth = 5f;

    [Header("Turret parents")]
    [Tooltip("Parent containing scene turret slots with TowerTurret components. Each slot needs its aim pivot and barrels assigned.")]
    [SerializeField] private Transform obliteratorTurretParent;
    [Tooltip("Parent containing scene turret slots with TowerTurret components. Each slot needs its aim pivot and barrel(s) assigned.")]
    [SerializeField] private Transform bazookaTurretParent;
    [Tooltip("Optional additional slots. The type is read from each TowerTurret component.")]
    [SerializeField] private TowerTurret[] additionalTurretSlots;
    [SerializeField, Min(0)] private int obliteratorPrice = 1000;
    [SerializeField, Min(0)] private int bazookaTurretPrice = 1500;
    [SerializeField] private TowerTurret.Settings turretSettings = new TowerTurret.Settings();

    private readonly List<TurretUpgradeSlot> availableTurretSlots = new List<TurretUpgradeSlot>();
    private readonly Dictionary<TowerTurret, TurretRuntimeState> turretRuntimeStates =
        new Dictionary<TowerTurret, TurretRuntimeState>();
    private bool towerUpgradesInitialized;
    private bool isBetweenWaveShopOpen;
    private int selectedShopOption;
    private GUIStyle shopTitleStyle;
    private GUIStyle shopButtonStyle;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisibility;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void InitializeTowerUpgrades()
    {
        if (towerUpgradesInitialized) return;
        towerUpgradesInitialized = true;

        TowerHealth towerHealth = FindObjectOfType<TowerHealth>();
        if (towerHealth != null)
        {
            towerHealth.ConfigureCoinEconomy(
                startingCoins,
                coinsForFastKill,
                fastKillTimeSeconds,
                coinsLostPerTimeStep,
                killRewardTimeStepSeconds,
                coinsLostPerTowerHealth);
        }
        else
        {
            Debug.LogError("GameManager could not find TowerHealth to configure coin rewards.", this);
        }

        availableTurretSlots.Clear();
        turretRuntimeStates.Clear();
        AddTurretSlots(obliteratorTurretParent, TowerTurret.UpgradeType.Obliterator);
        AddTurretSlots(bazookaTurretParent, TowerTurret.UpgradeType.BazookaTurret);
        if (additionalTurretSlots != null)
        {
            foreach (TowerTurret turret in additionalTurretSlots)
            {
                if (turret == null) continue;

                turret.SetSlotRoot(turret.SlotRoot);
                AddTurretUpgradeSlot(turret.SlotRoot, turret, turret.Type);
            }
        }

        if (availableTurretSlots.Count == 0)
        {
            Debug.LogWarning(
                "No turret child slots were found. Assign turret parents on GameManager and place each turret as a direct child.",
                this);
            return;
        }

        foreach (TurretUpgradeSlot slot in availableTurretSlots)
        {
            if (slot.Root == null) continue;

            if (slot.Turret != null)
            {
                turretRuntimeStates[slot.Turret] = new TurretRuntimeState();
                if (!HasRequiredTurretConfiguration(slot.Turret))
                {
                    Debug.LogWarning(
                        $"Turret '{slot.Root.name}' is counted as a shop slot but will not fire until its barrels and projectile configuration are assigned.",
                        slot.Turret);
                }

                slot.Turret.SetPurchased(false);
            }

            slot.Root.SetActive(false);
        }
    }

    private void AddTurretSlots(Transform parent, TowerTurret.UpgradeType type)
    {
        if (parent == null) return;

        parent.gameObject.SetActive(true);
        for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
        {
            Transform slotRoot = parent.GetChild(childIndex);
            TowerTurret turret = slotRoot.GetComponent<TowerTurret>();
            if (turret == null) turret = slotRoot.GetComponentInChildren<TowerTurret>(true);
            if (turret == null) turret = slotRoot.gameObject.AddComponent<TowerTurret>();
            if (turret != null) turret.SetUpgradeType(type);
            AddTurretUpgradeSlot(slotRoot.gameObject, turret, type);
        }
    }

    private void AddTurretUpgradeSlot(GameObject slotRoot, TowerTurret turret, TowerTurret.UpgradeType type)
    {
        if (slotRoot == null) return;

        foreach (TurretUpgradeSlot existingSlot in availableTurretSlots)
        {
            if (existingSlot.Root == slotRoot) return;
        }

        if (turret != null) turret.SetSlotRoot(slotRoot);
        availableTurretSlots.Add(new TurretUpgradeSlot
        {
            Root = slotRoot,
            Turret = turret,
            Type = type
        });
    }

    private bool HasRequiredTurretConfiguration(TowerTurret turret)
    {
        return turret.HasRequiredConfiguration
            && (turret.Type != TowerTurret.UpgradeType.BazookaTurret
                || turretSettings.bazookaProjectilePrefab != null);
    }

    public IEnumerator WaitForBetweenWaveShop()
    {
        if (!towerUpgradesInitialized) InitializeTowerUpgrades();

        selectedShopOption = 0;
        isBetweenWaveShopOpen = true;
        float previousTimeScale = Time.timeScale;
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisibility = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        while (isBetweenWaveShopOpen) yield return null;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisibility;
    }

    private void Update()
    {
        UpdatePurchasedTurrets();
        if (!isBetweenWaveShopOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame) selectedShopOption = Mathf.Max(0, selectedShopOption - 1);
            if (keyboard.rightArrowKey.wasPressedThisFrame) selectedShopOption = Mathf.Min(2, selectedShopOption + 1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            {
                ActivateShopOption();
                return;
            }
        }

        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame)
            {
                selectedShopOption = Mathf.Max(0, selectedShopOption - 1);
            }
            else if (gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame)
            {
                selectedShopOption = Mathf.Min(2, selectedShopOption + 1);
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                ActivateShopOption();
                return;
            }
        }
    }

    private void UpdatePurchasedTurrets()
    {
        if (Time.timeScale <= 0f) return;

        foreach (TurretUpgradeSlot slot in availableTurretSlots)
        {
            TowerTurret turret = slot.Turret;
            if (turret == null || !slot.IsPurchased || !slot.Root.activeInHierarchy) continue;
            if (!turretRuntimeStates.TryGetValue(turret, out TurretRuntimeState state)) continue;

            UpdateTurret(turret, state);
        }
    }

    private void UpdateTurret(TowerTurret turret, TurretRuntimeState state)
    {
        if (Time.time >= state.NextTargetRefreshTime)
        {
            state.Target = FindNearestTarget(turret);
            state.NextTargetRefreshTime = Time.time + 0.2f;
        }

        AimTurretAtTarget(turret, state.Target);
        if (Time.time < state.ReadyTime) return;

        if (turret.Type == TowerTurret.UpgradeType.Obliterator)
        {
            UpdateObliterator(turret, state);
        }
        else
        {
            UpdateBazookaTurret(turret, state);
        }
    }

    private FlyingAircraft FindNearestTarget(TowerTurret turret)
    {
        FlyingAircraft[] aircraft = FindObjectsOfType<FlyingAircraft>();
        FlyingAircraft nearest = null;
        float nearestDistanceSquared = turretSettings.targetRange * turretSettings.targetRange;

        foreach (FlyingAircraft candidate in aircraft)
        {
            if (!IsValidTurretTarget(candidate)) continue;

            float distanceSquared = (candidate.transform.position - turret.transform.position).sqrMagnitude;
            if (distanceSquared > nearestDistanceSquared) continue;

            nearest = candidate;
            nearestDistanceSquared = distanceSquared;
        }

        return nearest;
    }

    private void AimTurretAtTarget(TowerTurret turret, FlyingAircraft target)
    {
        if (!IsTargetInTurretRange(turret, target)) return;

        Transform aimPivot = turret.AimPivot;
        Vector3 direction = target.transform.position - aimPivot.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.99f
            ? Vector3.forward
            : Vector3.up;
        Quaternion targetRotation = Quaternion.LookRotation(direction, up);
        aimPivot.rotation = Quaternion.RotateTowards(
            aimPivot.rotation,
            targetRotation,
            turretSettings.aimSpeed * Time.deltaTime);
    }

    private void UpdateObliterator(TowerTurret turret, TurretRuntimeState state)
    {
        if (!state.IsBursting)
        {
            if (!IsTargetInTurretRange(turret, state.Target)) return;

            state.IsBursting = true;
            state.BurstEndTime = Time.time + turretSettings.firingDuration;
            state.NextShotTime = Time.time;
        }

        if (Time.time >= state.BurstEndTime)
        {
            state.IsBursting = false;
            BeginTurretCooldown(
                turret,
                state,
                turretSettings.obliteratorCooldown,
                turretSettings.obliteratorCooldownEffectPrefab);
            return;
        }

        float shotInterval = 1f / Mathf.Max(0.01f, turretSettings.bulletsPerSecond);
        while (Time.time >= state.NextShotTime && state.NextShotTime < state.BurstEndTime)
        {
            if (IsTargetInTurretRange(turret, state.Target)) FireObliteratorBullet(turret, state);
            state.NextShotTime += shotInterval;
        }
    }

    private void FireObliteratorBullet(TowerTurret turret, TurretRuntimeState state)
    {
        Transform barrel = GetNextBarrel(turret, state, 9);
        if (barrel == null || !IsTargetInTurretRange(turret, state.Target)) return;

        ShowTracer(barrel.position, state.Target.transform.position);
        state.Target.TakeDamage(turretSettings.bulletDamage);
    }

    private void UpdateBazookaTurret(TowerTurret turret, TurretRuntimeState state)
    {
        if (state.BazookaShotsRemaining == 0)
        {
            if (!IsTargetInTurretRange(turret, state.Target)) return;
            state.BazookaShotsRemaining = Mathf.Max(1, turretSettings.bazookaShotsPerBurst);
            state.NextShotTime = Time.time;
        }

        if (Time.time < state.NextShotTime || !IsTargetInTurretRange(turret, state.Target)) return;

        if (!FireBazooka(turret, state))
        {
            state.BazookaShotsRemaining = 0;
            BeginTurretCooldown(
                turret,
                state,
                turretSettings.bazookaCooldown,
                turretSettings.bazookaCooldownEffectPrefab);
            return;
        }

        state.BazookaShotsRemaining--;
        if (state.BazookaShotsRemaining == 0)
        {
            BeginTurretCooldown(
                turret,
                state,
                turretSettings.bazookaCooldown,
                turretSettings.bazookaCooldownEffectPrefab);
        }
        else
        {
            state.NextShotTime = Time.time + turretSettings.bazookaTimeBetweenShots;
        }
    }

    private bool FireBazooka(TowerTurret turret, TurretRuntimeState state)
    {
        Transform barrel = GetNextBarrel(turret, state, turret.Barrels != null ? turret.Barrels.Length : 0);
        if (barrel == null || turretSettings.bazookaProjectilePrefab == null) return false;

        GameObject projectileObject = Instantiate(
            turretSettings.bazookaProjectilePrefab,
            barrel.position,
            barrel.rotation);
        BazookaProjectile projectile = projectileObject.GetComponent<BazookaProjectile>();
        if (projectile == null)
        {
            Debug.LogError("The bazooka turret projectile prefab needs a BazookaProjectile component.", projectileObject);
            Destroy(projectileObject);
            return false;
        }

        Vector3 direction = state.Target.transform.position - barrel.position;
        if (direction.sqrMagnitude < 0.0001f) direction = barrel.forward;
        projectile.Launch(
            direction,
            turretSettings.bazookaProjectileSpeed,
            turretSettings.bazookaProjectileLifetime,
            turret.OwnerColliders,
            turretSettings.bazookaProjectileDamage,
            turretSettings.bazookaSmokeTrailPrefab,
            turretSettings.bazookaImpactEffectPrefab,
            0f,
            0f,
            0f,
            turretSettings.initialHitboxIgnoreDistance);
        return true;
    }

    private Transform GetNextBarrel(TowerTurret turret, TurretRuntimeState state, int maximumBarrels)
    {
        Transform[] barrels = turret.Barrels;
        if (barrels == null || barrels.Length == 0 || maximumBarrels <= 0) return null;

        int barrelCount = Mathf.Min(maximumBarrels, barrels.Length);
        for (int i = 0; i < barrelCount; i++)
        {
            int index = state.NextBarrelIndex++ % barrelCount;
            if (barrels[index] != null) return barrels[index];
        }

        return null;
    }

    private void BeginTurretCooldown(
        TowerTurret turret,
        TurretRuntimeState state,
        float duration,
        ParticleSystem effectPrefab)
    {
        state.ReadyTime = Time.time + duration;
        if (effectPrefab == null) return;

        Transform effectOrigin = turret.AimPivot;
        ParticleSystem effect = Instantiate(effectPrefab, effectOrigin.position, effectOrigin.rotation);
        ParticleSystem.MainModule main = effect.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        effect.Play(true);
    }

    private bool IsTargetInTurretRange(TowerTurret turret, FlyingAircraft aircraft)
    {
        return IsValidTurretTarget(aircraft)
            && (aircraft.transform.position - turret.transform.position).sqrMagnitude
                <= turretSettings.targetRange * turretSettings.targetRange;
    }

    private static bool IsValidTurretTarget(FlyingAircraft aircraft)
    {
        return aircraft != null && aircraft.IsVisibleOnRadar && !aircraft.IsFalling;
    }

    private void ShowTracer(Vector3 start, Vector3 end)
    {
        GameObject tracerObject = new GameObject("Obliterator tracer");
        LineRenderer line = tracerObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, end);
        line.SetPosition(1, start);
        line.startWidth = turretSettings.tracerWidth;
        line.endWidth = turretSettings.tracerWidth * 0.35f;
        line.useWorldSpace = true;
        line.sharedMaterial = GetTracerMaterial();
        line.startColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        line.endColor = new Color(0.55f, 0.55f, 0.55f, 0.1f);
        Destroy(tracerObject, turretSettings.tracerDuration);
    }

    private static Material GetTracerMaterial()
    {
        if (tracerMaterial != null) return tracerMaterial;

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        tracerMaterial = new Material(shader);
        return tracerMaterial;
    }

    private void ActivateShopOption()
    {
        if (selectedShopOption == 0)
        {
            TryPurchaseTowerUpgrade(TowerTurret.UpgradeType.Obliterator);
        }
        else if (selectedShopOption == 1)
        {
            TryPurchaseTowerUpgrade(TowerTurret.UpgradeType.BazookaTurret);
        }
        else
        {
            isBetweenWaveShopOpen = false;
        }
    }

    private void OnGUI()
    {
        if (isBetweenWaveShopOpen) DrawBetweenWaveShop();
    }

    private void DrawBetweenWaveShop()
    {
        TowerHealth towerHealth = TowerHealth.Instance;
        int coins = towerHealth != null ? towerHealth.Coins : 0;
        float panelWidth = Mathf.Min(1100f, Screen.width * 0.9f);
        float panelHeight = Mathf.Min(700f, Screen.height * 0.88f);
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);
        float horizontalPadding = panelWidth * 0.045f;
        float contentWidth = panelWidth - horizontalPadding * 2f;
        float titleHeight = panelHeight * 0.12f;
        float titleFontSize = Mathf.Clamp(panelWidth * 0.035f, 22f, 40f);
        float buttonFontSize = Mathf.Clamp(panelWidth * 0.026f, 18f, 32f);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (shopTitleStyle == null)
        {
            shopTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(titleFontSize)
            };
        }

        if (shopButtonStyle == null)
        {
            shopButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(buttonFontSize),
                wordWrap = true
            };
        }

        GUI.Label(
            new Rect(panel.x + horizontalPadding, panel.y + panelHeight * 0.035f, contentWidth, titleHeight),
            "TOWER UPGRADE SHOP",
            shopTitleStyle);
        GUI.Label(
            new Rect(panel.x + horizontalPadding, panel.y + panelHeight * 0.17f, contentWidth, panelHeight * 0.09f),
            $"COINS: {coins}",
            shopTitleStyle);

        float buttonTop = panel.y + panelHeight * 0.31f;
        float buttonHeight = panelHeight * 0.3f;
        float buttonGap = panelWidth * 0.025f;
        float buttonWidth = (contentWidth - buttonGap) * 0.5f;
        DrawPurchaseButton(
            new Rect(panel.x + horizontalPadding, buttonTop, buttonWidth, buttonHeight),
            TowerTurret.UpgradeType.Obliterator,
            "THE OBLITERATOR",
            0);
        DrawPurchaseButton(
            new Rect(panel.x + horizontalPadding + buttonWidth + buttonGap, buttonTop, buttonWidth, buttonHeight),
            TowerTurret.UpgradeType.BazookaTurret,
            "BAZOOKA TURRET",
            1);

        Color previousBackgroundColor = GUI.backgroundColor;
        if (selectedShopOption == 2) GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
        if (GUI.Button(
            new Rect(panel.x + panel.width * 0.2f, panel.y + panelHeight * 0.69f, panel.width * 0.6f, panelHeight * 0.17f),
            selectedShopOption == 2 ? "> CONTINUE TO NEXT WAVE" : "CONTINUE TO NEXT WAVE",
            shopButtonStyle))
        {
            isBetweenWaveShopOpen = false;
        }

        GUI.backgroundColor = previousBackgroundColor;
        GUI.Label(
            new Rect(panel.x + horizontalPadding, panel.y + panelHeight * 0.89f, contentWidth, panelHeight * 0.07f),
            "Use the mouse or left/right + confirm to select an upgrade.",
            GUI.skin.label);
        GUI.color = previousColor;
    }

    private void DrawPurchaseButton(Rect buttonRect, TowerTurret.UpgradeType type, string label, int optionIndex)
    {
        int price = GetTowerUpgradePrice(type);
        int purchaseLimit = GetTowerUpgradeLimit(type);
        int purchasedCount = CountPurchasedTowerUpgrades(type);
        int availableCount = CountAvailableTowerUpgrades(type);
        int totalSlots = CountTotalTowerUpgradeSlots(type);
        TowerHealth towerHealth = TowerHealth.Instance;
        bool canPurchase = towerHealth != null
            && towerHealth.Coins >= price
            && purchasedCount < purchaseLimit
            && availableCount > 0;

        bool previousGuiEnabled = GUI.enabled;
        GUI.enabled = canPurchase;
        Color previousBackgroundColor = GUI.backgroundColor;
        if (selectedShopOption == optionIndex) GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
        if (GUI.Button(
            buttonRect,
            $"{(selectedShopOption == optionIndex ? "> " : string.Empty)}{label}\n{price} COINS\n{purchasedCount}/{Mathf.Min(purchaseLimit, totalSlots)} OWNED",
            shopButtonStyle))
        {
            TryPurchaseTowerUpgrade(type);
        }

        GUI.backgroundColor = previousBackgroundColor;
        GUI.enabled = previousGuiEnabled;
    }

    public int GetTowerUpgradePrice(TowerTurret.UpgradeType type)
    {
        return type == TowerTurret.UpgradeType.Obliterator ? obliteratorPrice : bazookaTurretPrice;
    }

    public int GetTowerUpgradeLimit(TowerTurret.UpgradeType type)
    {
        return CountTotalTowerUpgradeSlots(type);
    }

    public int CountPurchasedTowerUpgrades(TowerTurret.UpgradeType type)
    {
        int count = 0;
        foreach (TurretUpgradeSlot slot in availableTurretSlots)
        {
            if (slot.Type == type && slot.IsPurchased) count++;
        }

        return count;
    }

    private int CountTotalTowerUpgradeSlots(TowerTurret.UpgradeType type)
    {
        int count = 0;
        foreach (TurretUpgradeSlot slot in availableTurretSlots)
        {
            if (slot.Type == type) count++;
        }

        return count;
    }

    public int CountAvailableTowerUpgrades(TowerTurret.UpgradeType type)
    {
        int count = 0;
        foreach (TurretUpgradeSlot slot in availableTurretSlots)
        {
            if (slot.Type == type && !slot.IsPurchased) count++;
        }

        return count;
    }

    public bool TryPurchaseTowerUpgrade(TowerTurret.UpgradeType type)
    {
        TowerHealth towerHealth = TowerHealth.Instance;
        if (towerHealth == null)
        {
            Debug.LogError("Cannot purchase a turret because no TowerHealth instance is active.", this);
            return false;
        }

        int price = GetTowerUpgradePrice(type);
        if (towerHealth.Coins < price
            || CountPurchasedTowerUpgrades(type) >= GetTowerUpgradeLimit(type))
        {
            return false;
        }

        foreach (TurretUpgradeSlot slot in availableTurretSlots)
        {
            if (slot.Root == null || slot.Type != type || slot.IsPurchased) continue;
            if (slot.Turret != null && !HasRequiredTurretConfiguration(slot.Turret))
            {
                Debug.LogWarning(
                    $"Purchased visual slot '{slot.Root.name}', but it cannot engage aircraft until its turret configuration is completed.",
                    slot.Turret);
            }

            if (!towerHealth.TrySpendCoins(price)) return false;

            slot.IsPurchased = true;
            if (slot.Turret != null) slot.Turret.SetPurchased(true);
            slot.Root.SetActive(true);
            return true;
        }

        return false;
    }

    public Material GetPartMaterial(string partName)
    {
        switch (partName)
        {
            case "Arms": return armMaterial;
            case "Beard": return beardMaterial;
            case "Hat": return hatMaterial;
            case "Head": return headMaterial;
            case "Shirt": return shirtMaterial;
            case "Eye White": return eyeWhiteMaterial;
            case "Pupils": return pupilsMaterial;
            default: return null;
        }
    }

    public void SetPartMaterial(string partName, Material material)
    {
        switch (partName)
        {
            case "Arms": armMaterial = material; break;
            case "Beard": beardMaterial = material; break;
            case "Hat": hatMaterial = material; break;
            case "Head": headMaterial = material; break;
            case "Shirt": shirtMaterial = material; break;
            case "Eye White": eyeWhiteMaterial = material; break;
            case "Pupils": pupilsMaterial = material; break;
        }
    }

    public void DisableCustomizationLight()
    {
        if (lightToDisableAfterCustomization != null)
        {
            lightToDisableAfterCustomization.enabled = false;
        }
    }
}
