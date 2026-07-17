namespace HillbillyTaxi.FishNetMigration.TaxiJobs
{
    public enum FishNetTaxiJobState
    {
        WaitingForPickup = 0,
        PassengerAboard = 1,
        JobComplete = 2
    }

    public enum FishNetTaxiJobZoneType
    {
        Pickup = 0,
        Dropoff = 1
    }
}
