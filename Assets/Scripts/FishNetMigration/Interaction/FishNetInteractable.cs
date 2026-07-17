using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Interaction
{
    /// <summary>
    /// Base class for any FishNet object a player can interact with.
    ///
    /// The client only chooses a target. The server identifies the sender through
    /// the ServerRpc connection, finds that connection's player object, and performs
    /// distance and line-of-sight validation before gameplay state may change.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public abstract class FishNetInteractable : NetworkBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private string defaultPrompt = "Interact";

        [Tooltip(
            "Optional default point for interaction ID 0. When empty, the closest " +
            "point on this object's collider is used.")]
        [SerializeField] private Transform interactionPoint;

        private Collider _cachedCollider;

        public virtual bool AllowDirectColliderTargeting => true;

        public virtual string GetPrompt(
            FishNetPlayerInteractor interactor,
            int interactionId)
        {
            return defaultPrompt;
        }

        public virtual bool CanShowPrompt(
            FishNetPlayerInteractor interactor,
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
                _cachedCollider =
                    GetComponentInChildren<Collider>();
            }

            return _cachedCollider != null
                ? _cachedCollider.ClosestPoint(
                    observerPosition)
                : transform.position;
        }

        public void RequestInteraction(int interactionId)
        {
            if (!IsClientInitialized)
            {
                return;
            }

            RequestInteractionServerRpc(interactionId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractionServerRpc(
            int interactionId,
            NetworkConnection sender = null)
        {
            if (!IsServerInitialized ||
                sender == null ||
                !sender.IsValid)
            {
                return;
            }

            NetworkObject playerObject =
                sender.FirstObject;

            if (playerObject == null ||
                playerObject.Owner != sender)
            {
                return;
            }

            FishNetPlayerInteractor interactor =
                playerObject.GetComponent<
                    FishNetPlayerInteractor>();

            if (interactor == null ||
                !interactor.ValidateServerInteraction(
                    this,
                    interactionId))
            {
                return;
            }

            if (!CanInteractOnServer(
                    interactor,
                    interactionId))
            {
                return;
            }

            InteractOnServer(
                interactor,
                interactionId);
        }

        protected virtual bool CanInteractOnServer(
            FishNetPlayerInteractor interactor,
            int interactionId)
        {
            return true;
        }

        protected abstract void InteractOnServer(
            FishNetPlayerInteractor interactor,
            int interactionId);
    }
}
