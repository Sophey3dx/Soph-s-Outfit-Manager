# Soph's Outfit Manager

A VRChat avatar outfit management tool for Unity that allows you to save and load outfit presets with automatic VRChat asset generation.

## Features

- **6 Outfit Slots**: Save up to 6 different outfit configurations
- **Editor Preview**: Preview outfits directly in the Unity Editor
- **Auto Icon Rendering**: Generate menu icons with Editor camera
- **Custom Menu Icon**: Includes a beautiful icon for the VRChat Expression Menu
- **Automatic Asset Generation**: Generates all required VRChat assets with one click:
  - Animation Clips for each outfit
  - FX Animator Layer with proper transitions
  - Expression Parameters (synced Int)
  - Expressions Menu with radial selector and individual buttons
- **VRChat SDK 3 Compatible**: Uses only VRChat-safe methods (GameObject toggles via AnimationClips)
- **No Runtime Scripts**: All logic is baked into the animator - fully upload-safe

## Requirements

- Unity 2022.3 LTS
- VRChat Creator Companion (VCC)
- VRChat Avatars SDK 3.5.0 or higher

## Installation

### Via VCC (Recommended)

1. **[Click here to add to VCC](vcc://vpm/addRepo?url=https%3A%2F%2Fsophey3dx.github.io%2FSoph-s-Outfit-Manager%2Findex.json)**

Or manually:
1. Open VRChat Creator Companion
2. Go to **Settings** → **Packages** → **Add Repository**
3. Enter: `https://sophey3dx.github.io/Soph-s-Outfit-Manager/index.json`
4. Open your avatar project and add "Soph's Outfit Manager"

### Manual Installation

1. Download the latest `.zip` from [Releases](https://github.com/Sophey3dx/Soph-s-Outfit-Manager/releases)
2. Extract to your project's `Packages` folder

## Usage

### Opening the Tool

Go to **Tools > Soph's Outfit Manager** in the Unity menu bar.

### Setup

1. **Avatar**: Drag your avatar (with VRC Avatar Descriptor) into the Avatar field
2. **Outfit Root**: Select the parent GameObject that contains all your outfit items
3. **Slot Data**: Click "Create New Slot Data" to create a data file
4. **Output Folder**: Choose where generated assets will be saved

### Saving Outfits

1. In your Scene, enable/disable the outfit GameObjects to create your desired look
2. Select a slot (0-5) in the Outfit Slots section
3. Give your slot a name (e.g., "Casual", "Formal", "Beach")
4. Click **"Save Current Visibility to Slot"**
5. Optionally click **"Render Icon"** to create a preview icon
6. Repeat for each outfit

### Generating VRChat Assets

1. Ensure you have at least one slot configured
2. Click **"Generate VRChat Assets"**
3. Upload your avatar!

### Using In-Game

1. Open your Expression Menu
2. Navigate to "Outfits"
3. Use the radial dial or individual buttons to switch outfits

## Technical Details

### Parameter Usage

| Parameter | Type | Cost | Saved | Synced |
|-----------|------|------|-------|--------|
| OutfitIndex | Int | 8 bits | Yes | Yes |

## License

MIT License - Free to use, modify, and distribute.

## Credits

Created by Soph / Sophey3dx

---

*Compatible with VRChat SDK 3 and VRChat Creator Companion.*
