using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.TaxiJobs
{
    /// <summary>
    /// Lightweight prototype presentation for a passenger walking between a
    /// roadside waiting point and a required-stop entrance.
    ///
    /// Contract state and timing come from the synchronized manager. This object
    /// is presentation only and never advances the contract.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetTaxiPassengerStopPresentation :
        MonoBehaviour
    {
        [SerializeField] private FishNetTaxiJobManager jobManager;
        [SerializeField] private GameObject passengerRoot;
        [SerializeField] private Transform roadsidePoint;
        [SerializeField] private Transform entrancePoint;

        private void Update()
        {
            if (jobManager == null ||
                passengerRoot == null ||
                roadsidePoint == null ||
                entrancePoint == null)
            {
                return;
            }

            switch (jobManager.State)
            {
                case FishNetTaxiJobState
                    .PassengerEnteringRequiredStop:
                    SetVisible(true);

                    passengerRoot.transform.position =
                        Vector3.Lerp(
                            roadsidePoint.position,
                            entrancePoint.position,
                            jobManager.StateProgress01);

                    FaceTravelDirection(
                        entrancePoint.position -
                        roadsidePoint.position);
                    break;

                case FishNetTaxiJobState
                    .PassengerReturningFromRequiredStop:
                    SetVisible(true);

                    passengerRoot.transform.position =
                        Vector3.Lerp(
                            entrancePoint.position,
                            roadsidePoint.position,
                            jobManager.StateProgress01);

                    FaceTravelDirection(
                        roadsidePoint.position -
                        entrancePoint.position);
                    break;

                default:
                    SetVisible(false);
                    break;
            }
        }

        private void FaceTravelDirection(
            Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            passengerRoot.transform.rotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);
        }

        private void SetVisible(bool visible)
        {
            if (passengerRoot.activeSelf != visible)
            {
                passengerRoot.SetActive(visible);
            }
        }

        private void Reset()
        {
            if (jobManager == null)
            {
                jobManager =
                    FindFirstObjectByType<
                        FishNetTaxiJobManager>();
            }
        }
    }
}
