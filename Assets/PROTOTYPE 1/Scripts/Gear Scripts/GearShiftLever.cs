using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using TMPro;

namespace UnityEngine.XR.Content.Interaction
{
    public class GearShiftLever : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
    {
        [System.Serializable]
        public class GearChangeEvent : UnityEvent<int> { }

        [Header("References")]
        [SerializeField]
        Transform m_Handle = null;
        [SerializeField]
        AudioSource m_AudioSource = null;
        [SerializeField]
        AudioClip m_GearShiftSound = null;
        [SerializeField]
        TextMeshProUGUI m_GearDisplayText = null;
        [SerializeField]
        TextMeshProUGUI m_ClutchStatusText = null;

        [Header("Gear Settings")]
        [SerializeField]
        int m_CurrentGear = 0;
        [SerializeField]
        float m_GearSpacing = 0.05f;
        [SerializeField]
        float m_GearLockDuration = 1.0f;
        [SerializeField]
        bool m_SnapToGears = true;

        [Header("Movement Limits")]
        [SerializeField]
        float m_MinPosition = -0.05f;
        [SerializeField]
        float m_MaxPosition = 0.25f;

        [SerializeField]
        GearChangeEvent m_OnGearChange = new GearChangeEvent();

        UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor m_Interactor;
        Vector3 m_InitialPosition;
        Vector3 m_LockedPosition;
        bool m_IsGrabbed;
        bool m_IsLocked;
        bool m_IsTransitioning;
        bool m_IsTriggerPressed;
        bool m_WasGearShifted;
        float m_BaseZPosition;
        float[] m_GearOffsets;

        readonly string[] m_GearDisplayStrings = new string[]
        {
            "R",
            "N",
            "1",
            "2",
            "3",
            "4",
            "5"
        };

        public int currentGear => m_CurrentGear;
        public GearChangeEvent onGearChange => m_OnGearChange;
        public bool isLocked => m_IsLocked;

        void OnValidate()
        {
            m_MinPosition = -m_GearSpacing;
            m_MaxPosition = m_GearSpacing * 5f;
            UpdateGearOffsets();
        }

        void UpdateGearOffsets()
        {
            m_GearOffsets = new float[]
            {
                -m_GearSpacing,
                0f,
                m_GearSpacing,
                m_GearSpacing * 2f,
                m_GearSpacing * 3f,
                m_GearSpacing * 4f,
                m_GearSpacing * 5f
            };
        }

        void Start()
        {
            UpdateGearOffsets();

            if (m_Handle != null)
            {
                m_InitialPosition = m_Handle.localPosition;
                m_BaseZPosition = m_InitialPosition.z;
                UpdateGearPosition(m_CurrentGear);
            }

            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
                m_AudioSource.spatialBlend = 1.0f;
            }

            UpdateGearDisplayText();
            UpdateClutchUI();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectEntered.AddListener(StartGrab);
            selectExited.AddListener(EndGrab);
            activated.AddListener(OnTriggerPressed);
            deactivated.AddListener(OnTriggerReleased);
        }

        protected override void OnDisable()
        {
            selectEntered.RemoveListener(StartGrab);
            selectExited.RemoveListener(EndGrab);
            activated.RemoveListener(OnTriggerPressed);
            deactivated.RemoveListener(OnTriggerReleased);
            base.OnDisable();
        }

        void StartGrab(SelectEnterEventArgs args)
        {
            m_Interactor = args.interactorObject;
            m_IsGrabbed = true;
            m_WasGearShifted = false;

            // Move interactor's attach transform to gear position
            if (m_Interactor != null && m_Handle != null)
            {
                var attachTransform = m_Interactor.GetAttachTransform(this);
                attachTransform.position = m_Handle.position;
                attachTransform.rotation = m_Handle.rotation;
            }
        }

        void EndGrab(SelectExitEventArgs args)
        {
            if (m_SnapToGears)
            {
                UpdateGearPosition(m_CurrentGear);
            }
            m_Interactor = null;
            m_IsGrabbed = false;
            m_IsTriggerPressed = false;
            m_WasGearShifted = false;
            UpdateClutchUI();
        }

        void OnTriggerPressed(ActivateEventArgs args)
        {
            m_IsTriggerPressed = true;
            UpdateClutchUI();
        }

        void OnTriggerReleased(DeactivateEventArgs args)
        {
            m_IsTriggerPressed = false;
            m_WasGearShifted = false;
            UpdateClutchUI();
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic &&
                isSelected && !m_IsLocked && m_IsTriggerPressed)
            {
                UpdateGearShift();
            }
        }

        void UpdateGearShift()
        {
            if (m_Handle == null || m_Interactor == null) return;

            if (m_IsTransitioning)
            {
                m_Handle.localPosition = m_LockedPosition;
                return;
            }

            Vector3 targetPosition = transform.InverseTransformPoint(m_Interactor.GetAttachTransform(this).position);
            Vector3 newPosition = m_InitialPosition;

            float zOffset = Mathf.Clamp(targetPosition.z - m_BaseZPosition, m_MinPosition, m_MaxPosition);
            newPosition.z = m_BaseZPosition + zOffset;
            m_Handle.localPosition = newPosition;

            int newGear = DetermineGearFromPosition(zOffset);
            if (newGear != m_CurrentGear)
            {
                StartGearTransition(newGear);
            }
        }

        void StartGearTransition(int newGear)
        {
            if (m_IsTransitioning) return;

            m_IsTransitioning = true;
            m_LockedPosition = m_Handle.localPosition;
            m_WasGearShifted = true;
            ChangeGear(newGear);
        }

        void ChangeGear(int newGear)
        {
            m_CurrentGear = newGear;
            m_OnGearChange.Invoke(m_CurrentGear);

            if (m_AudioSource != null && m_GearShiftSound != null)
            {
                m_AudioSource.PlayOneShot(m_GearShiftSound);
            }

            UpdateGearDisplayText();
            StartCoroutine(LockGearCoroutine());
        }

        int DetermineGearFromPosition(float zOffset)
        {
            float deadzone = m_GearSpacing * 0.2f;
            
            for (int i = -1; i <= 5; i++)
            {
                float gearPosition = i * m_GearSpacing;
                if (Mathf.Abs(zOffset - gearPosition) < deadzone)
                {
                    return i;
                }
            }

            return m_CurrentGear;
        }

        void UpdateGearDisplayText()
        {
            if (m_GearDisplayText != null)
            {
                int arrayIndex = m_CurrentGear + 1;
                if (arrayIndex >= 0 && arrayIndex < m_GearDisplayStrings.Length)
                {
                    m_GearDisplayText.text = m_GearDisplayStrings[arrayIndex];
                }
            }
        }

        void UpdateClutchUI()
        {
            if (m_ClutchStatusText != null)
            {
                m_ClutchStatusText.text = m_IsTriggerPressed ? "CLUTCH: ON" : "CLUTCH: OFF";
            }
        }

        IEnumerator LockGearCoroutine()
        {
            m_IsLocked = true;
            m_IsTransitioning = true;

            Vector3 targetPosition = m_InitialPosition;
            targetPosition.z = m_BaseZPosition + m_GearOffsets[m_CurrentGear + 1];

            float elapsedTime = 0f;
            Vector3 startPosition = m_Handle.localPosition;

            while (elapsedTime < m_GearLockDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsedTime / m_GearLockDuration);
                m_Handle.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            m_Handle.localPosition = targetPosition;
            m_LockedPosition = targetPosition;

            yield return new WaitForSeconds(0.1f);

            m_IsLocked = false;
            m_IsTransitioning = false;
        }

        void UpdateGearPosition(int gear)
        {
            if (m_Handle != null)
            {
                Vector3 newPosition = m_InitialPosition;
                newPosition.z = m_BaseZPosition + m_GearOffsets[gear + 1];
                m_Handle.localPosition = newPosition;
            }
        }
    }
}