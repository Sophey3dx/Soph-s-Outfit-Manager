# Soph's Outfit Manager - Complete Tutorial

## Installation

### Step 1: Add to VCC
1. Open **VRChat Creator Companion**
2. Go to **Settings** → **Packages** → **Add Repository**
3. Enter: `https://sophey3dx.github.io/Soph-s-Outfit-Manager/index.json`
4. Click **Add**

### Step 2: Add to Your Project
1. Open your avatar project in Unity
2. Go to **Manage Project** in VCC
3. Find **"Soph's Outfit Manager"** in the list
4. Click **Add to Project**

---

## First Time Setup

### Opening the Tool
Go to **Tools > Soph's Outfit Manager** in Unity's menu bar.

### Quick Setup (Recommended)

**Option 1: Drag & Drop**
- Simply drag your avatar from the Hierarchy into the Outfit Manager window
- The tool will automatically:
  - Detect your VRC Avatar Descriptor
  - Find or create an "Outfits" folder
  - Set everything up for you!

**Option 2: Setup Wizard**
- If you see the wizard, follow the 3 steps:
  1. **Select Avatar**: Drag avatar or choose from scene
  2. **Outfit Folder**: Confirm auto-detected folder or create new one
  3. **Ready**: Optionally apply preset names (Casual, Formal, etc.)

**Option 3: Manual Setup**
- **Avatar**: Drag your avatar (with VRC Avatar Descriptor) into the field
- **Outfit Root**: Click "Auto" to auto-detect, or select manually
- If no folder exists, click "+" to create an "Outfits" folder

---

## Organizing Your Outfits

### Preparing Your Avatar

Before using the tool, organize your clothing items:

1. **Create an Outfits Folder** (if you don't have one):
   - Under your avatar, create an empty GameObject named "Outfits" or "Clothing"
   - Drag all your clothing/accessory GameObjects into this folder

2. **Example Structure**:
   ```
   Avatar
   ├── Armature
   ├── Body
   └── Outfits          ← Your outfit root
       ├── Shirt_Casual
       ├── Shirt_Formal
       ├── Pants_Jeans
       ├── Pants_Shorts
       ├── Jacket
       └── Accessories
   ```

---

## Saving Outfits

### Step 1: Configure Your Outfit in Scene
1. In the Unity Scene view, enable/disable clothing items to create your desired look
2. For example, for "Casual" outfit:
   - Enable: Shirt_Casual, Pants_Jeans
   - Disable: Shirt_Formal, Pants_Shorts, Jacket

### Step 2: Save to Slot
1. In the Outfit Manager window, click on a slot card (0-5)
2. The slot will be highlighted in blue
3. Give it a name (e.g., "Casual", "Formal", "Beach")
4. Click **"Save Current Visibility to Slot"**
5. The slot card will turn green with a checkmark

### Step 3: Repeat for Other Outfits
- Configure different items in the scene
- Select a different slot
- Click "Save Current Visibility" again
- Repeat until all 6 slots are filled (or as many as you need)

### Using Preset Names
- Click **"Apply Preset Names"** button above the slot grid
- This automatically names slots: Default, Casual, Formal, Sporty, Beach, Night Out
- You can still edit names individually

---

## Previewing Outfits

### Load Outfit in Editor
1. Click on a configured slot (green card)
2. Click **"Load Slot in Editor"**
3. The outfit visibility will be applied in the Scene view
4. Perfect for checking how outfits look before generating assets!

---

## Rendering Icons

### Single Slot Icon
1. Select a configured slot
2. Make sure the outfit is loaded in the scene (click "Load Slot in Editor")
3. Position your avatar in the Scene view (front-facing works best)
4. Click **"Render Icon"**
5. The icon will appear in the slot card

### Render All Icons
1. Expand **"Icon Camera Settings"** section
2. Choose a preset: Portrait, Full Body, or 3/4 View
3. Adjust camera settings if needed (Distance, Height, FOV)
4. Click **"Render All Icons"**
5. Icons will be generated for all configured slots

**Tips for Good Icons:**
- Use Portrait preset for close-up shots
- Use Full Body for showing complete outfits
- Position avatar facing camera in Scene view
- Icons are 256x256 pixels

---

## Generating VRChat Assets

### Before Generating
Make sure you have:
- ✅ At least one slot configured
- ✅ Avatar and Outfit Root assigned
- ✅ Output folder path set (default: Assets/AvatarOutfitManager/Generated)

### Generate Assets
1. Click **"Generate VRChat Assets"** button
2. The tool will create:
   - **Animation Clips**: One for each outfit slot
   - **FX Animator Layer**: Controls outfit switching
   - **Expression Parameters**: Adds "OutfitIndex" parameter
   - **Expressions Menu**: Creates "Outfits" submenu with radial dial and toggle buttons

### What Gets Created
- `Animations/` folder with 6 animation clips
- `FX_OutfitManager.controller` (or updates existing FX controller)
- `ExpressionParameters_OutfitManager.asset` (or updates existing)
- `Menu_Outfits.asset` with your outfit selection menu

---

## Using In VRChat

### In-Game Menu
1. Upload your avatar to VRChat
2. Open your Expression Menu (usually right-click or gesture)
3. Navigate to **"Outfits"** submenu
4. You'll see:
   - **Radial Dial**: Scroll through outfits smoothly
   - **Toggle Buttons**: Click directly on outfit names (Casual, Formal, etc.)

### How It Works
- Each outfit toggle sets the `OutfitIndex` parameter (0-5)
- The FX Animator Layer switches between animation clips
- Animation clips toggle GameObjects on/off
- Your selection is saved and synced with other players

---

## Tips & Best Practices

### Organization
- **Name your slots clearly**: "Casual", "Formal", "Beach" are better than "Outfit 1", "Outfit 2"
- **Group related items**: Put all shirt variants in one folder, pants in another
- **Use descriptive names**: "Shirt_Casual" is better than "Shirt1"

### Performance
- **Limit outfit count**: 6 slots is the maximum (VRChat parameter limit)
- **Optimize meshes**: Make sure clothing items are optimized before uploading
- **Test in-game**: Always test outfit switching after uploading

### Troubleshooting

**"Outfit Root must be a child of the Avatar"**
- Make sure your Outfits folder is directly under the avatar GameObject
- Not under Armature, Body, or other child objects

**"No outfit slots are configured"**
- You need to save at least one outfit before generating assets
- Click "Save Current Visibility to Slot" first

**Icons not showing in menu**
- Icons are optional - the menu works without them
- Make sure you rendered icons before generating assets
- Icons must be in the Icons folder within your output directory

**Outfits not switching in-game**
- Check that FX Controller is assigned in Avatar Descriptor
- Verify Expression Parameters are assigned
- Make sure Expressions Menu is assigned
- Check that OutfitIndex parameter exists (should be Int, 0-5)

---

## Advanced Features

### Custom Camera Settings
- Expand "Icon Camera Settings" to customize:
  - **Distance**: How far camera is from avatar
  - **Height**: Vertical offset
  - **FOV**: Field of view (wider = more of avatar visible)
  - **Rotation**: Angle around avatar

### Manual Slot Management
- **Clear Slot**: Removes saved outfit data
- **Load in Editor**: Preview without generating assets
- **Render Icon**: Create custom preview image

### Multiple Avatars
- Each avatar can have its own Slot Data asset
- Create separate Slot Data for different avatars
- Output folders are separate per avatar

---

## Quick Reference

| Action | How To |
|--------|--------|
| **Open Tool** | Tools > Soph's Outfit Manager |
| **Quick Setup** | Drag avatar into window |
| **Save Outfit** | Configure items → Select slot → "Save Current Visibility" |
| **Preview** | Select slot → "Load Slot in Editor" |
| **Render Icon** | Select slot → "Render Icon" |
| **Generate Assets** | Click "Generate VRChat Assets" |
| **In-Game** | Expression Menu → Outfits → Select outfit |

---

## Support

If you encounter issues:
- Check the Unity Console for error messages
- Verify all requirements are met (Unity 2022.3, VRChat SDK 3.5.0+)
- Make sure avatar has VRC Avatar Descriptor component
- Ensure Outfit Root is a child of the avatar

For more help, visit: [GitHub Repository](https://github.com/Sophey3dx/Soph-s-Outfit-Manager)

---

**Made with 💜 by Soph**
