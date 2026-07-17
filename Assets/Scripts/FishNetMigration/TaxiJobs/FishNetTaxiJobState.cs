namespace HillbillyTaxi.FishNetMigration.TaxiJobs
{
    public enum FishNetTaxiJobState
    {
        WaitingForPickup = 0,
        TravellingToRequiredStop = 1,
        PassengerEnteringRequiredStop = 2,
        WaitingAtRequiredStop = 3,
        PassengerReturningFromRequiredStop = 4,
        TravellingToDestination = 5,
        ContractComplete = 6,
        ContractFailed = 7
    }

    // Retained so the original Phase 6 installer and serialized scenes continue
    // to compile/load. Runtime objective matching now uses stable string IDs.
    public enum FishNetTaxiJobZoneType
    {
        Pickup = 0,
        Dropoff = 1
    }
}
