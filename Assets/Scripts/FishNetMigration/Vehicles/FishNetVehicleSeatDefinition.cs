using System;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Vehicles
{
    [Serializable]
    public sealed class FishNetVehicleSeatDefinition
    {
        [SerializeField] private string displayName = "seat";
        [SerializeField] private FishNetVehicleSeatRole role;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private Transform occupantAnchor;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Transform exitPoint;

        public string DisplayName => displayName;
        public FishNetVehicleSeatRole Role => role;
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
