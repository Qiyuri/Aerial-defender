using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BazookaProjectile : MonoBehaviour
{
    private static Material sharedParticleMaterial;

    private Rigidbody projectileBody;
    private Collider projectileCollider;
    private float damage = 5f;
    private float projectileSpeed;
    private float targetSearchDistance;
    private float targetLockAngle;
    private float targetTurnSpeed;
    private Vector3 travelDirection;
    private bool hasHit;
    private ParticleSystem smokeTrail;
    private ParticleSystem impactEffectPrefab;
    private Collider[] ownerColliders;
    private Transform lockedTarget;
    private readonly RaycastHit[] sweepHits = new RaycastHit[64];
    private readonly Collider[] overlapHits = new Collider[64];

    private void Awake()
    {
        projectileBody = GetComponent<Rigidbody>();
        projectileBody.useGravity = false;
        projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        projectileCollider = GetComponent<Collider>();
        if (projectileCollider == null)
        {
            SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.15f;
            projectileCollider = sphereCollider;
        }

        projectileCollider.isTrigger = false;
    }

    private void FixedUpdate()
    {
        if (!hasHit && projectileSpeed > 0f)
        {
            UpdateTargetLock();
            float travelDistance = projectileSpeed * Time.fixedDeltaTime;
            float sweepRadius = Mathf.Max(0.05f, projectileCollider.bounds.extents.magnitude * 0.5f);
            Vector3 sweepOrigin = projectileCollider.bounds.center;
            int overlapCount = Physics.OverlapSphereNonAlloc(
                sweepOrigin,
                sweepRadius,
                overlapHits,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            Collider overlapTarget = null;
            Vector3 overlapPoint = default;
            float nearestOverlapDistance = float.PositiveInfinity;
            for (int i = 0; i < overlapCount; i++)
            {
                Collider overlappingCollider = overlapHits[i];
                if (IsIgnoredCollider(overlappingCollider)) continue;

                Vector3 impactPoint = overlappingCollider.ClosestPoint(sweepOrigin);
                float distance = (impactPoint - sweepOrigin).sqrMagnitude;
                if (distance >= nearestOverlapDistance) continue;

                overlapTarget = overlappingCollider;
                overlapPoint = impactPoint;
                nearestOverlapDistance = distance;
            }

            if (overlapTarget != null)
            {
                HandleImpact(overlapTarget, overlapPoint, -travelDirection);
                return;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                sweepOrigin,
                sweepRadius,
                travelDirection,
                sweepHits,
                travelDistance + projectileCollider.bounds.extents.magnitude * 0.05f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            RaycastHit nearestHit = default;
            bool foundHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = sweepHits[i];
                if (IsIgnoredCollider(candidate.collider)) continue;
                if (foundHit && candidate.distance >= nearestHit.distance) continue;

                nearestHit = candidate;
                foundHit = true;
            }

            if (foundHit)
            {
                HandleImpact(nearestHit.collider, nearestHit.point, nearestHit.normal);
                return;
            }

            projectileBody.linearVelocity = travelDirection * projectileSpeed;
        }
    }

    private void UpdateTargetLock()
    {
        if (lockedTarget == null)
        {
            lockedTarget = FindTargetInFront();
        }

        if (lockedTarget == null) return;

        Vector3 directionToTarget = lockedTarget.position - projectileCollider.bounds.center;
        if (directionToTarget.sqrMagnitude < 0.0001f) return;

        travelDirection = Vector3.RotateTowards(
            travelDirection,
            directionToTarget.normalized,
            targetTurnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime,
            0f).normalized;
        projectileBody.rotation = Quaternion.LookRotation(travelDirection);
    }

    private Transform FindTargetInFront()
    {
        if (targetSearchDistance <= 0f) return null;

        Vector3 origin = projectileCollider.bounds.center;
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            targetSearchDistance,
            overlapHits,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);
        float minimumDot = Mathf.Cos(targetLockAngle * Mathf.Deg2Rad);
        float nearestDistance = float.PositiveInfinity;
        Transform nearestTarget = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = overlapHits[i];
            if (IsIgnoredCollider(candidate)) continue;

            Vector3 closestPoint = candidate.ClosestPoint(origin);
            Vector3 offset = closestPoint - origin;
            float distance = offset.magnitude;
            if (distance < 0.01f || distance > targetSearchDistance) continue;
            if (Vector3.Dot(travelDirection, offset / distance) < minimumDot) continue;
            if (distance >= nearestDistance) continue;

            FlyingAircraft aircraft = candidate.GetComponentInParent<FlyingAircraft>();
            nearestTarget = aircraft != null
                ? aircraft.transform
                : candidate.attachedRigidbody != null
                    ? candidate.attachedRigidbody.transform
                    : candidate.transform;
            nearestDistance = distance;
        }

        return nearestTarget;
    }

    public void Launch(
        Vector3 direction,
        float speed,
        float lifetime,
        Collider[] ownerColliders,
        float projectileDamage,
        ParticleSystem smokeTrailPrefab,
        ParticleSystem impactEffect,
        float targetSearchDistance,
        float targetLockAngle,
        float targetTurnSpeed)
    {
        damage = projectileDamage;
        travelDirection = direction.normalized;
        projectileSpeed = Mathf.Max(0f, speed);
        impactEffectPrefab = impactEffect;
        this.targetSearchDistance = Mathf.Max(0f, targetSearchDistance);
        this.targetLockAngle = Mathf.Clamp(targetLockAngle, 0f, 45f);
        this.targetTurnSpeed = Mathf.Max(0f, targetTurnSpeed);
        this.ownerColliders = ownerColliders;
        projectileBody.linearVelocity = travelDirection * projectileSpeed;

        if (ownerColliders != null)
        {
            foreach (Collider ownerCollider in ownerColliders)
            {
                if (ownerCollider != null && ownerCollider != projectileCollider)
                {
                    Physics.IgnoreCollision(projectileCollider, ownerCollider);
                }
            }
        }

        smokeTrail = smokeTrailPrefab != null
            ? Instantiate(smokeTrailPrefab, transform.position, transform.rotation, transform)
            : CreateDefaultSmokeTrail();
        ParticleSystem.MainModule trailMain = smokeTrail.main;
        trailMain.loop = true;
        trailMain.simulationSpace = ParticleSystemSimulationSpace.World;
        trailMain.stopAction = ParticleSystemStopAction.Destroy;
        smokeTrail.Play(true);

        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider == null) return;
        if (collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            HandleImpact(collision.collider, contact.point, contact.normal);
        }
        else
        {
            HandleImpact(collision.collider, transform.position, -travelDirection);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleImpact(other, other.ClosestPoint(transform.position), -travelDirection);
    }

    private bool IsIgnoredCollider(Collider candidate)
    {
        if (candidate == null || candidate == projectileCollider || candidate.transform.IsChildOf(transform)) return true;
        if (ownerColliders == null) return false;

        foreach (Collider ownerCollider in ownerColliders)
        {
            if (candidate == ownerCollider) return true;
        }

        return false;
    }

    private void HandleImpact(Collider hitCollider, Vector3 impactPoint, Vector3 impactNormal)
    {
        if (hasHit || hitCollider == null || IsIgnoredCollider(hitCollider)) return;
        hasHit = true;
        projectileBody.linearVelocity = Vector3.zero;
        projectileBody.detectCollisions = false;

        if (impactNormal.sqrMagnitude <= 0.0001f) impactNormal = -travelDirection;

        if (hitCollider != null)
        {
            FlyingAircraft aircraft = hitCollider.GetComponentInParent<FlyingAircraft>();
            if (aircraft != null)
            {
                aircraft.TakeBazookaHit(damage, travelDirection);
            }
            else
            {
                hitCollider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }

        StopSmokeTrail();
        PlayImpactEffect(impactPoint, impactNormal);
        Destroy(gameObject);
    }

    private ParticleSystem CreateDefaultSmokeTrail()
    {
        GameObject trailObject = new GameObject("Bazooka smoke trail");
        trailObject.transform.SetParent(transform, false);

        ParticleSystem trail = trailObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = trail.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.32f, 0.32f, 0.32f, 0.55f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        ParticleSystem.EmissionModule emission = trail.emission;
        emission.rateOverTime = 35f;

        ParticleSystem.ShapeModule shape = trail.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.04f;

        SetParticleMaterial(trail);
        return trail;
    }

    private void PlayImpactEffect(Vector3 position, Vector3 normal)
    {
        Quaternion rotation = normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
        ParticleSystem impact = impactEffectPrefab != null
            ? Instantiate(impactEffectPrefab, position, rotation)
            : CreateDefaultImpactEffect(position, normal);

        ParticleSystem.MainModule main = impact.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        impact.Play(true);
    }

    private ParticleSystem CreateDefaultImpactEffect(Vector3 position, Vector3 normal)
    {
        GameObject impactObject = new GameObject("Bazooka impact effect");
        Quaternion rotation = normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
        impactObject.transform.SetPositionAndRotation(position, rotation);

        ParticleSystem impact = impactObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = impact.main;
        main.duration = 0.8f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.48f, 0.08f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = 40;

        ParticleSystem.EmissionModule emission = impact.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = impact.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        SetParticleMaterial(impact);
        impact.Emit(30);
        return impact;
    }

    private void StopSmokeTrail()
    {
        if (smokeTrail == null) return;

        smokeTrail.transform.SetParent(null, true);
        ParticleSystem.MainModule main = smokeTrail.main;
        main.loop = false;
        smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        smokeTrail = null;
    }

    private static void SetParticleMaterial(ParticleSystem particleSystem)
    {
        if (sharedParticleMaterial == null)
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader == null) return;

            sharedParticleMaterial = new Material(particleShader);
        }

        ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = sharedParticleMaterial;
    }
}
