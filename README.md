# WIP

This mod is a work in progress with not estimates for fixes or general stability, you have been warned

# Texture Swapper (R.E.P.O.)

Texture Swapper is an in-depth Texture and more swapper mod to the game R.E.P.O designed to be expandable and easy to use. Such was based on [RandomPaintingSwap](https://github.com/GabziDev/RandomPaintingSwap) by GabziDev and credit to them for the orignal idea and code.

> ## Installation
### Manual Installation
1. Go to the root folder of your game.
2. Open the `BepInEx\plugins` folder.
3. Place the `TextureSwapper-x.dll` file of the plugin inside this folder.

### Adding Custom Images
1. In the `plugins` folder where you placed the `TextureSwapper-x.dll` file, create a folder named `texture_swapper_queries`.
2. Place your images inside this folder. These images will be used to randomly replace the paintings in the game.

Do note that the `texture_swapper_queries` also looks into each plugin installed and into the config folder as well.

### Supported Media Format

The following is the currently supported formats to swap textures and other assets with more to be added in the future:

#### Image Formats
- `.png`
- `.jpg/.jpeg`
- `.webp`
- `.gif` (Note that all gifs are converted to webm's as such provides better performance with playback and file size

#### Video Formats
- `.webm` (Only VP8 Codec works due to issues with Unity)
- `.mp4` (Check unity for supported codecs)

## Data Format Information

Info about the data format to making queries will be documented within the future.

### Future Plans
- Add support for APNG
- Check media format size to better match swapping of textures to texture loactions
- Allow for swapping textures onto other textures i.e. replace the fridge images without needing asset replacement
- Swap to using VLC or another media player to allow for more supported Codec and Media formats
- Documentation about default queries provided, the format for making query files and info about creating addons for services
- Add Support for Youtube and other Video Platforms

### Known issues
- Random network incompatibilities
- Some Image URL's are not valid due to how the URL pattern currently works
- Local files are copied to cache instead of being used directly
- Only a single instance of a VideoPlayer can occur
- Some swapped textures are UV / Sprite Sheeted, leading to issues with the images swapped to being cropped 
