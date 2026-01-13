# Soph's Outfit Manager

A VRChat avatar outfit management tool for Unity that allows you to save and load outfit presets with automatic VRChat asset generation.

## Features

- **6 Outfit Slots**: Save up to 6 different outfit configurations
- **Editor Preview**: Preview outfits directly in the Unity Editor
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

1. Open VRChat Creator Companion
2. Go to **Settings** → **Packages** → **Add Repository**
3. Add the repository URL where this package is hosted
4. Open your avatar project
5. Go to **Manage Project** and add "Soph's Outfit Manager"

### Manual Installation

1. Download the package folder `com.soph.avatar-outfit-manager`
2. Copy it to your project's `Packages` folder
3. Unity will automatically import the package

## Usage

### Opening the Tool

Go to **Tools > Soph's Outfit Manager** in the Unity menu bar.

### Setup

1. **Avatar**: Drag your avatar (with VRC Avatar Descriptor) into the Avatar field
2. **Outfit Root**: Select the parent GameObject that contains all your outfit items
   - This is typically a folder like "Clothing" or "Outfits" under your avatar
3. **Slot Data**: Click "Create New Slot Data" to create a data file, or assign an existing one
4. **Output Folder**: Choose where generated assets will be saved

### Saving Outfits

1. In your Scene, enable/disable the outfit GameObjects to create your desired look
2. Select a slot (0-5) in the Outfit Slots section
3. Give your slot a name (e.g., "Casual", "Formal", "Beach")
4. Click **"Save Current Visibility to Slot"**
5. Repeat for each outfit you want to save

### Previewing Outfits

1. Select a configured slot
2. Click **"Load Slot in Editor"**
3. The outfit visibility will be applied in the Scene view

### Generating VRChat Assets

1. Ensure you have at least one slot configured
2. Click **"Generate VRChat Assets"**
3. The tool will create:
   - `Animations/` folder with animation clips
   - FX Controller with OutfitManager layer
   - Expression Parameters with OutfitIndex
   - Expressions Menu with Outfits submenu

### Using In-Game

After uploading your avatar:

1. Open your Expression Menu
2. Navigate to "Outfits"
3. Use the radial dial or individual buttons to switch outfits
4. Your selection is saved and synced with other players

## Technical Details

### How It Works

- Each outfit is represented by an **AnimationClip** that toggles GameObjects on/off
- The **FX Animator Layer** uses an Int parameter (`OutfitIndex`) to select which outfit is active
- **Any State transitions** ensure instant switching between outfits
- **Write Defaults is OFF** following VRChat best practices

### VRChat Limitations

- Runtime scripts cannot be attached to avatars
- All outfit logic must be baked into the Animator and AnimationClips
- This tool only works in the Unity Editor - it generates assets for VRChat to use at runtime

### Parameter Usage

| Parameter | Type | Cost | Saved | Synced |
|-----------|------|------|-------|--------|
| OutfitIndex | Int | 8 bits | Yes | Yes |

## Troubleshooting

### "Outfit Root must be a child of the Avatar"
Make sure the Outfit Root you selected is actually inside your avatar hierarchy.

### Objects not toggling in-game
- Verify that the objects are direct or indirect children of the Outfit Root
- Check that the FX Controller is properly assigned in the Avatar Descriptor
- Ensure the Expression Parameters and Menu are assigned

### Parameter conflicts
If you already have an "OutfitIndex" parameter, the tool will update it rather than creating a duplicate.

## License

MIT License - Free to use, modify, and distribute.

## Credits

Created by Soph

---

*This tool was designed to work with VRChat SDK 3 and the VRChat Creator Companion.*
