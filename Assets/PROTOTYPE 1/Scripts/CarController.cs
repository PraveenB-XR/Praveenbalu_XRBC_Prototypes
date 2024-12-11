using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace UnityEngine.XR.Content.Interaction
{
    public class CarController : XRBaseInteractable
    {
        [Header("Car Settings")]
        [SerializeField]
        private Rigidbody carRigidbody;

        [SerializeField]
        private float accelerationForce = 1000f;

        private bool isTriggerPressed = false;

        protected override void OnEnable()
        {
            base.OnEnable();
            // Subscribe to select events similar to SteeringController
            selectEntered.AddListener(OnSelectEntered);
            selectExited.AddListener(OnSelectExited);
        }

        protected override void OnDisable()
        {
            selectEntered.RemoveListener(OnSelectEntered);
            selectExited.RemoveListener(OnSelectExited);
            base.OnDisable();
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            // This event is called when the Interactor "selects" this interactable (trigger pressed)
            isTriggerPressed = true;
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            // This event is called when the Interactor stops selecting this interactable (trigger released)
            isTriggerPressed = false;
        }

        private void FixedUpdate()
        {
            // Apply force while trigger is pressed
            if (isTriggerPressed && carRigidbody != null)
            {
                Vector3 forwardDirection = transform.forward;
                carRigidbody.AddForce(forwardDirection * accelerationForce * Time.fixedDeltaTime, ForceMode.Force);
            }
        }
    }
}