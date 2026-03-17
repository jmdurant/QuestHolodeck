# Environment Skyboxes

Drop skybox materials here. EnvironmentManager loads them by name from Resources.

## How to add a new environment:

1. Get a 360 equirectangular image (from Blockade Labs, Scaniverse, stock photos, etc.)
2. Import into Unity (Assets → Import New Asset)
3. In Inspector: Texture Shape → Cube or 2D, Wrap Mode → Clamp
4. Create Material: Shader → Skybox/Panoramic (for equirectangular) or Skybox/6 Sided
5. Assign texture to material
6. Name the material: Skybox_Mountain, Skybox_Beach, etc.
7. Drop into this folder (Assets/Resources/Environments/)
8. EnvironmentManager.DefaultPresets() already references these names

## Naming convention:

- Skybox_Mountain — starry mountain night
- Skybox_Beach — ocean sunset
- Skybox_Hotel — luxury suite with city lights
- Skybox_Cabin — mountain cabin with fireplace
- Skybox_Cave — candlelit stone cave

## AI-generated skyboxes:

1. Go to skybox.blockadelabs.com
2. Type: "luxury hotel suite, king bed, warm lighting, romantic, night"
3. Download equirectangular JPEG
4. Import → Material → drop here

## Agent control:

The AI agent can switch environments via:
  set_environment(skybox="beach")
  set_environment(skybox="passthrough")
  set_environment(skybox="void")
