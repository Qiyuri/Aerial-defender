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

    // De gameobject waarheen we gaan
    [SerializeField] private GameObject target; 

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

        if (target == null) Debug.Log("Target object not found!");
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

        if (distance > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(directionToTarget, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
        }

        if (distance < 2.5f)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        Vector3 right = Vector3.Cross(Vector3.up, directionToTarget).normalized;
        if (right.sqrMagnitude < 0.001f) right = transform.right;
        Vector3 up = Vector3.Cross(directionToTarget, right).normalized;

        Vector3 weaveVelocity = right * Mathf.Cos(Time.time * weaveSpeed) * weaveWidth * weaveSpeed;
        Vector3 bobVelocity = up * Mathf.Cos(Time.time * bobSpeed) * bobHeight * bobSpeed;
        currentVelocity = directionToTarget * speed + weaveVelocity + bobVelocity;
    }

    void FixedUpdate()
    {
        if (rb != null) rb.linearVelocity = currentVelocity;
    }
}