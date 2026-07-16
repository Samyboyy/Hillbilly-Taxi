using Unity.Netcode;
using UnityEngine;

namespace HillbillyTaxi.Interaction
{
    /// <summary>
    /// Base class for anything a networked player can interact with.
    ///
    /// The owning client only chooses a target and sends its NetworkObject reference.
    /// The server performs the distance, line-of-sight, and availability checks before
    /// allowing the interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public abstract class NetworkInteractable : NetworkBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private string defaultPrompt = "Interact";

        [Tooltip(
            "Optional point used for server distance and line-of-sight checks. " +
            "When empty, the closest point on this object's collider is used.")]
        [SerializeField] private Transform interactionPoint;

        private Collider _cachedCollider;

        public virtual string GetPrompt(NetworkPlayerInteractor interactor)
        {
            return defaultPrompt;
        }

        public virtual bool CanShowPrompt(NetworkPlayerInteractor interactor)
        {
            return isActiveAndEnabled && IsSpawned;
        }

        public Vector3 GetInteractionPosition(Vector3 observerPosition)
        {
            if (interactionPoint != null)
            {
                return interactionPoint.position;
            }

            if (_cachedCollider == null)
            {
                _cachedCollider = GetComponentInChildren<Collider>();
            }

            return _cachedCollider != null
                ? _cachedCollider.ClosestPoint(observerPosition)
                : transform.position;
        }

        internal bool TryInteractOnServer(NetworkPlayerInteractor interactor)
        {
            if (!IsServer || !isActiveAndEnabled || !IsSpawned)
            {
                return false;
            }

            if (!CanInteractOnServer(interactor))
            {
                return false;
            }

            InteractOnServer(interactor);
            return true;
        }

        protected virtual bool CanInteractOnServer(NetworkPlayerInteractor interactor)
        {
            return true;
        }

        protected abstract void InteractOnServer(NetworkPlayerInteractor interactor);
    }
}
