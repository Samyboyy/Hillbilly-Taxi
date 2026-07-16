using System;
using Unity.Netcode;
using UnityEngine;

namespace HillbillyTaxi.Interaction
{
    /// <summary>
    /// Owner-side targeting plus server-side validation for player interactions.
    /// Add this to the same NetworkObject as NetworkPlayerCharacter.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerInteractor : NetworkBehaviour
    {
        [Header("Owner references")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private InteractionPromptView promptView;

        [Header("Targeting")]
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        [Header("Server validation")]
        [Tooltip("Extra tolerance for normal network movement delay.")]
        [SerializeField, Min(0f)] private float serverDistanceTolerance = 0.75f;

        [Tooltip(
            "Approximate eye height used by the server. It does not trust the client's " +
            "reported camera position.")]
        [SerializeField, Min(0f)] private float serverEyeHeight = 1.25f;

        [SerializeField] private bool requireServerLineOfSight = true;
        [SerializeField] private LayerMask serverLineOfSightLayers = ~0;

        private readonly RaycastHit[] _raycastHits = new RaycastHit[24];

        private NetworkInteractable _currentTarget;
        private bool _interactionEnabled;

        public NetworkInteractable CurrentTarget => _currentTarget;

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;

            if (!enabled)
            {
                ClearTarget();
            }
        }

        public void Tick(bool interactPressed, bool gameplayCursorLocked)
        {
            if (!_interactionEnabled ||
                !IsSpawned ||
                !IsOwner ||
                !gameplayCursorLocked ||
                interactionCamera == null)
            {
                ClearTarget();
                return;
            }

            NetworkInteractable target = FindLocalTarget();
            SetTarget(target);

            if (!interactPressed || _currentTarget == null)
            {
                return;
            }

            NetworkObject targetNetworkObject = _currentTarget.NetworkObject;

            if (targetNetworkObject == null || !targetNetworkObject.IsSpawned)
            {
                return;
            }

            RequestInteractionRpc(
                new NetworkObjectReference(targetNetworkObject));
        }

        public override void OnNetworkDespawn()
        {
            ClearTarget();
            base.OnNetworkDespawn();
        }

        private NetworkInteractable FindLocalTarget()
        {
            Ray ray = new Ray(
                interactionCamera.transform.position,
                interactionCamera.transform.forward);

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _raycastHits,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);

            bool foundNearestHit = false;
            float nearestDistance = float.PositiveInfinity;
            RaycastHit nearestHit = default;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _raycastHits[index];

                if (hit.collider == null)
                {
                    continue;
                }

                Transform hitTransform = hit.collider.transform;

                if (hitTransform == transform ||
                    hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestHit = hit;
                foundNearestHit = true;
            }

            if (!foundNearestHit)
            {
                return null;
            }

            NetworkInteractable target =
                nearestHit.collider.GetComponentInParent<NetworkInteractable>();

            if (target == null || !target.CanShowPrompt(this))
            {
                return null;
            }

            return target;
        }

        private void SetTarget(NetworkInteractable target)
        {
            _currentTarget = target;

            if (_currentTarget == null)
            {
                promptView?.Hide();
                return;
            }

            string prompt = _currentTarget.GetPrompt(this);

            if (string.IsNullOrWhiteSpace(prompt))
            {
                promptView?.Hide();
                return;
            }

            promptView?.Show(prompt);
        }

        private void ClearTarget()
        {
            _currentTarget = null;
            promptView?.Hide();
        }

        [Rpc(SendTo.Server)]
        private void RequestInteractionRpc(
            NetworkObjectReference targetReference,
            RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            if (!targetReference.TryGet(
                    out NetworkObject targetNetworkObject,
                    NetworkManager))
            {
                return;
            }

            NetworkInteractable target =
                targetNetworkObject.GetComponent<NetworkInteractable>();

            if (target == null)
            {
                target = targetNetworkObject
                    .GetComponentInChildren<NetworkInteractable>(true);
            }

            if (target == null ||
                !target.isActiveAndEnabled ||
                !target.IsSpawned)
            {
                return;
            }

            Vector3 serverOrigin =
                transform.position + Vector3.up * serverEyeHeight;

            Vector3 targetPosition =
                target.GetInteractionPosition(serverOrigin);

            float allowedDistance =
                interactionDistance + serverDistanceTolerance;

            if ((targetPosition - serverOrigin).sqrMagnitude >
                allowedDistance * allowedDistance)
            {
                return;
            }

            if (requireServerLineOfSight &&
                !HasServerLineOfSight(
                    serverOrigin,
                    targetPosition,
                    target))
            {
                return;
            }

            target.TryInteractOnServer(this);
        }

        private bool HasServerLineOfSight(
            Vector3 origin,
            Vector3 targetPosition,
            NetworkInteractable expectedTarget)
        {
            Vector3 offset = targetPosition - origin;
            float distance = offset.magnitude;

            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                offset / distance,
                _raycastHits,
                distance + 0.05f,
                serverLineOfSightLayers,
                QueryTriggerInteraction.Collide);

            bool foundNearestHit = false;
            float nearestDistance = float.PositiveInfinity;
            RaycastHit nearestHit = default;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _raycastHits[index];

                if (hit.collider == null)
                {
                    continue;
                }

                Transform hitTransform = hit.collider.transform;

                if (hitTransform == transform ||
                    hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestHit = hit;
                foundNearestHit = true;
            }

            if (!foundNearestHit)
            {
                // Nothing blocked the ray. This also supports interactables that use
                // trigger-only colliders.
                return true;
            }

            NetworkInteractable hitTarget =
                nearestHit.collider.GetComponentInParent<NetworkInteractable>();

            return hitTarget == expectedTarget;
        }

        private void Reset()
        {
            interactionCamera = GetComponentInChildren<Camera>(true);
            promptView = GetComponentInChildren<InteractionPromptView>(true);
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0.1f, interactionDistance);
            serverDistanceTolerance = Mathf.Max(0f, serverDistanceTolerance);
            serverEyeHeight = Mathf.Max(0f, serverEyeHeight);

            if (interactionCamera == null)
            {
                interactionCamera = GetComponentInChildren<Camera>(true);
            }

            if (promptView == null)
            {
                promptView =
                    GetComponentInChildren<InteractionPromptView>(true);
            }
        }
    }
}
