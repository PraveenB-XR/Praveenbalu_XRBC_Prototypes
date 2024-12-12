using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace UnityEngine.XR.Content.Interaction
{
    public class GearShiftLever : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
    {
        [System.Serializable]
        public class GearChangeEvent : UnityEvent<int> { }

        [SerializeField]
        Transform m_Handle = null;

        [SerializeField]
        int m_CurrentGear = 0; // -1 = Reverse, 0 = Neutral, 1-5 = Forward gears

        [SerializeField]
        float m_GearSpacing = 0.1f; // Distance between gear positions

        [SerializeField]
        bool m_SnapToGears = true;

        [SerializeField]
        GearChangeEvent m_OnGearChange = new GearChangeEvent();

        UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor m_Interactor;
        Vector3 m_InitialPosition;
        bool m_IsGrabbed;
        
        // Gear positions (local Z positions)
        readonly float[] m_GearPositions = new float[] 
        {
            -0.2f,  // Reverse (-1)
            0f,     // Neutral (0)
            0.1f,   // First (1)
            0.2f,   // Second (2)
            0.3f,   // Third (3)
            0.4f,   // Fourth (4)
            0.5f    // Fifth (5)
        };

        public int currentGear => m_CurrentGear;
        public GearChangeEvent onGearChange => m_OnGearChange;

        void Start()
        {
            if (m_Handle != null)
            {
                m_InitialPosition = m_Handle.localPosition;
                UpdateGearPosition(m_CurrentGear);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectEntered.AddListener(StartGrab);
            selectExited.AddListener(EndGrab);
        }

        protected override void OnDisable()
        {
            selectEntered.RemoveListener(StartGrab);
            selectExited.RemoveListener(EndGrab);
            base.OnDisable();
        }

        void StartGrab(SelectEnterEventArgs args)
        {
            m_Interactor = args.interactorObject;
            m_IsGrabbed = true;
        }

        void EndGrab(SelectExitEventArgs args)
        {
            if (m_SnapToGears)
            {
                UpdateGearPosition(m_CurrentGear);
            }
            m_Interactor = null;
            m_IsGrabbed = false;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && isSelected)
            {
                UpdateGearShift();
            }
        }

        void UpdateGearShift()
        {
            if (m_Handle == null || m_Interactor == null) return;

            // Get the local position relative to the gear shift base
            Vector3 targetPosition = transform.InverseTransformPoint(m_Interactor.GetAttachTransform(this).position);
            Vector3 newPosition = m_InitialPosition;
            newPosition.z = Mathf.Clamp(targetPosition.z, m_GearPositions[0], m_GearPositions[^1]);

            // Update handle position
            m_Handle.localPosition = newPosition;

            // Determine current gear based on position
            int newGear = DetermineGearFromPosition(newPosition.z);
            if (newGear != m_CurrentGear)
            {
                m_CurrentGear = newGear;
                m_OnGearChange.Invoke(m_CurrentGear);
            }
        }

        int DetermineGearFromPosition(float zPosition)
        {
            float closestDistance = float.MaxValue;
            int closestGear = 0;

            for (int i = 0; i < m_GearPositions.Length; i++)
            {
                float distance = Mathf.Abs(zPosition - m_GearPositions[i]);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestGear = i - 1; // Adjust for our gear numbering (-1 to 5)
                }
            }

            return closestGear;
        }

        void UpdateGearPosition(int gear)
        {
            if (m_Handle != null)
            {
                Vector3 newPosition = m_InitialPosition;
                newPosition.z = m_GearPositions[gear + 1]; // +1 to adjust for array index
                m_Handle.localPosition = newPosition;
            }
        }
    }
}