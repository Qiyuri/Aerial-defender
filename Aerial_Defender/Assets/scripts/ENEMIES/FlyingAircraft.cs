using UnityEngine;

public class FlyingAircraft : MonoBehaviour
{
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

    void Start()
    {
        // Koppel Rigidbody
        rb = GetComponent<Rigidbody>();

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

    private void SelectRandomTarget()
    {
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
        if (target == null)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        Vector3 targetPosition = target.transform.position;
        Vector3 toTarget = targetPosition - transform.position;
        float distance = toTarget.magnitude;
        Vector3 directionToTarget = distance > 0.001f ? toTarget / distance : transform.forward;
        Vector3 referenceUp = Mathf.Abs(Vector3.Dot(directionToTarget, Vector3.up)) > 0.99f
            ? Vector3.forward
            : Vector3.up;
        Vector3 right = Vector3.Cross(referenceUp, directionToTarget).normalized;
        Vector3 up = Vector3.Cross(directionToTarget, right).normalized;
        UpdateEnginePitch(distance);

        if (distance <= 2.5f)
        {
            currentVelocity = Vector3.zero;
        }
        else if (distance <= straightFlightDistance)
        {
            currentVelocity = directionToTarget * speed;
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
            Vector3 bobVelocity = up * Mathf.Cos(Time.time * bobSpeed) * bobHeight * bobSpeed * movementMultiplier;
            currentVelocity = directionToTarget * speed + weaveVelocity + bobVelocity;
        }

        Vector3 noseAxis = modelForwardAxis.sqrMagnitude > 0.001f ? modelForwardAxis.normalized : Vector3.up;
        Quaternion offset = Quaternion.Euler(modelRotationOffset);
        Vector3 offsetNoseAxis = offset * noseAxis;
        Quaternion modelAlignment = Quaternion.FromToRotation(offsetNoseAxis, Vector3.forward) * offset;
        Quaternion lookRotation = Quaternion.LookRotation(directionToTarget, up)
            * modelAlignment;
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
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
        if (rb != null) rb.linearVelocity = currentVelocity;
    }
}
