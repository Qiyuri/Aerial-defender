using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera & FOV Settings")]
    public float minFov = 60f;
    public float maxFov = 90f;
    public float fovSensitivity = 1f;
    
    // Verwijderd: Rigidbody component en bewegingsvariabelen
    
    private Camera playerCamera;

    void Start()
    {
        // Probeer de Camera te vinden. Als deze niet bestaat, creëer er één op dit object.
        if (playerCamera == null)
        {
            playerCamera = GetComponent<Camera>();
            if (playerCamera == null)
            {
                Debug.LogError("Missing Camera! Please add a Camera component to this object.");
            }
            else
            {
                playerCamera.fieldOfView = minFov;
            }
        }
        
        // Zorg dat de rotatie niet verandert door andere krachten (hoewel er geen Rigidbody meer is)
        rigidbodyMode();
    }

    void Update()
    {
        // Verwijderd: Check voor Rigidbody null
            
        if (playerCamera == null) return;

        // Links naar Rechts kijken (Yaw)
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(0f, mouseX * 2f, 0f); // Draai het hoofd object
        
        // Naar Boven naar Onder kijken (Pitch - Camera zelf draaien)
        float mouseY = Input.GetAxis("Mouse Y");
        playerCamera.transform.Rotate(0f, -mouseY * 2f, 0f);

        // Limiteer hoe ver je kunt kijken om over je hoofd heen te voorkomen
        Vector3 camPos = playerCamera.transform.localPosition;
        camPos.y = Mathf.Clamp(camPos.y, -90f, 90f);
        playerCamera.transform.localPosition = camPos;

        // Verander de zoom (FOV) met de muiswiel
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f && playerCamera != null)
        {
            float targetFov = playerCamera.fieldOfView - (scroll * fovSensitivity);
            playerCamera.fieldOfView = Mathf.Clamp(targetFov, minFov, maxFov);
        }

        // Verwijderd: Alle bewegingslogica (transform.right/forward en Rigidbody.linearVelocity)
    }
    
    // Helper method om te simuleren dat rotatie bevroren is als er ooit een Rigidbody was
    private void rigidbodyMode() 
    {
        // Geen Rigidbody nodig voor pure camera rotatie, dus dit kan leeg blijven of worden verwijderd.
    }
}

