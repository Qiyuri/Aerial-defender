using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Movement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeedMultiplier = 1.75f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField, Min(0f)] private float respawnDebounceSeconds = 1f;

    [Header("Ground check")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;

    [Header("Weapon slots")]
    [SerializeField] private GameObject unarmedObject;
    [SerializeField] private GameObject sniperWeapon;
    [SerializeField] private GameObject bazookaWeapon;
    [SerializeField, Range(1f, 60f)] private float sniperAimFieldOfView = 8f;
    [SerializeField, Range(1f, 60f)] private float bazookaAimFieldOfView = 28f;
    [SerializeField, Range(0.01f, 1f)] private float sniperAimSensitivityMultiplier = 0.25f;
    [SerializeField, Range(0.01f, 1f)] private float bazookaAimSensitivityMultiplier = 0.5f;

    [Header("Weapon firing")]
    [SerializeField] private Transform sniperMuzzle;
    [SerializeField] private Transform bazookaMuzzle;
    [SerializeField] private GameObject bazookaProjectilePrefab;
    [SerializeField] private ParticleSystem weaponMuzzleFlashPrefab;
    [SerializeField] private float bazookaProjectileSpeed = 80f;
    [SerializeField] private float bazookaProjectileLifetime = 8f;
    [SerializeField] private float bazookaFireInterval = 0.8f;
    [SerializeField] private float bazookaMuzzleForwardOffset = 0.35f;
    [SerializeField] private float bazookaDamage = 5f;
    [SerializeField, Min(0f)] private float initialHitboxIgnoreDistance = 10f;
    [SerializeField] private ParticleSystem bazookaSmokeTrailPrefab;
    [SerializeField] private ParticleSystem bazookaImpactEffectPrefab;
    [Header("Bazooka auto lock")]
    [SerializeField, Min(0f)] private float bazookaTargetSearchDistance = 50f;
    [SerializeField, Range(0f, 45f)] private float bazookaTargetLockAngle = 5f;
    [SerializeField, Min(0f)] private float bazookaTargetTurnSpeed = 180f;
    [SerializeField] private float sniperProjectileSpeed = 250f;
    [SerializeField] private float sniperRange = 500f;
    [SerializeField] private float sniperFireInterval = 0.6f;
    [SerializeField] private float sniperDamage = 2.5f;

    private Rigidbody rb;
    private Collider playerCollider;
    private CameraController cameraController;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Vector2 moveInput;
    private bool jumpRequested;
    private bool isGrounded;
    private bool isSprinting;
    private int selectedWeaponSlot;
    private float nextFireTime;
    private float nextAllowedRespawnTime;
    private Gamepad assignedGamepad;
    private bool hasAssignedGamepad;
    private int playerIndex;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private readonly RaycastHit[] sniperHits = new RaycastHit[32];

    public int PlayerIndex => playerIndex;
    public Gamepad InputGamepad => hasAssignedGamepad ? assignedGamepad : Gamepad.current;

    public void SetPlayerInput(int index, Gamepad gamepad)
    {
        playerIndex = index;
        assignedGamepad = gamepad;
        hasAssignedGamepad = true;
        if (cameraController != null)
        {
            cameraController.SetPlayerInput(index, gamepad);
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        spawnPosition = rb.position;
        spawnRotation = rb.rotation;
        rb.freezeRotation = true;
        cameraController = GetComponentInChildren<CameraController>();
        if (cameraController != null && hasAssignedGamepad)
        {
            cameraController.SetPlayerInput(playerIndex, assignedGamepad);
        }
        if (unarmedObject == null)
        {
            unarmedObject = FindChildGameObject("Unarmed", false);
        }
        if (sniperWeapon == null)
        {
            sniperWeapon = FindChildGameObject("Sniper", false);
        }
        if (bazookaWeapon == null)
        {
            bazookaWeapon = FindChildGameObject("Bazooka", false);
        }
        if (bazookaMuzzle == null && bazookaWeapon != null)
        {
            bazookaMuzzle = FindDescendant(bazookaWeapon.transform, "Rocket");
        }
        if (sniperMuzzle == null && sniperWeapon != null)
        {
            sniperMuzzle = FindFirstDescendant(sniperWeapon.transform, "Muzzle", "Barrel", "Tip");
        }
        SelectWeaponSlot(0);

    }

    private void Update()
    {
        HandleWeaponSelection();
        HandleWeaponFiring();

        Keyboard keyboard = playerIndex == 0 ? Keyboard.current : null;
        Gamepad gamepad = InputGamepad;
        moveInput = Vector2.zero;
        if (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed))
        {
            moveInput.y += 1f;
        }
        if (keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed))
        {
            moveInput.y -= 1f;
        }
        if (keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed))
        {
            moveInput.x += 1f;
        }
        if (keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed))
        {
            moveInput.x -= 1f;
        }

        if (gamepad != null)
        {
            moveInput += gamepad.leftStick.ReadValue();
        }

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        isSprinting = (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            || (gamepad != null && gamepad.leftStickButton.isPressed && moveInput.sqrMagnitude > 0.001f);

        if (cameraController != null)
        {
            cameraController.SetMovementLean(moveInput.x, selectedWeaponSlot != 0);
        }

        if ((keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
        {
            jumpRequested = true;
        }
    }

    private void HandleWeaponFiring()
    {
        Mouse mouse = playerIndex == 0 ? Mouse.current : null;
        Gamepad gamepad = InputGamepad;
        bool mouseFired = mouse != null && mouse.leftButton.wasPressedThisFrame;
        bool controllerFired = gamepad != null && gamepad.rightTrigger.isPressed;
        if ((!mouseFired && !controllerFired) || Time.time < nextFireTime) return;
        if (cameraController == null) return;

        if (selectedWeaponSlot == 1)
        {
            nextFireTime = Time.time + sniperFireInterval;
            StartCoroutine(FireSniperShot());
        }
        else if (selectedWeaponSlot == 2)
        {
            if (bazookaProjectilePrefab == null)
            {
                Debug.LogError("Assign the bazooka projectile prefab in the Movement inspector.", this);
                return;
            }

            nextFireTime = Time.time + bazookaFireInterval;
            FireBazooka();
        }
    }

    private IEnumerator FireSniperShot()
    {
        Ray aimRay = cameraController.GetAimRay();
        Vector3 startPosition = sniperMuzzle != null
            ? sniperMuzzle.position
            : sniperWeapon != null
                ? sniperWeapon.transform.position
                : aimRay.origin + cameraController.transform.right * 0.25f;
        Vector3 aimPoint = GetSniperAimPoint(aimRay);
        Vector3 toAimPoint = aimPoint - startPosition;
        float shotRange = Mathf.Min(sniperRange, toAimPoint.magnitude);
        Vector3 direction = toAimPoint.sqrMagnitude > 0.0001f
            ? toAimPoint.normalized
            : aimRay.direction.normalized;
        Vector3 currentPosition = startPosition;
        float distanceTravelled = 0f;
        LineRenderer tracer = CreateSniperTracer(startPosition);
        SpawnWeaponMuzzleFlash(startPosition, direction);

        while (distanceTravelled < shotRange)
        {
            yield return new WaitForFixedUpdate();

            float stepDistance = Mathf.Min(Mathf.Max(1f, sniperProjectileSpeed) * Time.fixedDeltaTime, shotRange - distanceTravelled);
            int hitCount = Physics.RaycastNonAlloc(
                currentPosition,
                direction,
                sniperHits,
                stepDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            RaycastHit nearestHit = default;
            bool hitTarget = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = sniperHits[i];
                if (distanceTravelled + candidate.distance < initialHitboxIgnoreDistance) continue;
                if (IsPlayerCollider(candidate.collider)) continue;
                if (hitTarget && candidate.distance >= nearestHit.distance) continue;

                nearestHit = candidate;
                hitTarget = true;
            }

            if (hitTarget)
            {
                currentPosition = nearestHit.point;
                if (tracer != null) tracer.SetPosition(1, currentPosition);
                FlyingAircraft aircraft = nearestHit.collider.GetComponentInParent<FlyingAircraft>();
                if (aircraft != null)
                {
                    aircraft.TakeDamage(sniperDamage);
                }
                else
                {
                    nearestHit.collider.SendMessageUpwards("TakeDamage", sniperDamage, SendMessageOptions.DontRequireReceiver);
                }
                break;
            }

            currentPosition += direction * stepDistance;
            distanceTravelled += stepDistance;
            if (tracer != null) tracer.SetPosition(1, currentPosition);
        }

        if (tracer != null)
        {
            Destroy(tracer.gameObject, 0.05f);
        }
    }

    private void FireBazooka()
    {
        Ray aimRay = cameraController.GetAimRay();
        Vector3 direction = aimRay.direction.normalized;
        Vector3 spawnPosition = bazookaMuzzle != null
            ? bazookaMuzzle.position + direction * bazookaMuzzleForwardOffset
            : (bazookaWeapon != null ? bazookaWeapon.transform.position : transform.position) + direction;
        GameObject projectileObject = Instantiate(
            bazookaProjectilePrefab,
            spawnPosition,
            Quaternion.LookRotation(direction));
        Physics.SyncTransforms();
        BazookaProjectile projectile = projectileObject.GetComponent<BazookaProjectile>();
        if (projectile == null) projectile = projectileObject.AddComponent<BazookaProjectile>();
        projectile.Launch(
            direction,
            bazookaProjectileSpeed,
            bazookaProjectileLifetime,
            GetComponentsInChildren<Collider>(),
            bazookaDamage,
            bazookaSmokeTrailPrefab,
            bazookaImpactEffectPrefab,
            bazookaTargetSearchDistance,
            bazookaTargetLockAngle,
            bazookaTargetTurnSpeed,
            initialHitboxIgnoreDistance);
        SpawnWeaponMuzzleFlash(spawnPosition, direction);
    }

    private Vector3 GetSniperAimPoint(Ray aimRay)
    {
        int hitCount = Physics.RaycastNonAlloc(
            aimRay,
            sniperHits,
            sniperRange,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        RaycastHit nearestHit = default;
        bool hitTarget = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = sniperHits[i];
            if (candidate.distance < initialHitboxIgnoreDistance) continue;
            if (IsPlayerCollider(candidate.collider)) continue;
            if (hitTarget && candidate.distance >= nearestHit.distance) continue;

            nearestHit = candidate;
            hitTarget = true;
        }

        return hitTarget ? nearestHit.point : aimRay.origin + aimRay.direction * sniperRange;
    }

    private void SpawnWeaponMuzzleFlash(Vector3 position, Vector3 direction)
    {
        Quaternion rotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction)
            : Quaternion.identity;
        ParticleSystem muzzleFlash = weaponMuzzleFlashPrefab != null
            ? Instantiate(weaponMuzzleFlashPrefab, position, rotation)
            : CreateDefaultMuzzleFlash(position, rotation);

        ParticleSystem.MainModule main = muzzleFlash.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        muzzleFlash.Play(true);
    }

    private static ParticleSystem CreateDefaultMuzzleFlash(Vector3 position, Quaternion rotation)
    {
        GameObject flashObject = new GameObject("Weapon muzzle flash");
        flashObject.transform.SetPositionAndRotation(position, rotation);

        ParticleSystem flash = flashObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = flash.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.18f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = 24;

        ParticleSystem.EmissionModule emission = flash.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = flash.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 24f;
        shape.radius = 0.025f;

        flash.Emit(12);
        return flash;
    }

    private LineRenderer CreateSniperTracer(Vector3 startPosition)
    {
        GameObject tracerObject = new GameObject("Sniper tracer");
        LineRenderer tracer = tracerObject.AddComponent<LineRenderer>();
        tracer.positionCount = 2;
        tracer.useWorldSpace = true;
        tracer.startWidth = 0.025f;
        tracer.endWidth = 0.01f;
        tracer.SetPosition(0, startPosition);
        tracer.SetPosition(1, startPosition);

        Shader tracerShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (tracerShader == null) tracerShader = Shader.Find("Sprites/Default");
        if (tracerShader != null)
        {
            Material tracerMaterial = new Material(tracerShader);
            tracerMaterial.color = Color.white;
            tracer.material = tracerMaterial;
            Destroy(tracerMaterial, sniperRange / Mathf.Max(1f, sniperProjectileSpeed) + 1f);
        }

        Destroy(tracerObject, sniperRange / Mathf.Max(1f, sniperProjectileSpeed) + 1f);
        return tracer;
    }

    private bool IsPlayerCollider(Collider collider)
    {
        return collider != null && (collider.transform == transform || collider.transform.IsChildOf(transform));
    }

    private static Transform FindDescendant(Transform parent, string objectName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child != parent && child.name == objectName) return child;
        }

        return null;
    }

    private static Transform FindFirstDescendant(Transform parent, params string[] objectNames)
    {
        foreach (string objectName in objectNames)
        {
            Transform match = FindDescendant(parent, objectName);
            if (match != null) return match;
        }

        return null;
    }

    private void HandleWeaponSelection()
    {
        Mouse mouse = playerIndex == 0 ? Mouse.current : null;
        Gamepad gamepad = InputGamepad;

        if (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame)
        {
            SelectWeaponSlot((selectedWeaponSlot + 1) % 3);
        }
        else if (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame)
        {
            SelectWeaponSlot((selectedWeaponSlot + 2) % 3);
        }

        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll > 0f)
        {
            SelectWeaponSlot((selectedWeaponSlot + 1) % 3);
        }
        else if (scroll < 0f)
        {
            SelectWeaponSlot((selectedWeaponSlot + 2) % 3);
        }
    }

    private void SelectWeaponSlot(int slot)
    {
        selectedWeaponSlot = slot;

        if (unarmedObject != null)
        {
            unarmedObject.SetActive(slot == 0);
        }

        if (sniperWeapon != null)
        {
            sniperWeapon.SetActive(slot == 1);
        }

        if (bazookaWeapon != null)
        {
            bazookaWeapon.SetActive(slot == 2);
        }

        if (cameraController != null)
        {
            if (slot == 1)
            {
                cameraController.SetAimFieldOfView(sniperAimFieldOfView);
                cameraController.SetAimSensitivityMultiplier(sniperAimSensitivityMultiplier);
            }
            else if (slot == 2)
            {
                cameraController.SetAimFieldOfView(bazookaAimFieldOfView);
                cameraController.SetAimSensitivityMultiplier(bazookaAimSensitivityMultiplier);
            }
            else
            {
                cameraController.RestoreDefaultAimFieldOfView();
            }
        }
    }

    private GameObject FindChildGameObject(string objectName, bool logIfMissing = true)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && child.name == objectName)
            {
                return child.gameObject;
            }
        }

        if (logIfMissing)
        {
            Debug.LogWarning("Weapon slot could not find a child named '" + objectName + "'. Assign it in the Movement inspector.", this);
        }

        return null;
    }

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();

        if (moveInput.sqrMagnitude > 0.001f && cameraController != null)
        {
            cameraController.AlignBodyToLookDirection(rb, Time.fixedDeltaTime);
        }

        Vector3 velocity = rb.linearVelocity;
        Vector3 movement = cameraController != null
            ? cameraController.GetLookRelativeMovement(moveInput)
            : transform.right * moveInput.x + transform.forward * moveInput.y;
        movement = Vector3.ClampMagnitude(movement, 1f);
        float currentMoveSpeed = moveSpeed * (isSprinting ? sprintSpeedMultiplier : 1f);
        velocity.x = movement.x * currentMoveSpeed;
        velocity.z = movement.z * currentMoveSpeed;

        if (jumpRequested && isGrounded)
        {
            velocity.y = jumpForce;
        }

        rb.linearVelocity = velocity;
        jumpRequested = false;
    }

    private bool CheckGrounded()
    {
        if (playerCollider == null) return false;

        Bounds bounds = playerCollider.bounds;
        Vector3 rayOrigin = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
        float rayDistance = groundCheckDistance + 0.05f;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            groundHits,
            rayDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = groundHits[i].collider;
            if (hitCollider != null && hitCollider.transform != transform && !hitCollider.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    public void RespawnAtStart()
    {
        if (Time.time < nextAllowedRespawnTime) return;
        nextAllowedRespawnTime = Time.time + respawnDebounceSeconds;

        rb.position = spawnPosition;
        rb.rotation = spawnRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        moveInput = Vector2.zero;
        jumpRequested = false;
        isGrounded = false;

        if (cameraController != null)
        {
            cameraController.ResetLookDirection();
        }

        Physics.SyncTransforms();
    }

}
