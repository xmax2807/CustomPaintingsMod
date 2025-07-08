using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using Photon.Pun;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine.Device;
using System;
using System.Linq;
using BepInEx.Configuration;

namespace CustomPaintings
{
    public class CP_Swapper
    {
        // create different states for the mod
        public enum ModState
        {
            Host,        
            Client,      
            SinglePlayer 
        }

        private readonly CP_Logger logger;
        private readonly CP_Loader loader;
        private readonly CP_GroupList grouper;
        private static CP_Config configfile;

        //create seed variables      
        public static int HostSeed = 0;        
        public static int ReceivedSeed = 0;     
        public static int Seed = 0; //seed applied to swap

        //create string used to check host settings
        public static string SeperateState = "Singleplayer";
        public static bool RBState = false;
        public static bool ChaosState = false;

        //create string to check what mode you are in
        public static string ImageMode = "Normal";

        //changed counts for all the painting swaps
        private int paintingsChangedCount = 0;  
        private int LandscapeChangedCount = 0;  
        private int SquareChangedCount = 0;  
        private int PortraitChangedCount = 0;  

        // Default to Singleplayer
        private static ModState currentState = ModState.SinglePlayer;


        //check the current modstate of the mod
        public ModState GetModState()
        {
            return currentState;
        }

        // Constructor to initialize the logger and loader
        public CP_Swapper(CP_Logger logger, CP_Loader loader, CP_GroupList grouper)
        {
            this.logger = logger;
            this.loader = loader; // Initialize loader instance
            this.grouper = grouper;
            logger.LogInfo("CP_Swapper initialized.");

            // Log the current state on initialization
            logger.LogInfo($"Initial ModState: {currentState}");

            
        }

        

        // Perform the painting swap operation
        public void ReplacePaintings()
        {
            if (currentState == ModState.SinglePlayer)
            {
                //use singleplayer seed
                Seed = UnityEngine.Random.Range(0, int.MaxValue);
                logger.LogInfo($"Generated new random singleplayer seed: {Seed}");
            }


            if (currentState == ModState.Host)
            {
                //use host seed
                Seed = HostSeed;
            }


            if (currentState == ModState.Client)
            {
                //use client seed
                Seed = ReceivedSeed;
            }

            var rng = new System.Random(Seed);


            if (CP_Config.ChaosMode.Value == true && CP_Config.HostControl.Value == false && currentState != ModState.SinglePlayer|| ChaosState == true && CP_Config.HostControl.Value == true && currentState != ModState.SinglePlayer|| CP_Config.ChaosMode.Value == true && currentState == ModState.SinglePlayer)
            {
                ImageMode = "Chaos";
            }
            else if (CP_Config.ChaosMode.Value == false && CP_Config.HostControl.Value == false && currentState != ModState.SinglePlayer|| ChaosState == false && CP_Config.HostControl.Value == true && currentState != ModState.SinglePlayer|| CP_Config.ChaosMode.Value == false && currentState == ModState.SinglePlayer)
            {
                if (CP_Config.RugsAndBanners.Value == true && CP_Config.HostControl.Value == false && currentState != ModState.SinglePlayer || RBState == true && CP_Config.HostControl.Value == true && currentState != ModState.SinglePlayer|| CP_Config.RugsAndBanners.Value == true && currentState == ModState.SinglePlayer)
                {
                    ImageMode = "RugsAndBanners";
                }
                else if (CP_Config.RugsAndBanners.Value == false && CP_Config.HostControl.Value == false && currentState != ModState.SinglePlayer|| RBState == false && CP_Config.HostControl.Value == true && currentState != ModState.SinglePlayer|| CP_Config.RugsAndBanners.Value == false && currentState == ModState.SinglePlayer)
                {
                    ImageMode = "Normal";
                }
            }

            //check current scene
            Scene scene = SceneManager.GetActiveScene();

            logger.LogInfo($"Applying seed {Seed} for painting swaps in scene: {scene.name}");

            logger.LogInfo("Replacing all paintings with custom images...");

            paintingsChangedCount = 0;  // Reset the paintings changed counter for this scene

            int materialsChecked = 0;  // Count materials checked in the scene

            if (CP_Config.SeperateImages.Value == false && SeperateState == "Singleplayer" || SeperateState == "off" && CP_Config.HostControl.Value == true || CP_Config.HostControl.Value == false && CP_Config.SeperateImages.Value == false)
            {
                foreach (GameObject obj in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    foreach (MeshRenderer renderer in obj.GetComponentsInChildren<MeshRenderer>())
                    {
                        Material[] materials = renderer.sharedMaterials;

                        for (int i = 0; i < materials.Length; i++)
                        {

                            materialsChecked++;  // Increment checked materials count
                            string matName = materials[i].name.Trim();

                            if (CP_GroupList.MaterialNameToGroup.TryGetValue(matName, out var groupNames))
                            {

                                if (materials[i] != null && groupNames.Contains(ImageMode))
                                {

                                    // Exclude specific materials
                                    if (materials[i].name.Contains("Painting Frame Vertical Gold") || materials[i].name.Contains("Painting Frame Horizontal Gold"))
                                    {

                                        continue; // Skip this material
                                    }

                                    if (loader.LoadedMaterials.Count > 0)
                                    {
                                        int number = rng.Next(0, int.MaxValue);

                                        // Use the seed to pick the image based on the index
                                        int index = Mathf.Abs(number % loader.LoadedMaterials.Count);
                                        materials[i] = loader.LoadedMaterials[index];
                                        paintingsChangedCount++;  // Increment the count of paintings changed                               
                                        logger.LogToFileOnly("DEBUG", $"painting used random number | {number, -13} | to change any painting");

                                    }
                                }
                            }
                        }

                        renderer.sharedMaterials = materials;
                    }
                }
            }
            else if (CP_Config.SeperateImages.Value == true && SeperateState == "Singleplayer" || SeperateState == "on" && CP_Config.HostControl.Value == true || CP_Config.HostControl.Value == false && CP_Config.SeperateImages.Value == true)
            {
                foreach (GameObject obj in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    foreach (MeshRenderer renderer in obj.GetComponentsInChildren<MeshRenderer>())
                    {
                        Material[] materials = renderer.sharedMaterials;

                        for (int i = 0; i < materials.Length; i++)
                        {
                            materialsChecked++;  // Increment checked materials count

                            string matName = materials[i].name.Trim();

                            if (CP_GroupList.MaterialNameToGroup.TryGetValue(matName, out var groupNames))
                            {

                                if (materials[i] != null && groupNames.Contains(ImageMode))
                                {

                                    // Exclude specific materials (e.g., frames that we don't want to swap)
                                    if (materials[i].name.Contains("Painting Frame Vertical Gold") || materials[i].name.Contains("Painting Frame Horizontal Gold"))
                                    {

                                        continue; // Skip this material
                                    }

                                    int number = rng.Next(0, int.MaxValue);

                                    if (groupNames.Contains("Landscape"))
                                    {
                                        int index = Mathf.Abs(number % loader.MaterialGroups["Landscape"].Count);
                                        materials[i] = loader.MaterialGroups["Landscape"][index];
                                        LandscapeChangedCount++;  // Increment the count of paintings changed 
                                        logger.LogToFileOnly("DEBUG", $"painting used random number | {number,-13} | to change landscape painting");

                                    }
                                    else if (groupNames.Contains("Square"))
                                    {
                                        int index = Mathf.Abs(number % loader.MaterialGroups["Square"].Count);
                                        materials[i] = loader.MaterialGroups["Square"][index];
                                        SquareChangedCount++;  // Increment the count of paintings changed 
                                        logger.LogToFileOnly("DEBUG", $"painting used random number | {number,-13} | to change square painting");
                                    }
                                    else if (groupNames.Contains("Portrait"))
                                    {
                                        int index = Mathf.Abs(number % loader.MaterialGroups["Portrait"].Count);
                                        materials[i] = loader.MaterialGroups["Portrait"][index];
                                        PortraitChangedCount++;  // Increment the count of paintings changed 
                                        logger.LogToFileOnly("DEBUG", $"painting used random number | {number,-13} | to change portrait painting");
                                    }

                                }
                            }
                            renderer.sharedMaterials = materials;
                        }
                    }
                }
                                
                logger.LogInfo($"Total materials checked: {materialsChecked}");

                // Log how many paintings were changed in this scene
                logger.LogInfo($"Total paintings changed in this scene: {LandscapeChangedCount + SquareChangedCount + PortraitChangedCount}");
            }
        }

        // Set the current mod state
        public void SetState(ModState newState)
        {
            currentState = newState;
            
            logger.LogInfo($"Mod state set to: {currentState}");
        }
    }
}