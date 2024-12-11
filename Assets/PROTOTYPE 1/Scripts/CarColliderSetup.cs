using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

public class CarColliderSetup : MonoBehaviour
{
    [SerializeField] private Transform m_XRRig;
    [SerializeField] private LayerMask m_IgnoreCollisionLayers;

    void Start()
    {
        SetupXRRig();
        IgnoreCollisions();
    }

    void SetupXRRig()
    {
        if (m_XRRig == null)
        {
            m_XRRig = FindObjectOfType<XROrigin>()?.transform;
            if (m_XRRig == null)
            {
                Debug.LogWarning("XR Rig not assigned and couldn't be found in scene!");
                return;
            }
        }
    }

    void IgnoreCollisions()
    {
        // Get all colliders from the car and its children
        Collider[] carColliders = GetComponentsInChildren<Collider>();
        
        // Get all colliders from the XR Rig and its children
        Collider[] xrColliders = m_XRRig.GetComponentsInChildren<Collider>();

        // Ignore collisions between car and XR Rig colliders
        foreach (Collider carCol in carColliders)
        {
            if (carCol == null) continue;

            foreach (Collider xrCol in xrColliders)
            {
                if (xrCol == null) continue;

                Physics.IgnoreCollision(carCol, xrCol);
            }
        }
    }

    void OnValidate()
    {
        if (m_XRRig == null)
        {
            m_XRRig = FindObjectOfType<XROrigin>()?.transform;
        }
    }
}