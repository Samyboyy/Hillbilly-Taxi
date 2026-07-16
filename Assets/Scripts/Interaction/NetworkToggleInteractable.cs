using Unity.Netcode;
using UnityEngine;

namespace HillbillyTaxi.Interaction
{
    /// <summary>
    /// Small server-authoritative test target. Pressing Interact toggles one
    /// NetworkVariable, and every client renders the same state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkToggleInteractable : NetworkInteractable
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

        private NetworkVariable<bool> _isOn = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private MaterialPropertyBlock _propertyBlock;

        public bool IsOn => _isOn.Value;

        public override string GetPrompt(NetworkPlayerInteractor interactor)
        {
            return _isOn.Value ? turnOffPrompt : turnOnPrompt;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _isOn.OnValueChanged += HandleStateChanged;
            ApplyState(_isOn.Value);
        }

        public override void OnNetworkDespawn()
        {
            _isOn.OnValueChanged -= HandleStateChanged;
            base.OnNetworkDespawn();
        }

        protected override void InteractOnServer(
            NetworkPlayerInteractor interactor)
        {
            _isOn.Value = !_isOn.Value;
        }

        private void HandleStateChanged(bool previousValue, bool newValue)
        {
            ApplyState(newValue);
        }

        private void ApplyState(bool isOn)
        {
            if (targetRenderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(_propertyBlock);

            Color stateColor = isOn ? onColor : offColor;
            Material sharedMaterial = targetRenderer.sharedMaterial;

            if (sharedMaterial != null &&
                sharedMaterial.HasProperty(BaseColorProperty))
            {
                _propertyBlock.SetColor(
                    BaseColorProperty,
                    stateColor);
            }

            if (sharedMaterial != null &&
                sharedMaterial.HasProperty(ColorProperty))
            {
                _propertyBlock.SetColor(
                    ColorProperty,
                    stateColor);
            }

            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void Reset()
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        private void OnValidate()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            if (!Application.isPlaying)
            {
                ApplyState(false);
            }
        }
    }
}
