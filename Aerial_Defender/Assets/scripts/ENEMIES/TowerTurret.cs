using UnityEngine;

[DisallowMultipleComponent]
public class TowerTurret : MonoBehaviour
{
    [System.Serializable]
    public sealed class Settings
    {
        [Header("Targeting")]
        [Min(0f)] public float targetRange = 200f;
        [Min(0f)] public float aimSpeed = 180f;

        [Header("Obliterator")]
        [Min(0.01f)] public float bulletsPerSecond = 30f;
        [Min(0.01f)] public float firingDuration = 6f;
        [Min(0f)] public float obliteratorCooldown = 20f;
        [Min(0f)] public float bulletDamage = 0.1f;
        [Min(0.01f)] public float tracerDuration = 0.08f;
        [Min(0f)] public float tracerWidth = 0.035f;
        public ParticleSystem obliteratorCooldownEffectPrefab;

        [Header("Bazooka turret")]
        public GameObject bazookaProjectilePrefab;
        [Min(0f)] public float bazookaProjectileDamage = 5f;
        [Min(0f)] public float bazookaProjectileSpeed = 80f;
        [Min(0.01f)] public float bazookaProjectileLifetime = 8f;
        [Min(0f)] public float bazookaTimeBetweenShots = 1f;
        [Min(1)] public int bazookaShotsPerBurst = 3;
        [Min(0f)] public float bazookaCooldown = 30f;
        [Min(0f)] public float initialHitboxIgnoreDistance = 10f;
        public ParticleSystem bazookaSmokeTrailPrefab;
        public ParticleSystem bazookaImpactEffectPrefab;
        public ParticleSystem bazookaCooldownEffectPrefab;
    }

    public enum UpgradeType
    {
        Obliterator,
        BazookaTurret
    }

    [Header("Upgrade slot")]
    [SerializeField] private UpgradeType upgradeType;
    [SerializeField] private Transform aimPivot;

    private Collider[] ownerColliders;
    private Transform[] discoveredBarrels;
    private bool isPurchased;
    private GameObject slotRoot;

    public UpgradeType Type => upgradeType;
    public bool IsPurchased => isPurchased;
    public Transform AimPivot => aimPivot != null ? aimPivot : transform;
    public Transform[] Barrels
    {
        get
        {
            if (discoveredBarrels == null) DiscoverBarrels();
            return discoveredBarrels;
        }
    }
    public Collider[] OwnerColliders => ownerColliders;
    public GameObject SlotRoot => slotRoot != null ? slotRoot : gameObject;

    public bool HasRequiredConfiguration
    {
        get
        {
            Transform[] barrels = Barrels;
            if (barrels.Length == 0) return false;
            if (upgradeType == UpgradeType.Obliterator)
            {
                if (barrels.Length < 9) return false;
                for (int i = 0; i < 9; i++)
                {
                    if (barrels[i] == null) return false;
                }

                return true;
            }

            foreach (Transform barrel in barrels)
            {
                if (barrel != null) return true;
            }

            return false;
        }
    }

    private void Awake()
    {
        ownerColliders = GetComponentsInChildren<Collider>(true);
        DiscoverBarrels();
    }

    private void DiscoverBarrels()
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        int barrelCount = 0;
        foreach (Transform child in childTransforms)
        {
            if (IsBarrelTransform(child))
            {
                barrelCount++;
            }
        }

        discoveredBarrels = new Transform[barrelCount];
        int index = 0;
        foreach (Transform child in childTransforms)
        {
            if (IsBarrelTransform(child))
            {
                discoveredBarrels[index++] = child;
            }
        }
    }

    private bool IsBarrelTransform(Transform candidate)
    {
        if (candidate == transform) return false;

        if (candidate.name.StartsWith("Barrel", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return upgradeType == UpgradeType.BazookaTurret
            && candidate.name.StartsWith("No rocket Launcher", System.StringComparison.OrdinalIgnoreCase);
    }

    public void SetUpgradeType(UpgradeType type)
    {
        if (upgradeType == type) return;
        upgradeType = type;
        DiscoverBarrels();
    }

    public void SetSlotRoot(GameObject root)
    {
        slotRoot = root != null ? root : gameObject;
    }

    public void SetPurchased(bool purchased)
    {
        isPurchased = purchased;
    }
}
