# CustomPaintings

This mod replaces all paintings with your custom images.

> *This is a performance-optimized fork of [original CustomPaintings](https://github.com/LittleHund/CustomPaintingsMod).*

## How to Use

1. Create or use a folder named `CustomPaintings` within the plugins or config folder (subfolders also work).
   - **For sharing profile codes, the config folder is recommended.**
2. Place any `.png`, `.jpg`, or `.jpeg` files in this folder and play.

**Important:** For multiplayer sync, everyone needs the exact same images in the same location with the same filename.

This works with multiple folders in different locations. A PNG and JPEG converter with instructions is available on the GitHub page.

## Fork Changes

Replaced global pre-loading with on-demand texture loading. With 600+ images, RAM usage stays under 100 MB compared to 10 GB+ in the original.

**What changed:**
- Removed the startup pre-loading step that caused extreme memory spikes with large image libraries.
- Textures are now loaded only after the level is loaded, reducing memory allocations during processing.

## Latest Update

**Update 1.1.13**
- Added all new paintings from the museum update.

## Discord

For questions and ideas, join the Discord:

[![Discord](https://imgur.com/f0OHQHx.png)](https://discord.gg/FB4KmrdgPr)

## Support

Support the original creator of this mod:

[![Ko-fi](https://i.imgur.com/jzwECeF.png)](https://Ko-fi.com/littlehund)

## Inspiration

v1.0.0 of this mod was inspired by [RandomPaintingSwap](https://thunderstore.io/c/repo/p/GabzDEV/RandomPaintingSwap/).
