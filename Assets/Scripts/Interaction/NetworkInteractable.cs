using Unity.Netcode;
using UnityEngine;

namespace HillbillyTaxi.Interaction
{
    /// <summary>
    /// Base class for anything a networked player can interact with.
    ///
    /// The owning client chooses a NetworkObject plus an interaction ID.
    /// The server validates the request before changing gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public abstract class NetworkInteractable : NetworkBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private string defaultPrompt = "Interact";

        [Tooltip(
            "Optional default point used for interaction ID 0. " +
            "Complex interactables can override GetInteractionPosition.")]
        [SerializeField] private Transform interactionPoint;

        private Collider _cachedCollider;

        /// <summary>
        /// Simple objects can be targeted through any collider beneath them.
        /// Complex objects such as vehicles override this and require an explicit
        /// NetworkInteractionPoint on the collider being targeted.
        /// </summary>
        public virtual bool AllowDirectColliderTargeting => true;

        public virtual string GetPrompt(
            NetworkPlayerInteractor interactor,
            int interactionId)
        {
            return defaultPrompt;
        }

        public virtual bool CanShowPrompt(
            NetworkPlayerInteractor interactor,
            int interactionId)
        {
            return isActiveAndEnabled && IsSpawned;
        }

        public virtual Vector3 GetInteractionPosition(
            int interactionId,
            Vector3 observerPosition)
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

        internal bool TryInteractOnServer(
            NetworkPlayerInteractor interactor,
            int interactionId)
        {
            if (!IsServer || !isActiveAndEnabled || !IsSpawned)
            {
                return false;
            }

            if (!CanInteractOnServer(interactor, interactionId))
            {
                return false;
            }

            InteractOnServer(interactor, interactionId);
            return true;
        }

        protected virtual bool CanInteractOnServer(
            NetworkPlayerInteractor interactor,
            int interactionId)
        {
            return true;
        }

        protected abstract void InteractOnServer(
            NetworkPlayerInteractor interactor,
            int interactionId);
    }
}
