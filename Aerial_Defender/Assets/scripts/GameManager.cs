using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
