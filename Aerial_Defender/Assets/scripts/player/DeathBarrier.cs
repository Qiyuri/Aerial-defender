using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DeathBarrier : MonoBehaviour
{
    private void Awake()
    {
        Collider barrierCollider = GetComponent<Collider>();
        barrierCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Movement playerMovement = other.GetComponentInParent<Movement>();
        if (playerMovement != null)
        {
            playerMovement.RespawnAtStart();
        }
    }
}
