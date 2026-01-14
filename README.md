# Soph's Outfit Manager

A VRChat avatar outfit management tool for Unity that allows you to save and load outfit presets with automatic VRChat asset generation.

## ✨ Features

- **6 Outfit Slots**: Save up to 6 different outfit configurations
- **🪄 Wizard Mode**: Step-by-step setup guide for first-time users
- **🖱️ One-Click Setup**: Just drag your avatar into the window!
- **🖼️ Visual Slot Grid**: Large preview cards with icons instead of small buttons
- **🏷️ Preset Names**: Auto-fill slot names (Casual, Formal, etc.)
- **📸 Auto Icon Rendering**: Generate menu icons with Editor camera
- **🎨 Custom Menu Icon**: Beautiful icon for the VRChat Expression Menu
- **⚡ Automatic Asset Generation**: Generates all required VRChat assets with one click:
  - Animation Clips for each outfit (ON/OFF toggles)
  - FX Animator Layer with proper transitions
  - Expression Parameters (synced Int)
  - Expressions Menu with radial selector and individual buttons
- **🔍 Auto-Detect**: Automatically finds outfit folders
- **VRChat SDK 3 Compatible**: Uses only VRChat-safe methods (GameObject toggles via AnimationClips)
- **No Runtime Scripts**: All logic is baked into the animator - fully upload-safe

## Requirements

- Unity 2022.3 LTS
- VRChat Creator Companion (VCC)
- VRChat Avatars SDK 3.5.0 or higher

## Installation

### Via VCC (Recommended)

**[Click here to add to VCC](vcc://vpm/addRepo?url=https%3A%2F%2Fsophey3dx.github.io%2FSoph-s-Outfit-Manager%2Findex.json)**

Or manually:
1. Open VRChat Creator Companion
2. Go to **Settings** → **Packages** → **Add Repository**
3. Enter: `https://sophey3dx.github.io/Soph-s-Outfit-Manager/index.json`
4. Open your avatar project and add "Soph's Outfit Manager"

### Manual Installation

1. Download the latest `.zip` from [Releases](https://github.com/Sophey3dx/Soph-s-Outfit-Manager/releases)
2. Extract to your project's `Packages` folder

## Quick Start

### Option 1: One-Click Setup (Easiest)
1. Open **Tools > Soph's Outfit Manager**
2. **Drag your avatar** from the Hierarchy into the window
3. Done! The tool auto-detects everything

### Option 2: Setup Wizard
1. Open **Tools > Soph's Outfit Manager**
2. Follow the 3-step wizard:
   - **Step 1**: Select your avatar
   - **Step 2**: Confirm or create outfit folder
   - **Step 3**: Apply preset names (optional)

### Option 3: Manual Setup
1. **Avatar**: Drag avatar (with VRC Avatar Descriptor) into the field
2. **Outfit Root**: Click "Auto" to auto-detect, or select manually
3. Click "+" to create an "Outfits" folder if needed

## Usage

### Saving Outfits

1. In your Scene, enable/disable clothing items to create your desired look
2. Click on a slot card (0-5) in the visual grid
3. Give it a name (e.g., "Casual", "Formal", "Beach")
4. Click **"Save Current Visibility to Slot"**
5. The slot card will turn green with a checkmark
6. Repeat for other outfits

**Tip**: Click **"Apply Preset Names"** to auto-fill: Default, Casual, Formal, Sporty, Beach, Night Out

### Previewing Outfits

1. Click on a configured slot (green card)
2. Click **"Load Slot in Editor"**
3. The outfit visibility will be applied in the Scene view

### Rendering Icons

**Single Icon:**
1. Select a configured slot
2. Load the outfit in editor
3. Position avatar in Scene view
4. Click **"Render Icon"**

**All Icons:**
1. Expand **"Icon Camera Settings"**
2. Choose preset: Portrait, Full Body, or 3/4 View
3. Click **"Render All Icons"**

### Generating VRChat Assets

1. Ensure at least one slot is configured
2. Set output folder (default: `Assets/AvatarOutfitManager/Generated`)
3. Click **"Generate VRChat Assets"**

**What gets created:**
- `Animations/` folder with 6 animation clips
- `FX_OutfitManager.controller` (or updates existing FX controller)
- `ExpressionParameters_OutfitManager.asset` (or updates existing)
- `Menu_Outfits.asset` with outfit selection menu

### Using In-Game

1. Upload your avatar to VRChat
2. Open your Expression Menu
3. Navigate to **"Outfits"** submenu
4. Use the **radial dial** to scroll through outfits
5. Or click **toggle buttons** for direct selection
6. Your selection is saved and synced with other players

## Technical Details

### Parameter Usage

| Parameter | Type | Cost | Saved | Synced |
|-----------|------|------|-------|--------|
| OutfitIndex | Int | 8 bits | Yes | Yes |

### How It Works

- Each outfit is an **AnimationClip** that toggles GameObjects on/off
- The **FX Animator Layer** uses `OutfitIndex` parameter to select which outfit is active
- **Any State transitions** ensure instant switching between outfits
- **Write Defaults is OFF** (VRChat best practice)

## Troubleshooting

### "Outfit Root must be a child of the Avatar"
- Make sure your Outfits folder is directly under the avatar GameObject
- Not under Armature, Body, or other child objects

### "No outfit slots are configured"
- You need to save at least one outfit before generating assets
- Click "Save Current Visibility to Slot" first

### Outfits not switching in-game
- Check that FX Controller is assigned in Avatar Descriptor
- Verify Expression Parameters are assigned
- Make sure Expressions Menu is assigned
- Check that OutfitIndex parameter exists (should be Int, 0-5)
- Verify AnimationClips contain curves for all outfit objects

### Icons not showing in menu
- Icons are optional - the menu works without them
- Make sure you rendered icons before generating assets
- Icons must be in the Icons folder within your output directory

## Documentation

- **[Complete Tutorial](TUTORIAL.md)** - Step-by-step guide with screenshots
- **[GitHub Repository](https://github.com/Sophey3dx/Soph-s-Outfit-Manager)** - Source code and issues
- **[Releases](https://github.com/Sophey3dx/Soph-s-Outfit-Manager/releases)** - Download older versions

## Changelog

### v1.2.0
- Added dedicated "Soph Outfit Manager" GameObject in hierarchy
- Improved hierarchy organization (similar to GoGo Loco or SPS)
- Auto-detection of existing "Soph Outfit Manager" GameObject
- Updated UI buttons and wizard flow

### v1.1.1
- Fixed FX Layer setup for reliable outfit switching
- Improved animation clip generation
- Added debug logging

### v1.1.0
- Major UX overhaul
- Wizard mode for first-time users
- Visual slot grid with icons
- One-click setup (drag & drop)
- Preset slot names
- Tooltips on all UI elements
- Auto-detect outfit folders

### v1.0.0
- Initial release
- 6 outfit slots
- Auto icon rendering
- VRChat asset generation

## License

MIT License - Free to use, modify, and distribute.

## Credits

Created by **Soph / Sophey3dx**

---

*Compatible with VRChat SDK 3 and VRChat Creator Companion.*
