using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace UnityEngine.XR.Content.Interaction
{
    public class SteerController : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
    {
        const float k_ModeSwitchDeadZone = 0.1f;

        struct TrackedRotation
        {
            float m_BaseAngle;
            float m_CurrentOffset;
            float m_AccumulatedAngle;
            public float totalOffset => m_AccumulatedAngle + m_CurrentOffset;

            public void Reset()
            {
                m_BaseAngle = 0.0f;
                m_CurrentOffset = 0.0f;
                m_AccumulatedAngle = 0.0f;
            }

            public void SetBaseFromVector(Vector3 direction)
            {
                m_AccumulatedAngle += m_CurrentOffset;
                m_BaseAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
                m_CurrentOffset = 0.0f;
            }

            public void SetTargetFromVector(Vector3 direction)
            {
                var targetAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
                m_CurrentOffset = ShortestAngleDistance(m_BaseAngle, targetAngle, 360.0f);

                if (Mathf.Abs(m_CurrentOffset) > 90.0f)
                {
                    m_BaseAngle = targetAngle;
                    m_AccumulatedAngle += m_CurrentOffset;
                    m_CurrentOffset = 0.0f;
                }
            }
        }

        [Header("Steering Settings")]
        [SerializeField] Transform m_Handle = null;
        [SerializeField, Range(0.0f, 1.0f)] float m_Value = 0.5f;
        [SerializeField] bool m_ClampedMotion = true;
        [SerializeField] float m_MaxAngle = 450f;
        [SerializeField] float m_MinAngle = -450f;
        [SerializeField] float m_ReturnSpeed = 100f;
        [SerializeField] float m_PositionTrackedRadius = 0.1f;
        [SerializeField] float m_TwistSensitivity = 1.5f;

        [Header("Car Movement")]
        [SerializeField] private float m_MaxSpeed = 15f;
        [SerializeField] private float m_AccelerationForce = 1500f;
        [SerializeField] private float m_MaxTurnAngle = 45f;
        [SerializeField] private float m_TurnTorqueFactor = 2f;
        [SerializeField] private float m_SteeringSensitivity = 1f;
        [SerializeField] private float m_StabilityForce = 20f;
        [SerializeField] private float m_MinTurnSpeed = 5f;

        [Header("Deceleration")]
        [Tooltip("Initial deceleration force applied opposite of travel direction after trigger release.")]
        [SerializeField] private float m_InitialDecelerationForce = 500f;
        [Tooltip("Time in seconds over which deceleration force is linearly reduced to zero.")]
        [SerializeField] private float m_DecelerationDuration = 2f;

        [Header("Audio")]
        [SerializeField] private AudioSource m_EngineAudioSource;

        public UnityEvent<float> onValueChange = new UnityEvent<float>();

        private Rigidbody m_CarRigidbody;
        private Transform m_CarTransform;
        private float m_CurrentSteerAngle;

        UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor m_Interactor;
        bool m_PositionDriven = false;
        bool m_UpVectorDriven = false;
        TrackedRotation m_PositionAngles = new TrackedRotation();
        TrackedRotation m_UpVectorAngles = new TrackedRotation();
        TrackedRotation m_ForwardVectorAngles = new TrackedRotation();
        float m_BaseKnobRotation = 0.0f;
        bool m_IsGrabbed = false;
        private bool m_IsAccelerating = false;

        // Deceleration state
        private bool m_IsDecelerating = false;
        private float m_DecelerationStartTime;

        void Start()
        {
            SetValue(m_Value);
            SetKnobRotation(ValueToRotation());

            m_CarTransform = transform.parent;
            if (m_CarTransform != null)
            {
                m_CarRigidbody = m_CarTransform.GetComponent<Rigidbody>();
                if (m_CarRigidbody == null)
                {
                    Debug.LogError("No Rigidbody found on car parent!");
                }
                else
                {
                    m_CarRigidbody.centerOfMass = new Vector3(0, -0.5f, 0);
                    m_CarRigidbody.maxAngularVelocity = 7;
                }
            }
            else
            {
                Debug.LogError("Steering wheel must be a child of the car!");
            }

            if (m_EngineAudioSource != null)
            {
                m_EngineAudioSource.Stop();
                m_EngineAudioSource.loop = true;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectEntered.AddListener(StartGrab);
            selectExited.AddListener(EndGrab);
            activated.AddListener(OnActivated);
            deactivated.AddListener(OnDeactivated);
        }

        protected override void OnDisable()
        {
            selectEntered.RemoveListener(StartGrab);
            selectExited.RemoveListener(EndGrab);
            activated.RemoveListener(OnActivated);
            deactivated.RemoveListener(OnDeactivated);
            base.OnDisable();
        }

        void StartGrab(SelectEnterEventArgs args)
        {
            m_IsGrabbed = true;
            m_Interactor = args.interactorObject;
            m_PositionAngles.Reset();
            m_UpVectorAngles.Reset();
            m_ForwardVectorAngles.Reset();
            UpdateBaseKnobRotation();
            UpdateRotation(true);
        }

        void EndGrab(SelectExitEventArgs args)
        {
            m_IsGrabbed = false;
            m_Interactor = null;
        }

        void OnActivated(ActivateEventArgs args)
        {
            m_IsAccelerating = true;
            m_IsDecelerating = false; // cancel any deceleration

            if (m_EngineAudioSource != null && !m_EngineAudioSource.isPlaying)
            {
                m_EngineAudioSource.Play();
            }
        }

        void OnDeactivated(DeactivateEventArgs args)
        {
            m_IsAccelerating = false;
            StartDeceleration();

            if (m_EngineAudioSource != null && m_EngineAudioSource.isPlaying)
            {
                m_EngineAudioSource.Stop();
            }
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            {
                if (isSelected)
                {
                    UpdateRotation();
                }
                else if (!m_IsGrabbed)
                {
                    ReturnToCenter();
                }

                if (m_CarRigidbody != null)
                {
                    ApplyCarPhysics();
                    UpdateXRRig();
                }
            }
        }

        void ReturnToCenter()
        {
            if (Mathf.Abs(m_Value - 0.5f) > 0.01f)
            {
                float newValue = Mathf.MoveTowards(m_Value, 0.5f, m_ReturnSpeed * Time.deltaTime / (m_MaxAngle - m_MinAngle));
                SetValue(newValue);
                SetKnobRotation(ValueToRotation());
                m_CurrentSteerAngle = 0f;
            }
        }

        void UpdateRotation(bool freshCheck = false)
        {
            if (m_Interactor == null) return;

            var interactorTransform = m_Interactor.GetAttachTransform(this);
            var localOffset = transform.InverseTransformVector(interactorTransform.position - m_Handle.position);
            localOffset.y = 0.0f;
            var radiusOffset = transform.TransformVector(localOffset).magnitude;
            localOffset.Normalize();

            var localForward = transform.InverseTransformDirection(interactorTransform.forward);
            var localY = Mathf.Abs(localForward.y);
            localForward.y = 0.0f;
            localForward.Normalize();

            var localUp = transform.InverseTransformDirection(interactorTransform.up);
            localUp.y = 0.0f;
            localUp.Normalize();

            if (m_PositionDriven && !freshCheck)
                radiusOffset *= (1.0f + k_ModeSwitchDeadZone);

            if (radiusOffset >= m_PositionTrackedRadius)
            {
                if (!m_PositionDriven || freshCheck)
                {
                    m_PositionAngles.SetBaseFromVector(localOffset);
                    m_PositionDriven = true;
                }
            }
            else
                m_PositionDriven = false;

            if (!freshCheck)
            {
                if (!m_UpVectorDriven)
                    localY *= (1.0f - (k_ModeSwitchDeadZone * 0.5f));
                else
                    localY *= (1.0f + (k_ModeSwitchDeadZone * 0.5f));
            }

            if (localY > 0.707f)
            {
                if (!m_UpVectorDriven || freshCheck)
                {
                    m_UpVectorAngles.SetBaseFromVector(localUp);
                    m_UpVectorDriven = true;
                }
            }
            else
            {
                if (m_UpVectorDriven || freshCheck)
                {
                    m_ForwardVectorAngles.SetBaseFromVector(localForward);
                    m_UpVectorDriven = false;
                }
            }

            if (m_PositionDriven)
                m_PositionAngles.SetTargetFromVector(localOffset);

            if (m_UpVectorDriven)
                m_UpVectorAngles.SetTargetFromVector(localUp);
            else
                m_ForwardVectorAngles.SetTargetFromVector(localForward);

            var knobRotation = m_BaseKnobRotation - ((m_UpVectorAngles.totalOffset + m_ForwardVectorAngles.totalOffset) * m_TwistSensitivity) - m_PositionAngles.totalOffset;

            if (m_ClampedMotion)
                knobRotation = Mathf.Clamp(knobRotation, m_MinAngle, m_MaxAngle);

            SetKnobRotation(knobRotation);

            var knobValue = (knobRotation - m_MinAngle) / (m_MaxAngle - m_MinAngle);
            SetValue(knobValue);

            m_CurrentSteerAngle = Mathf.Lerp(-m_MaxTurnAngle, m_MaxTurnAngle, knobValue) * m_SteeringSensitivity;
        }

        void ApplyCarPhysics()
        {
            if (m_CarRigidbody == null) return;

            Vector3 currentVelocity = m_CarRigidbody.velocity;
            float currentSpeed = currentVelocity.magnitude;

            // If accelerating and below max speed, apply forward force
            if (m_IsAccelerating && currentSpeed < m_MaxSpeed)
            {
                Vector3 forwardForce = m_CarTransform.forward * m_AccelerationForce * Time.fixedDeltaTime;
                m_CarRigidbody.AddForce(forwardForce, ForceMode.Acceleration);
            }
            else if (!m_IsAccelerating && m_IsDecelerating && currentSpeed > 0.1f)
            {
                // Apply deceleration force linearly decreasing over m_DecelerationDuration
                float elapsed = Time.time - m_DecelerationStartTime;
                if (elapsed < m_DecelerationDuration)
                {
                    float t = elapsed / m_DecelerationDuration;
                    float decelFactor = 1f - t;
                    float currentDecelForce = m_InitialDecelerationForce * decelFactor;

                    Vector3 decelForce = -currentVelocity.normalized * currentDecelForce * Time.fixedDeltaTime;
                    m_CarRigidbody.AddForce(decelForce, ForceMode.Acceleration);
                }
                else
                {
                    // Deceleration complete
                    m_IsDecelerating = false;
                }
            }

            // Only turn above min turn speed
            if (currentSpeed > m_MinTurnSpeed)
            {
                float turnRate = m_CurrentSteerAngle * m_TurnTorqueFactor;
                float speedFactor = Mathf.InverseLerp(0f, m_MaxSpeed, currentSpeed);
                turnRate *= Mathf.Lerp(1f, 0.5f, speedFactor);

                m_CarTransform.Rotate(Vector3.up, turnRate * Time.fixedDeltaTime);

                Vector3 forwardVelocity = Vector3.Project(currentVelocity, m_CarTransform.forward);
                Vector3 sideVelocity = Vector3.Project(currentVelocity, m_CarTransform.right);
                sideVelocity = Vector3.Lerp(sideVelocity, Vector3.zero, Time.fixedDeltaTime * 5f);
                m_CarRigidbody.velocity = forwardVelocity + sideVelocity;

                float downforce = Mathf.Lerp(0f, m_StabilityForce, Mathf.Abs(m_CurrentSteerAngle) / m_MaxTurnAngle);
                m_CarRigidbody.AddForce(-m_CarTransform.up * downforce * currentSpeed, ForceMode.Force);
            }

            // Keep car upright
            Vector3 carUp = m_CarTransform.up;
            if (Vector3.Dot(carUp, Vector3.up) < 0.99f)
            {
                Quaternion targetRotation = Quaternion.FromToRotation(carUp, Vector3.up) * m_CarTransform.rotation;
                m_CarRigidbody.MoveRotation(Quaternion.Slerp(m_CarRigidbody.rotation, targetRotation, Time.fixedDeltaTime * 5f));
            }
        }

        private void UpdateXRRig()
        {
            var xrRig = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>()?.transform;
            if (xrRig != null && xrRig.parent != m_CarTransform)
            {
                xrRig.SetParent(m_CarTransform, true);
            }
        }

        void SetKnobRotation(float angle)
        {
            if (m_Handle != null)
                m_Handle.localEulerAngles = new Vector3(0.0f, angle, 0.0f);
        }

        void SetValue(float value)
        {
            if (m_ClampedMotion)
                value = Mathf.Clamp01(value);

            m_Value = value;
            onValueChange.Invoke(m_Value);
        }

        float ValueToRotation()
        {
            return m_ClampedMotion ? Mathf.Lerp(m_MinAngle, m_MaxAngle, m_Value) : Mathf.LerpUnclamped(m_MinAngle, m_MaxAngle, m_Value);
        }

        void UpdateBaseKnobRotation()
        {
            m_BaseKnobRotation = Mathf.LerpUnclamped(m_MinAngle, m_MaxAngle, m_Value);
        }

        static float ShortestAngleDistance(float start, float end, float max)
        {
            var angleDelta = end - start;
            var angleSign = Mathf.Sign(angleDelta);

            angleDelta = Mathf.Abs(angleDelta) % max;
            if (angleDelta > (max * 0.5f))
                angleDelta = -(max - angleDelta);

            return angleDelta * angleSign;
        }

        /// <summary>
        /// Called when trigger is released to start a deceleration phase.
        /// Over m_DecelerationDuration seconds, a deceleration force is applied
        /// that linearly decreases from m_InitialDecelerationForce to zero.
        /// </summary>
        void StartDeceleration()
        {
            m_IsDecelerating = true;
            m_DecelerationStartTime = Time.time;
        }
    }
}
