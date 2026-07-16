using System;
using UnityEngine;

namespace HillbillyTaxi.Vehicles
{
    [Serializable]
    public sealed class VehicleSeatDefinition
    {
        [SerializeField] private string displayName = "Seat";
        [SerializeField] private VehicleSeatRole role;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private Transform occupantAnchor;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Transform exitPoint;

        public string DisplayName => displayName;
        public VehicleSeatRole Role => role;
        public Transform InteractionPoint => interactionPoint;
        public Transform OccupantAnchor => occupantAnchor;
        public Transform CameraAnchor => cameraAnchor;
        public Transform ExitPoint => exitPoint;

        public bool IsConfigured =>
            interactionPoint != null &&
            occupantAnchor != null &&
            cameraAnchor != null &&
            exitPoint != null;
    }
}
