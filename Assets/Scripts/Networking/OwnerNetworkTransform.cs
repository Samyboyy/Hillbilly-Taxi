using Unity.Netcode.Components;

namespace HillbillyTaxi.Networking
{
    /// <summary>
    /// Gives the owning client transform authority for responsive prototype movement.
    /// Important gameplay state will still be validated by the server as those systems are added.
    /// </summary>
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
