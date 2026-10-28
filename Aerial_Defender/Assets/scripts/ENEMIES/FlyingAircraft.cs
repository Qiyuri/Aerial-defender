using System.Collections;
using UnityEngine;

public class FlyingAircraft : MonoBehaviour
{
    private static Material sharedHitEffectMaterial;

    // --- Instellingen voor Unity Inspector (Rechtskantvenster) ---
    
    public float speed = 5f;           // Basis snelheid naar doel
    
    // Slinger-instellingen (Bob & Weave)
    public float bobSpeed = 2.0f;      // Hoe snel het vliegtuig omhoog/omlaag gaat
    public float weaveSpeed = 3.0f;    // Hoe snel het vliegtuig naar links/rechts gaat
    
    // Hoogte en breedte van de schommeling (hoeveel "wijd" je wilt bewegen)
    public float bobHeight = 2.0f;     // Hoe hoog omlaag/omhoog?
    public float weaveWidth = 1.5f;    // Hoe ver naar links/rechts?

    [Header("Targets")]
    [Tooltip("At Start, one non-empty target is chosen at random from this list.")]
    [SerializeField] private GameObject[] targets;
    [Tooltip("Fallback target used when the target list is empty.")]
    [SerializeField] private GameObject target; 

    [Header("Close-range flight")]
    [Tooltip("Within this distance, fly straight at the target without bobbing or weaving.")]
    [SerializeField] private float straightFlightDistance = 50f;
    [Tooltip("Distance at which bob and weave start getting stronger as the aircraft approaches.")]
    [SerializeField] private float intensityStartDistance = 500f;
    [SerializeField] private float maxMovementMultiplier = 2.5f;
    [Tooltip("Smoothly fade out bob and weave just before entering the straight-flight distance.")]
    [SerializeField] private float straightenTransitionDistance = 10f;
    [SerializeField, Min(0f)] private float weaveLeanAngle = 15f;
    [SerializeField, Min(0f)] private float weaveLeanResponse = 6f;

    [Header("Damage and falling")]
    [SerializeField] private float hitPoints = 5f;
    [SerializeField, Range(0f, 1f)] private float firstHitSpeedMultiplier = 0.65f;
    [SerializeField] private float fallingRotationSpeed = 45f;
    [SerializeField] private float bazookaKnockbackForce = 12f;
    [SerializeField] private float despawnDelayAfterImpact = 20f;
    [SerializeField] private ParticleSystem hitEffectPrefab;
    [SerializeField] private Transform firstHitEffectTransform;
    [SerializeField] private Transform secondHitEffectTransform;
    [SerializeField] private Vector3 firstHitEffectLocalPosition = new Vector3(-2f, 0f, 0f);
    [SerializeField] private Vector3 secondHitEffectLocalPosition = new Vector3(2f, 0f, 0f);
    [SerializeField, Min(0.05f)] private float hitEffectSpawnInterval = 0.5f;

    [Header("Engine sound")]
    [SerializeField] private AudioClip engineSound;
    [SerializeField] private AudioSource engineAudio;
    [SerializeField] private float pitchAtTarget = 0.6f;
    [SerializeField] private float pitchAtDistance = 1f;
    [SerializeField] private float pitchDistance = 500f;

    [Header("Model orientation")]
    [Tooltip("Local axis that points out of the airplane nose. This model points along local Y.")]
    [SerializeField] private Vector3 modelForwardAxis = Vector3.up;
    [Tooltip("Extra local rotation for aligning the model. The nose remains aimed at the target.")]
    [SerializeField] private Vector3 modelRotationOffset;

    // --- Private variabelen ---
    
    private Rigidbody rb;
    private Vector3 currentVelocity;
    private float currentHitPoints;
    private float speedMultiplier = 1f;
    private bool isFalling;
    private bool despawnScheduled;
    private bool hitEffectCoroutineRunning;
    private int hitEffectPointMask;
    private bool healthInitialized;
    private bool hasWaveTargetOverride;
    private float currentWeaveLeanAngle;

    public bool IsVisibleOnRadar => isActiveAndEnabled && currentHitPoints > 0f;
    public bool IsDefeated => !isActiveAndEnabled || (healthInitialized && currentHitPoints <= 0f);

    void Start()
    {
        // Koppel Rigidbody
        rb = GetComponent<Rigidbody>();
        currentHitPoints = Mathf.Max(0f, hitPoints);
        healthInitialized = true;

        // Uitzetten zwaartekracht en rotatie-vriest (belangrijk voor vliegtuig)
        if (rb != null)
        {
            rb.useGravity = false;
            rb.freezeRotation = true; 
            rb.linearVelocity = Vector3.zero; // Reset velocity
        }

        SelectRandomTarget();
        SetupEngineSound();
    }

    public void InitializeWaveSpawn(Transform waveTarget, float speedMultiplier)
    {
        if (waveTarget == null)
        {
            Debug.LogError("Cannot initialize a wave aircraft without a target.", this);
            return;
        }

        target = waveTarget.gameObject;
        hasWaveTargetOverride = true;
        speed *= Mathf.Max(0f, speedMultiplier);
    }

    private void SelectRandomTarget()
    {
        if (hasWaveTargetOverride) return;

        int validTargetCount = 0;
        if (targets != null)
        {
            foreach (GameObject candidate in targets)
            {
                if (candidate != null) validTargetCount++;
            }
        }

        if (validTargetCount == 0)
        {
            if (target == null) Debug.LogError("FlyingAircraft has no valid target assigned.", this);
            return;
        }

        int selectedIndex = Random.Range(0, validTargetCount);
        foreach (GameObject candidate in targets)
        {
            if (candidate == null) continue;
            if (selectedIndex == 0)
            {
                target = candidate;
                return;
            }

            selectedIndex--;
        }
    }

    private void SetupEngineSound()
    {
        if (engineSound == null)
        {
            Debug.LogWarning("FlyingAircraft has no engine sound assigned.", this);
            return;
        }

        if (engineAudio == null) engineAudio = GetComponent<AudioSource>();
        if (engineAudio == null) engineAudio = gameObject.AddComponent<AudioSource>();

        engineAudio.clip = engineSound;
        engineAudio.loop = true;
        engineAudio.playOnAwake = false;
        engineAudio.Play();
    }

    void Update()
    {
        if (isFalling)
        {
            Quaternion fallingRotation = GetModelLookRotation(Vector3.down, Vector3.forward);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                fallingRotation,
                fallingRotationSpeed * Time.deltaTime);
            return;
        }

        if (target == null)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        Vector3 targetPosition = target.transform.position;
        Vector3 toTarget = targetPosition - transform.position;
        float distance = toTarget.magnitude;
        Vector3 directionToTarget = distance > 0.001f ? toTarget / distance : transform.forward;
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(directionToTarget, Vector3.up).normalized;
        if (horizontalDirection.sqrMagnitude < 0.001f)
        {
            horizontalDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (horizontalDirection.sqrMagnitude < 0.001f) horizontalDirection = Vector3.forward;
        }

        Vector3 right = Vector3.Cross(Vector3.up, horizontalDirection).normalized;
        Vector3 referenceUp = Mathf.Abs(Vector3.Dot(directionToTarget, Vector3.up)) > 0.99f
            ? Vector3.forward
            : Vector3.up;
        UpdateEnginePitch(distance);

        if (distance <= 2.5f)
        {
            currentVelocity = Vector3.zero;
            currentWeaveLeanAngle = Mathf.Lerp(currentWeaveLeanAngle, 0f, 1f - Mathf.Exp(-weaveLeanResponse * Time.deltaTime));
        }
        else if (distance <= straightFlightDistance)
        {
            currentVelocity = directionToTarget * speed * speedMultiplier;
            currentWeaveLeanAngle = Mathf.Lerp(currentWeaveLeanAngle, 0f, 1f - Mathf.Exp(-weaveLeanResponse * Time.deltaTime));
        }
        else
        {
            float rampEndDistance = straightFlightDistance + Mathf.Max(0f, straightenTransitionDistance);
            float intensity = Mathf.Lerp(
                1f,
                Mathf.Max(1f, maxMovementMultiplier),
                Mathf.InverseLerp(intensityStartDistance, rampEndDistance, distance));
            float straightening = straightenTransitionDistance > 0f
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(straightFlightDistance, rampEndDistance, distance))
                : 1f;
            float movementMultiplier = 1f + (intensity - 1f) * straightening;

            Vector3 weaveVelocity = right * Mathf.Cos(Time.time * weaveSpeed) * weaveWidth * weaveSpeed * movementMultiplier;
            Vector3 bobVelocity = Vector3.up * Mathf.Cos(Time.time * bobSpeed) * bobHeight * bobSpeed * movementMultiplier;
            currentVelocity = (directionToTarget * speed + weaveVelocity + bobVelocity) * speedMultiplier;
            float leanFade = straightening * Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(straightFlightDistance, intensityStartDistance, distance));
            float targetLeanAngle = -Mathf.Sin(Time.time * weaveSpeed) * weaveLeanAngle * leanFade;
            currentWeaveLeanAngle = Mathf.Lerp(
                currentWeaveLeanAngle,
                targetLeanAngle,
                1f - Mathf.Exp(-weaveLeanResponse * Time.deltaTime));
        }

        Quaternion lookRotation = GetModelLookRotation(directionToTarget, referenceUp);
        lookRotation = Quaternion.AngleAxis(currentWeaveLeanAngle, directionToTarget) * lookRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
    }

    private Quaternion GetModelLookRotation(Vector3 forwardDirection, Vector3 referenceUp)
    {
        Vector3 noseAxis = modelForwardAxis.sqrMagnitude > 0.001f ? modelForwardAxis.normalized : Vector3.up;
        Quaternion offset = Quaternion.Euler(modelRotationOffset);
        Vector3 offsetNoseAxis = offset * noseAxis;
        Quaternion modelAlignment = Quaternion.FromToRotation(offsetNoseAxis, Vector3.forward) * offset;
        return Quaternion.LookRotation(forwardDirection, referenceUp) * modelAlignment;
    }

    private void UpdateEnginePitch(float distance)
    {
        if (engineAudio == null || engineSound == null) return;

        float distanceFactor = pitchDistance > 0f
            ? Mathf.Clamp01(distance / pitchDistance)
            : 0f;
        engineAudio.pitch = Mathf.Lerp(pitchAtTarget, pitchAtDistance, distanceFactor);
    }

    void FixedUpdate()
    {
        if (rb != null && !isFalling) rb.linearVelocity = currentVelocity;
    }

    public void TakeDamage(float damage)
    {
        if (isFalling || damage <= 0f) return;

        currentHitPoints = Mathf.Max(0f, currentHitPoints - damage);
        StartRepeatingHitEffects(1 << Random.Range(0, 2));
        if (currentHitPoints <= 0f)
        {
            StartFalling();
            return;
        }

        speedMultiplier = firstHitSpeedMultiplier;
    }

    public void TakeBazookaHit(float damage, Vector3 projectileDirection)
    {
        if (damage > 0f)
        {
            currentHitPoints = Mathf.Max(0f, currentHitPoints - damage);
        }

        StartRepeatingHitEffects(0b11);
        StartFalling();

        if (rb != null && projectileDirection.sqrMagnitude > 0.0001f)
        {
            rb.AddForce(projectileDirection.normalized * bazookaKnockbackForce, ForceMode.Impulse);
        }
    }

    private void StartRepeatingHitEffects(int pointMask)
    {
        hitEffectPointMask |= pointMask;
        if (hitEffectCoroutineRunning) return;

        hitEffectCoroutineRunning = true;
        StartCoroutine(SpawnHitEffectsRepeatedly());
    }

    private IEnumerator SpawnHitEffectsRepeatedly()
    {
        while (true)
        {
            for (int pointIndex = 0; pointIndex < 2; pointIndex++)
            {
                if ((hitEffectPointMask & (1 << pointIndex)) != 0)
                {
                    SpawnHitEffect(pointIndex);
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, hitEffectSpawnInterval));
        }
    }

    private void SpawnHitEffect(int pointIndex)
    {
        Transform effectTransform = pointIndex == 0 ? firstHitEffectTransform : secondHitEffectTransform;
        Vector3 localPosition = pointIndex == 0 ? firstHitEffectLocalPosition : secondHitEffectLocalPosition;
        Vector3 worldPosition = effectTransform != null
            ? effectTransform.position
            : transform.TransformPoint(localPosition);
        Quaternion worldRotation = effectTransform != null ? effectTransform.rotation : transform.rotation;
        ParticleSystem effect;
        if (hitEffectPrefab != null)
        {
            effect = Instantiate(hitEffectPrefab, worldPosition, worldRotation);
        }
        else
        {
            effect = CreateDefaultHitEffect(worldPosition);
        }

        ParticleSystem.MainModule main = effect.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        effect.Play(true);
    }

    private static ParticleSystem CreateDefaultHitEffect(Vector3 position)
    {
        GameObject effectObject = new GameObject("Aircraft hit effect");
        effectObject.transform.position = position;

        ParticleSystem effect = effectObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = effect.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.48f, 0.08f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = 24;

        ParticleSystem.EmissionModule emission = effect.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = effect.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        if (sharedHitEffectMaterial == null)
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader != null) sharedHitEffectMaterial = new Material(particleShader);
        }

        if (sharedHitEffectMaterial != null)
        {
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = sharedHitEffectMaterial;
        }

        effect.Emit(12);
        return effect;
    }

    private void StartFalling()
    {
        if (isFalling) return;

        isFalling = true;
        currentVelocity = Vector3.zero;

        if (engineAudio != null && engineAudio.isPlaying)
        {
            engineAudio.Stop();
        }

        if (rb != null)
        {
            rb.useGravity = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isFalling || despawnScheduled) return;
        if (collision.collider != null && collision.collider.GetComponentInParent<BazookaProjectile>() != null) return;

        despawnScheduled = true;
        Destroy(gameObject, despawnDelayAfterImpact);
    }
}
