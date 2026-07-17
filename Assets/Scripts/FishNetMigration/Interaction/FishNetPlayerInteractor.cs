using FishNet.Object;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Interaction
{
    /// <summary>
    /// Owner-side camera targeting and server-side validation.
    /// Add this to the same FishNet player NetworkObject as FishNetPlayerCharacter.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FishNetPlayerInteractor : NetworkBehaviour
    {
        [Header("Owner references")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private FishNetInteractionPromptView promptView;

        [Header("Targeting")]
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        [Header("Server validation")]
        [SerializeField, Min(0f)] private float serverDistanceTolerance = 0.75f;

        [Tooltip(
            "Server-side eye offset. The server uses its synchronized copy of the " +
            "player root and never trusts a camera position supplied by the client.")]
        [SerializeField, Min(0f)] private float serverEyeHeight = 1.25f;

        [SerializeField] private bool requireServerLineOfSight = true;
        [SerializeField] private LayerMask serverLineOfSightLayers = ~0;

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[24];

        private FishNetInteractable _currentTarget;
        private int _currentInteractionId;
        private bool _interactionEnabled;

        public FishNetInteractable CurrentTarget =>
            _currentTarget;

        public int CurrentInteractionId =>
            _currentInteractionId;

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;

            if (!enabled)
            {
                ClearTarget();
            }
        }

        public void Tick(
            bool interactPressed,
            bool gameplayCursorLocked)
        {
            if (!_interactionEnabled ||
                !IsClientInitialized ||
                !IsOwner ||
                !gameplayCursorLocked ||
                interactionCamera == null)
            {
                ClearTarget();
                return;
            }

            if (TryFindLocalTarget(
                    out FishNetInteractable target,
                    out int interactionId))
            {
                SetTarget(target, interactionId);
            }
            else
            {
                ClearTarget();
            }

            if (!interactPressed ||
                _currentTarget == null)
            {
                return;
            }

            _currentTarget.RequestInteraction(
                _currentInteractionId);
        }

        internal bool ValidateServerInteraction(
            FishNetInteractable target,
            int interactionId)
        {
            if (!IsServerInitialized ||
                target == null ||
                !target.IsServerInitialized ||
                !target.isActiveAndEnabled)
            {
                return false;
            }

            Vector3 serverOrigin =
                transform.position +
                Vector3.up * serverEyeHeight;

            Vector3 targetPosition =
                target.GetInteractionPosition(
                    interactionId,
                    serverOrigin);

            float allowedDistance =
                interactionDistance +
                serverDistanceTolerance;

            if ((targetPosition - serverOrigin)
                    .sqrMagnitude >
                allowedDistance * allowedDistance)
            {
                return false;
            }

            if (!requireServerLineOfSight)
            {
                return true;
            }

            return HasServerLineOfSight(
                serverOrigin,
                targetPosition,
                target);
        }

        private bool TryFindLocalTarget(
            out FishNetInteractable target,
            out int interactionId)
        {
            target = null;
            interactionId = 0;

            Ray ray = new Ray(
                interactionCamera.transform.position,
                interactionCamera.transform.forward);

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _raycastHits,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);

            if (!TryGetNearestExternalHit(
                    hitCount,
                    out RaycastHit nearestHit))
            {
                return false;
            }

            FishNetInteractionPoint point =
                nearestHit.collider
                    .GetComponentInParent<
                        FishNetInteractionPoint>();

            if (point != null &&
                point.TryGetTarget(
                    out target,
                    out interactionId))
            {
                return target.CanShowPrompt(
                    this,
                    interactionId);
            }

            target =
                nearestHit.collider
                    .GetComponentInParent<
                        FishNetInteractable>();

            if (target == null ||
                !target.AllowDirectColliderTargeting)
            {
                target = null;
                return false;
            }

            interactionId = 0;

            return target.CanShowPrompt(
                this,
                interactionId);
        }

        private bool HasServerLineOfSight(
            Vector3 origin,
            Vector3 targetPosition,
            FishNetInteractable expectedTarget)
        {
            Vector3 offset =
                targetPosition - origin;

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

            if (!TryGetNearestExternalHit(
                    hitCount,
                    out RaycastHit nearestHit))
            {
                // Nothing blocked the segment.
                return true;
            }

            FishNetInteractionPoint point =
                nearestHit.collider
                    .GetComponentInParent<
                        FishNetInteractionPoint>();

            if (point != null &&
                point.TryGetTarget(
                    out FishNetInteractable pointTarget,
                    out _))
            {
                return pointTarget == expectedTarget;
            }

            FishNetInteractable directTarget =
                nearestHit.collider
                    .GetComponentInParent<
                        FishNetInteractable>();

            return directTarget == expectedTarget;
        }

        private bool TryGetNearestExternalHit(
            int hitCount,
            out RaycastHit nearestHit)
        {
            nearestHit = default;
            float nearestDistance =
                float.PositiveInfinity;

            bool found = false;

            for (int index = 0;
                 index < hitCount;
                 index++)
            {
                RaycastHit hit = _raycastHits[index];

                if (hit.collider == null)
                {
                    continue;
                }

                Transform hitTransform =
                    hit.collider.transform;

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
                found = true;
            }

            return found;
        }

        private void SetTarget(
            FishNetInteractable target,
            int interactionId)
        {
            _currentTarget = target;
            _currentInteractionId = interactionId;

            string prompt =
                _currentTarget.GetPrompt(
                    this,
                    interactionId);

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
            _currentInteractionId = 0;
            promptView?.Hide();
        }

        private void Reset()
        {
            interactionCamera =
                GetComponentInChildren<Camera>(true);

            promptView =
                GetComponentInChildren<
                    FishNetInteractionPromptView>(
                    true);
        }

        private void OnValidate()
        {
            interactionDistance =
                Mathf.Max(0.1f, interactionDistance);

            serverDistanceTolerance =
                Mathf.Max(0f, serverDistanceTolerance);

            serverEyeHeight =
                Mathf.Max(0f, serverEyeHeight);

            if (interactionCamera == null)
            {
                interactionCamera =
                    GetComponentInChildren<Camera>(
                        true);
            }

            if (promptView == null)
            {
                promptView =
                    GetComponentInChildren<
                        FishNetInteractionPromptView>(
                        true);
            }
        }
    }
}
