using FishNet.Object.Synchronizing;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Interaction
{
    /// <summary>
    /// Small server-authoritative test object.
    /// Its SyncVar sends the latest state to current clients and clients which join
    /// after the switch has already been toggled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetToggleInteractable :
        FishNetInteractable
    {
        private static readonly int BaseColorProperty =
            Shader.PropertyToID("_BaseColor");

        private static readonly int ColorProperty =
            Shader.PropertyToID("_Color");

        [Header("Presentation")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private string turnOnPrompt = "Turn on";
        [SerializeField] private string turnOffPrompt = "Turn off";

        [SerializeField] private Color offColor =
            new Color(0.38f, 0.38f, 0.38f, 1f);

        [SerializeField] private Color onColor =
            new Color(0.25f, 0.85f, 0.35f, 1f);

        private readonly SyncVar<bool> _isOn = new();

        private MaterialPropertyBlock _propertyBlock;

        public bool IsOn => _isOn.Value;

        private void Awake()
        {
            _isOn.OnChange += HandleStateChanged;
        }

        private void OnDestroy()
        {
            _isOn.OnChange -= HandleStateChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ApplyState(_isOn.Value);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // OnChange is not required to fire for the initial spawn value, so apply
            // the already-synchronized value explicitly for late joiners.
            ApplyState(_isOn.Value);
        }

        public override string GetPrompt(
            FishNetPlayerInteractor interactor,
            int interactionId)
        {
            return _isOn.Value
                ? turnOffPrompt
                : turnOnPrompt;
        }

        protected override void InteractOnServer(
            FishNetPlayerInteractor interactor,
            int interactionId)
        {
            _isOn.Value = !_isOn.Value;
        }

        private void HandleStateChanged(
            bool previousValue,
            bool newValue,
            bool asServer)
        {
            ApplyState(newValue);
        }

        private void ApplyState(bool isOn)
        {
            if (targetRenderer == null)
            {
                return;
            }

            _propertyBlock ??=
                new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(
                _propertyBlock);

            Color stateColor =
                isOn ? onColor : offColor;

            Material sharedMaterial =
                targetRenderer.sharedMaterial;

            if (sharedMaterial != null &&
                sharedMaterial.HasProperty(
                    BaseColorProperty))
            {
                _propertyBlock.SetColor(
                    BaseColorProperty,
                    stateColor);
            }

            if (sharedMaterial != null &&
                sharedMaterial.HasProperty(
                    ColorProperty))
            {
                _propertyBlock.SetColor(
                    ColorProperty,
                    stateColor);
            }

            targetRenderer.SetPropertyBlock(
                _propertyBlock);
        }

        private void Reset()
        {
            targetRenderer =
                GetComponentInChildren<Renderer>();
        }

        private void OnValidate()
        {
            if (targetRenderer == null)
            {
                targetRenderer =
                    GetComponentInChildren<Renderer>();
            }

            if (!Application.isPlaying)
            {
                ApplyState(false);
            }
        }
    }
}
