#if UNITY_EDITOR
using FishNet.Managing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetSteamPhaseEightHotfixInstaller
    {
        private const string CameraName =
            "Hillbilly Taxi Frame Clear Camera";

        [MenuItem(
            "Hillbilly Taxi/Steam/Phase 8/Install Host Loss And Overlay Fix")]
        public static void InstallHostLossAndOverlayFix()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before installing the Phase 8 hotfix.");

                return;
            }

            NetworkManager networkManager =
                Object.FindFirstObjectByType<NetworkManager>();

            if (networkManager == null)
            {
                Debug.LogError(
                    "No FishNet NetworkManager exists in the active scene.");

                return;
            }

            Transform existing =
                networkManager.transform.Find(CameraName);

            GameObject cameraObject;

            if (existing == null)
            {
                cameraObject =
                    new GameObject(CameraName);

                Undo.RegisterCreatedObjectUndo(
                    cameraObject,
                    "Create Hillbilly Taxi Frame Clear Camera");

                cameraObject.transform.SetParent(
                    networkManager.transform,
                    false);
            }
            else
            {
                cameraObject =
                    existing.gameObject;
            }

            Camera frameClearCamera =
                cameraObject.GetComponent<Camera>();

            if (frameClearCamera == null)
            {
                frameClearCamera =
                    Undo.AddComponent<Camera>(
                        cameraObject);
            }

            Undo.RecordObject(
                frameClearCamera,
                "Configure Hillbilly Taxi Frame Clear Camera");

            frameClearCamera.enabled = true;
            frameClearCamera.clearFlags =
                CameraClearFlags.SolidColor;

            frameClearCamera.backgroundColor =
                Color.black;

            frameClearCamera.cullingMask = 0;
            frameClearCamera.depth = -1000f;
            frameClearCamera.orthographic = true;
            frameClearCamera.orthographicSize = 1f;
            frameClearCamera.nearClipPlane = 0.01f;
            frameClearCamera.farClipPlane = 1f;
            frameClearCamera.allowHDR = false;
            frameClearCamera.allowMSAA = false;
            frameClearCamera.useOcclusionCulling = false;
            frameClearCamera.targetDisplay = 0;

            cameraObject.transform.localPosition =
                Vector3.zero;

            cameraObject.transform.localRotation =
                Quaternion.identity;

            cameraObject.transform.localScale =
                Vector3.one;

            EditorUtility.SetDirty(cameraObject);
            EditorUtility.SetDirty(frameClearCamera);
            EditorUtility.SetDirty(networkManager.gameObject);

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    activeScene);
            }

            Selection.activeGameObject =
                cameraObject;

            Debug.Log(
                "Installed the Phase 8 host-loss and Steam-overlay frame " +
                "clear fix. Save FishNetProof.unity.",
                cameraObject);
        }
    }
}
#endif
