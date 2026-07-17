#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
#define HILLBILLY_STEAM_SUPPORTED
#endif

using System;
using UnityEngine;

#if HILLBILLY_STEAM_SUPPORTED
using Steamworks;
#endif

namespace HillbillyTaxi.FishNetMigration.Steam
{
    /// <summary>
    /// Minimal Steamworks.NET lifecycle for the Steam transport proof.
    ///
    /// FishySteamworks supplies FishNet's transport but deliberately does not own
    /// the SteamAPI lifecycle. This component initializes Steam once, pumps
    /// callbacks, exposes the local identity, and shuts Steam down once.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class HillbillyTaxiSteamBootstrap : MonoBehaviour
    {
        private static HillbillyTaxiSteamBootstrap _instance;
        private static bool _everInitialized;

        [SerializeField] private bool initializeOnAwake = true;

        public bool Initialized { get; private set; }
        public string PersonaName { get; private set; } = "Unavailable";
        public ulong SteamId { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        public string IdentityText =>
            Initialized
                ? $"{PersonaName} ({SteamId})"
                : "Steam unavailable";

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _everInitialized = false;
        }

        private void Awake()
        {
            if (_instance != null &&
                _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;

            if (initializeOnAwake)
            {
                InitializeSteam();
            }
        }

        public bool InitializeSteam()
        {
            if (Initialized)
            {
                return true;
            }

#if !HILLBILLY_STEAM_SUPPORTED
            LastError =
                "Steam is not supported on this build target.";

            return false;
#else
            if (_everInitialized)
            {
                LastError =
                    "SteamAPI was already initialized and shut down " +
                    "during this process.";

                Debug.LogError(LastError, this);
                return false;
            }

            _everInitialized = true;

            if (!Packsize.Test())
            {
                LastError =
                    "Steamworks.NET Packsize test failed. The managed " +
                    "and native Steamworks versions may not match.";

                Debug.LogError(LastError, this);
                return false;
            }

            if (!DllCheck.Test())
            {
                LastError =
                    "Steamworks.NET DLL check failed. A native Steam " +
                    "library is missing or has the wrong version.";

                Debug.LogError(LastError, this);
                return false;
            }

            try
            {
                Initialized = SteamAPI.Init();
            }
            catch (DllNotFoundException exception)
            {
                LastError =
                    "SteamAPI native library was not found: " +
                    exception.Message;

                Debug.LogError(LastError, this);
                return false;
            }
            catch (Exception exception)
            {
                LastError =
                    "SteamAPI initialization threw an exception: " +
                    exception.Message;

                Debug.LogError(LastError, this);
                return false;
            }

            if (!Initialized)
            {
                LastError =
                    "SteamAPI.Init returned false. Keep Steam open, " +
                    "logged in, and verify steam_appid.txt is beside " +
                    "the executable and contains 480.";

                Debug.LogError(LastError, this);
                return false;
            }

            SteamId =
                SteamUser.GetSteamID().m_SteamID;

            PersonaName =
                SteamFriends.GetPersonaName();

            LastError = string.Empty;

            Debug.Log(
                $"Steam initialized as {PersonaName} ({SteamId}).",
                this);

            return true;
#endif
        }

        private void Update()
        {
#if HILLBILLY_STEAM_SUPPORTED
            if (!Initialized)
            {
                return;
            }

            SteamAPI.RunCallbacks();
#endif
        }

        public void ShutdownSteam()
        {
#if HILLBILLY_STEAM_SUPPORTED
            if (!Initialized)
            {
                return;
            }

            SteamAPI.Shutdown();
#endif

            Initialized = false;
            SteamId = 0;
            PersonaName = "Unavailable";
        }

        private void OnApplicationQuit()
        {
            ShutdownSteam();
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            ShutdownSteam();
            _instance = null;
        }
    }
}
