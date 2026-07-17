#!/usr/bin/env bash
set -euo pipefail

EXPECTED_BRANCH="networking/fishnet-migration"
CURRENT_BRANCH="$(git branch --show-current)"

if [[ "$CURRENT_BRANCH" != "$EXPECTED_BRANCH" ]]; then
    echo "ERROR: This script must run on $EXPECTED_BRANCH."
    echo "Current branch: $CURRENT_BRANCH"
    exit 1
fi

if [[ ! -f "Assets/FishNetMigration/PlayerBase.prefab" ]]; then
    echo "ERROR: Assets/FishNetMigration/PlayerBase.prefab is missing."
    echo "Open Unity and run:"
    echo "Hillbilly Taxi > FishNet Migration > Prepare Framework-Neutral Assets"
    exit 1
fi

if [[ ! -f "Assets/Scenes/FishNetProof.unity" ]]; then
    echo "ERROR: Assets/Scenes/FishNetProof.unity is missing."
    exit 1
fi

echo "Removing leftover failed-migration metadata..."
rm -f Assets/Scripts/FishNetMigration.meta

echo "Removing NGO-dependent project scripts from the migration branch..."

NGO_FILES=(
    "Assets/Scripts/Networking/DevelopmentNetworkLauncher.cs"
    "Assets/Scripts/Networking/DevelopmentNetworkLauncher.cs.meta"
    "Assets/Scripts/Networking/NetworkPlayerSpawnManager.cs"
    "Assets/Scripts/Networking/NetworkPlayerSpawnManager.cs.meta"
    "Assets/Scripts/Networking/OwnerNetworkTransform.cs"
    "Assets/Scripts/Networking/OwnerNetworkTransform.cs.meta"

    "Assets/Scripts/Interaction/NetworkInteractable.cs"
    "Assets/Scripts/Interaction/NetworkInteractable.cs.meta"
    "Assets/Scripts/Interaction/NetworkInteractionPoint.cs"
    "Assets/Scripts/Interaction/NetworkInteractionPoint.cs.meta"
    "Assets/Scripts/Interaction/NetworkPlayerInteractor.cs"
    "Assets/Scripts/Interaction/NetworkPlayerInteractor.cs.meta"
    "Assets/Scripts/Interaction/NetworkToggleInteractable.cs"
    "Assets/Scripts/Interaction/NetworkToggleInteractable.cs.meta"

    "Assets/Scripts/Player/NetworkPlayerCharacter.cs"
    "Assets/Scripts/Player/NetworkPlayerCharacter.cs.meta"
    "Assets/Scripts/Player/NetworkPlayerSeatController.cs"
    "Assets/Scripts/Player/NetworkPlayerSeatController.cs.meta"
    "Assets/Scripts/Player/NetworkSeatState.cs"
    "Assets/Scripts/Player/NetworkSeatState.cs.meta"

    "Assets/Scripts/Vehicles/NetworkVehicle.cs"
    "Assets/Scripts/Vehicles/NetworkVehicle.cs.meta"
    "Assets/Scripts/Vehicles/NetworkTruckMotor.cs"
    "Assets/Scripts/Vehicles/NetworkTruckMotor.cs.meta"

    "Assets/Scripts/Editor/InteractionFoundationInstaller.cs"
    "Assets/Scripts/Editor/InteractionFoundationInstaller.cs.meta"
    "Assets/Scripts/Editor/VehicleSeatFoundationInstaller.cs"
    "Assets/Scripts/Editor/VehicleSeatFoundationInstaller.cs.meta"
    "Assets/Scripts/Editor/NetworkTruckControllerInstaller.cs"
    "Assets/Scripts/Editor/NetworkTruckControllerInstaller.cs.meta"

    "Assets/DefaultNetworkPrefabs.asset"
    "Assets/DefaultNetworkPrefabs.asset.meta"
)

for path in "${NGO_FILES[@]}"; do
    if git ls-files --error-unmatch "$path" >/dev/null 2>&1; then
        git rm -f "$path"
    else
        rm -f "$path"
    fi
done

echo "Removing Netcode for GameObjects from Packages/manifest.json..."

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command '
$path = "Packages/manifest.json"
$json = Get-Content -Raw $path | ConvertFrom-Json
$json.dependencies.PSObject.Properties.Remove("com.unity.netcode.gameobjects")
$json | ConvertTo-Json -Depth 100 | Set-Content -Encoding UTF8 $path
'

# Let Unity rebuild this from the new manifest.
rm -f Packages/packages-lock.json

echo "Clearing Unity package/code-generation caches..."
rm -rf Library Temp obj

echo
echo "NGO has been removed from the migration branch working tree."
echo "Next:"
echo "1. Open Unity and wait for a clean compile."
echo "2. Do not open SampleScene; use FishNetProof."
echo "3. Install FishNet with:"
echo "   https://github.com/FirstGearGames/FishNet.git?path=Assets/FishNet#4.7.2"
echo
git status
