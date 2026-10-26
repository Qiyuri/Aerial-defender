using UnityEngine;

public class FirstPersonController : MonoBehaviour
{
    // --- Instellingen die je kunt wijzigen in Unity (Inspectoren) ---
    
    public float moveSpeed = 10f;             // Hoe snel lopen (kracht per seconde)
    public float rotationXAxis = -2.5f;        // Hoe snel draaien met muis (X as)
    public float rotationYAxis = 2.5f;         // Hoe snel draaien met muis (Y as)
    
    // --- Private variabelen (niet te wijzigen in Unity) ---
    
    private Rigidbody rb;
    private Vector3 movementVector;
    private Camera cam;

    void Start()
    {
        // Zoek het Rigidbody component van dit object
        rb = GetComponent<Rigidbody>();
        
        // Zoek de Camera. Vaak staat deze direct op het "Main Camera" object, 
        // maar we zoeken hem hier via naam "MainCamera" als hij op hetzelfde object zit,
        // of via transform.GetComponent<Camera> als hij ergens anders is.
        // Voor een standaard First Person setup zoek je meestal de camera op dit object zelf:
        cam = GetComponentInChildren<Camera>(); 
    }

    void Update()
    {
        // 1. Lees toetsenbord input (Horizontal en Vertical)
        float xInput = Input.GetAxis("Horizontal");
        float zInput = Input.GetAxis("Vertical");

        // Bouw de bewegingsrichting op basis van hoe de Camera naar voren kijkt.
        // We gebruiken transform.right voor links/rechts en transform.forward voor voor/achter.
        movementVector = new Vector3(xInput, 0f, zInput);

        // --- CAMERABEWEGING (Muis) ---
        
        // Rotatie op Y-as (Linker-Rechter draai)
        float mouseX = Input.GetAxis("Mouse X") * rotationYAxis;
        
        // Rotatie op X-as (Omlaap-Omhoog draai). 
        // We gebruiken Quaternion.LookRotation of simple Rotate. Hierbij letten we op dat de Y-rotatie niet onbeperkt is in sommige games, 
        // maar voor een simpel script doen we gewoon:
        float mouseY = Input.GetAxis("Mouse Y") * rotationXAxis;

        transform.Rotate(0, mouseX, 0);     // Draai alleen om de verticale as (Y)
        
        // Opmerking: In Unity is het vaak handiger om de rotatie van de Camera zelf te draaien 
        // in plaats van het hele speler-object, zodat je hoofd niet mee draait als je neerbuigt.
        // Als dat zo werkt, kun je hieronder ook draaien:
        // if (cam != null) { cam.transform.Rotate(0, 0, mouseY * rotationXAxis); } 
        // Maar voor nu roteren we het hele object om Y en laten de camera vrij rotatie in X/Y doen via Quaternion.

        // Correcte aanpak voor FPS: Draai speler op Y, draai Camera zelf op X en Z.
        if (cam != null)
        {
            transform.Rotate(0, mouseX * rotationYAxis, 0); // Speler Y rotatie
            
            // Camera X/Y rotatie apart regelen zodat je kunt kijken omhoog/omlaag zonder dat je karakter kantelt
            cam.transform.localRotation = Quaternion.Euler(Input.GetAxis("Mouse Y") * -rotationXAxis, Input.GetAxis("Mouse X") * rotationYAxis, 0);
        }

        // --- Bewegen voorwaarts naar de camera-richting ---
        movementVector *= Time.deltaTime; // Zorg dat dit frame-rate proof is

        // Voeg de kracht toe aan het Rigidbody in FixedUpdate (zie hieronder)
    }
}