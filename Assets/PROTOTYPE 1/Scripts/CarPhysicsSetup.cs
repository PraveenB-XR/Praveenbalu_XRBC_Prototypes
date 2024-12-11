using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class CarPhysicsSetup : MonoBehaviour
{
    private Rigidbody rb;
    private BoxCollider boxCollider;

    void Awake()
    {
        SetupPhysics();
    }

    void SetupPhysics()
    {
        // Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        rb.mass = 1500;
        rb.drag = 2;
        rb.angularDrag = 5;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Calculate and set the center of mass lower
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            // Set center of mass lower than geometric center
            rb.centerOfMass = new Vector3(0, -0.5f, 0);
        }

        // Create and apply physics material
        PhysicMaterial carPhysicsMaterial = new PhysicMaterial()
        {
            dynamicFriction = 0.6f,
            staticFriction = 0.6f,
            bounciness = 0f,
            frictionCombine = PhysicMaterialCombine.Average,
            bounceCombine = PhysicMaterialCombine.Minimum
        };

        boxCollider.material = carPhysicsMaterial;
    }

    void OnValidate()
    {
        // Auto-adjust collider settings in editor
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            // Adjust these values based on your car model size
            boxCollider.center = new Vector3(0, 0.5f, 0); // Adjust Y value to match your car's center
            // Adjust these sizes based on your car model
            boxCollider.size = new Vector3(2f, 1f, 4f); // Example values, adjust to match your car
        }
    }
}