using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace CustomPaintings
{
    public class CP_Loader
    {
        //assign dedicated folder name
        private const string IMAGE_FOLDER_NAME = "CustomPaintings";

        private readonly CP_Logger logger;
        private readonly CP_GifVidManager GifVidManager;

        //create a dictionary for the different image groups
        public Dictionary<string, List<Material>> MaterialGroups = new Dictionary<string, List<Material>>();

        // Loaded materials list
        public List<Material> LoadedMaterials { get; } = new List<Material>();

        // Grunge materials (Seperate materials to avoid stretching
        private const string GRUNGE_ASSET_BUNDLE           = "GrungeAssets";
        private const string MATERIAL_LANDSCAPE_ASSET_NAME = "GrungeHorizontalMaterial";
        private const string MATERIAL_PORTRAIT_ASSET_NAME  = "GrungeVerticalMaterial";
        static Material _LandscapeMaterial;
        static Material _PortraitMaterial;

        private int loadedcount = 1;

        // Constructor to initialize the logger
        public CP_Loader(CP_Logger logger, CP_GifVidManager GifVidManager)
        {
            this.logger = logger;
            this.GifVidManager = GifVidManager;
            logger.LogInfo("CP_Loader initialized.");
        }

        // Load images from all plugins
        public void LoadImagesFromAllPlugins()
        {
            string pluginsPath = Path.Combine(Paths.BepInExRootPath);
            if (!Directory.Exists(pluginsPath))
            {
                logger.LogWarning($"Plugins directory not found: {pluginsPath}");
                return;
            }

            string[] directories = Directory.GetDirectories(pluginsPath, IMAGE_FOLDER_NAME, SearchOption.AllDirectories);

            LoadGrungeMaterials();
            BindConfigUpdates();

            if (directories.Length == 0)
            {
                logger.LogWarning("No 'CustomPaintings' folders found in plugins.");
                return;
            }

            foreach (string directoryPath in directories)
            {
                logger.LogInfo($"Loading images from: {directoryPath}");
                LoadImagesFromDirectory(directoryPath);
            }
            foreach (var materialGroup in MaterialGroups)
            {
                LoadedMaterials.AddRange(materialGroup.Value);
            }

            logger.LogInfo($"Total images loaded: {LoadedMaterials.Count}");

        }

        // Load images from a specific directory
        private void LoadImagesFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                logger.LogWarning($"Directory does not exist: {directoryPath}");
                return;
            }

            string[] validExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".mp4", ".gif" };

            var files = Directory
                .EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(file => validExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToArray();
            if (files.Length == 0)
            {
                logger.LogWarning($"No images found in {directoryPath}");
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string fileExtension = Path.GetExtension(filePath).ToLower();

                if (fileExtension == ".gif")
                {
                    logger.LogWarning($"Failed to load gif {filePath}. please convert it to .mp4. there is a converter with instructions on the mod's github");
                    continue;   //dont do anything with the file

                }

                if (fileExtension == ".mp4")
                {
                    continue;

                }
                else
                {                    
                    Texture2D texture = LoadTextureFromFile(filePath);
                    if (texture == null)
                    {
                        logger.LogWarning($"Failed to load image #{i + 1}: {filePath}");
                        continue;
                    }

                    float aspectRatio = (float)texture.width / texture.height;
                    if (aspectRatio > 1.3f)
                    {
                        AddGrungeMaterial("Landscape", _LandscapeMaterial, texture);
                    }
                    else if (aspectRatio < 6.0f / 7.0f) // paintings taller than 6x7 will be portraits
                    {
                        AddGrungeMaterial("Portrait", _PortraitMaterial, texture);
                    }
                    else
                    {
                        AddGrungeMaterial("Square", _LandscapeMaterial, texture);
                    }

                    logger.LogInfo($"Loaded image #{loadedcount}: {Path.GetFileName(filePath)}");                    
                }
                loadedcount++;
            }
        }
            

        Material AddGrungeMaterial(Material grungeMaterial, Texture2D texture)
        {
            if (grungeMaterial == null)
            {
                logger.LogWarning($"Falling back to default shader");
                return new Material(Shader.Find("Standard"));
            }

            Material material = new Material(grungeMaterial)
            {
                mainTexture = texture
            };
            return material;
        }

        // Helper method for adding grunge Material
        void AddGrungeMaterial(string paintingType, Material grungeMaterial, Texture2D texture)
        {
            if (!MaterialGroups.ContainsKey(paintingType))
            {
                MaterialGroups[paintingType] = new List<Material>(); // Create if not exists
            }

            Material material = AddGrungeMaterial(grungeMaterial, texture);
            MaterialGroups[paintingType].Add(material);  // Add material to Square group
        }

        // Helper method to load texture from file
        private Texture2D LoadTextureFromFile(string filePath)
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            var texture = new Texture2D(2, 2);
            if (ImageConversion.LoadImage(texture, fileData))
            {
                SetFilterMode(texture);

                texture.Apply();

                return texture;
            }
            return null;
        }

        // Load the grunge materials from the asset bundle
        private void LoadGrungeMaterials()
        {
            string location      = Assembly.GetExecutingAssembly().Location;
            string directoryName = Path.GetDirectoryName(location);
            string assetName     = GRUNGE_ASSET_BUNDLE;
            string assetLocation = Path.Combine(directoryName, assetName);
            logger.LogInfo($"Loading [{assetLocation}]");
            bool assetBundleExists = File.Exists(assetLocation);
            if (assetBundleExists)
            {
                logger.LogInfo($"Grunge Asset Bundle exists.");
            }
            else
            {
                logger.LogWarning($"Grunge Asset Bundle doesn't exist!");

            }

            AssetBundle assetBundle = AssetBundle.LoadFromFile(assetLocation);

            if (assetBundle == null)
            {
                logger.LogError($"Failed to load [{assetName}]!");
            }
            else
            {
                _LandscapeMaterial = assetBundle.LoadAsset<Material>(MATERIAL_LANDSCAPE_ASSET_NAME);
                if (_LandscapeMaterial == null)
                {
                    logger.LogError($"Could not load landscape painting material [{MATERIAL_LANDSCAPE_ASSET_NAME}]!");
                }
                _PortraitMaterial = assetBundle.LoadAsset<Material>(MATERIAL_PORTRAIT_ASSET_NAME);
                if (_PortraitMaterial == null)
                {
                    logger.LogError($"Could not load portrait painting material [{MATERIAL_PORTRAIT_ASSET_NAME}]!");
                }
            }

            if (_LandscapeMaterial != null && _PortraitMaterial != null)
            {
                logger.LogInfo($"Grunge materials successfully loaded!");
            }
        }

        internal void BindConfigUpdates()
        {
            CP_Config.Grunge.State.SettingChanged         += OnGrungeConfigOptionChanged;
            CP_Config.Grunge.Intensity.SettingChanged     += OnGrungeConfigOptionChanged;
            CP_Config.Grunge._BaseColor.SettingChanged    += OnGrungeConfigOptionChanged;

            CP_Config.Grunge._BaseColor.SettingChanged    += OnGrungeConfigOptionChanged;
            CP_Config.Grunge._MainColor.SettingChanged    += OnGrungeConfigOptionChanged;
            CP_Config.Grunge._CracksColor.SettingChanged  += OnGrungeConfigOptionChanged;
            CP_Config.Grunge._OutlineColor.SettingChanged += OnGrungeConfigOptionChanged;

            CP_Config.Graphics.PointFiltering.SettingChanged       += OnPointFilteringConfigOptionChange;
        }

        internal void OnPointFilteringConfigOptionChange(object sender, EventArgs e)
        {
            foreach (var material in LoadedMaterials)
            {
                SetFilterMode(material.mainTexture);
            }
        }

        internal void SetFilterMode(Texture texture)
        {
            if (CP_Config.Graphics.PointFiltering.Value)
            { texture.filterMode = FilterMode.Point; }
            else
            { texture.filterMode = FilterMode.Trilinear; }
        }

        internal void OnGrungeConfigOptionChanged(object sender, EventArgs e)
        {
            UpdateGrungeMaterialParameters();
        }

        // Get config values for the material
        internal void UpdateGrungeMaterialParameters()
        {
            logger.LogDebug($"Updating Grunge Material Parameters...");

            bool grungeEnabled    = CP_Config.Grunge.State.Value;
            float grungeIntensity = CP_Config.Grunge.Intensity.Value;
            Color grungeColor     = new Color(1, 1, 1, grungeIntensity);

            logger.LogDebug($"Grunge state is [{grungeEnabled}]!");
            logger.LogDebug($"Grunge intensity is [{grungeIntensity}]!");

            logger.LogDebug($"Number of loaded painting materials = [{LoadedMaterials.Count}]");

            foreach (var material in LoadedMaterials)
            {
                if (material == null)
                {
                    logger.LogWarning($"No material found!");
                    continue;
                }

                if (grungeEnabled)
                {
                    material.SetColor(CP_Config.Grunge._BaseColor.Definition.Key   ,    CP_Config.Grunge._BaseColor.Value);
                    material.SetColor(CP_Config.Grunge._MainColor.Definition.Key   , CP_Config.Grunge._MainColor.Value    * grungeColor);
                    material.SetColor(CP_Config.Grunge._CracksColor.Definition.Key , CP_Config.Grunge._CracksColor.Value  * grungeColor);
                    material.SetColor(CP_Config.Grunge._OutlineColor.Definition.Key, CP_Config.Grunge._OutlineColor.Value * grungeColor);
                }
                else
                {
                    material.SetColor(CP_Config.Grunge._BaseColor.Definition.Key   , Color.clear);
                    material.SetColor(CP_Config.Grunge._MainColor.Definition.Key   , Color.clear);
                    material.SetColor(CP_Config.Grunge._CracksColor.Definition.Key , Color.clear);
                    material.SetColor(CP_Config.Grunge._OutlineColor.Definition.Key, Color.clear);
                }
            }
        }
    }
}