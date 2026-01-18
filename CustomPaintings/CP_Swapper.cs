using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private readonly CP_LoaderV2 m_loaderV2;
        private readonly CP_GroupList grouper;
        private static CP_Config configfile;
        private static CP_Synchroniser syncer;
        private static CustomPaintings CP_Main;

        //create seed variables      
        public static int HostSeed = 0;        
        public static int ReceivedSeed = 0;     
        public static int Seed = 0; //seed applied to swap

        //create string used to check host settings
        public static string SeperateState = "Singleplayer";
        public static bool RBState = false;
        public static bool ChaosState = false;

        public static bool PrevHostControlValue = CP_Config.HostControl.Value;
        public bool SyncedToHost = false;

        //create string to check what mode you are in
        public static DisplayMode DisplayMode = DisplayMode.Normal;
                

        //changed counts for all the painting swaps
        private int paintingsChangedCount = 0;  
        private int LandscapeChangedCount = 0;  
        private int SquareChangedCount = 0;  
        private int PortraitChangedCount = 0;  

        // Default to Singleplayer
        private static ModState currentState = ModState.SinglePlayer;

        //create Temporary lists to prevent duplicates
        public List<Material> ListPaintingsAll;
        public List<Material> ListPaintingsPortrait;
        public List<Material> ListPaintingsLandscape;
        public List<Material> ListPaintingsSquare;

        public List<int> ListUsedPaintingsAll        = new List<int>{};
        public List<int> ListUsedPaintingsPortrait   = new List<int>{};
        public List<int> ListUsedPaintingsLandscape  = new List<int>{};
        public List<int> ListUsedPaintingsSquare     = new List<int>{};

        public List<int> ListUsedPaintingsAllPrevRound;
        public List<int> ListUsedPaintingsPortraitPrevRound;
        public List<int> ListUsedPaintingsLandscapePrevRound;
        public List<int> ListUsedPaintingsSquarePrevRound;

        //check the current modstate of the mod
        public ModState GetModState()
        {
            return currentState;
        }

        // Constructor to initialize the logger and loader
        public CP_Swapper(CP_Logger logger, CP_Loader loader, CP_GroupList grouper, CP_LoaderV2 loaderV2)
        {
            this.logger = logger;
            this.loader = loader;
            this.grouper = grouper;
            m_loaderV2 = loaderV2;
            logger.LogInfo("CP_Swapper initialized.");

            // Log the current state on initialization
            logger.LogInfo($"Initial ModState: {currentState}");
            ResetTempLists();

        }

        //reset temp lists
        public void ResetTempLists()
        {
            ListPaintingsAll =           new List<Material>(loader.LoadedMaterials);
            PopulateList(ref ListPaintingsPortrait, "Portrait", loader);
            PopulateList(ref ListPaintingsLandscape, "Landscape", loader);
            PopulateList(ref ListPaintingsSquare, "Square", loader);
            ListUsedPaintingsAll.Clear();
            ListUsedPaintingsLandscape.Clear();
            ListUsedPaintingsSquare.Clear();
            ListUsedPaintingsPortrait.Clear();
            logger.LogDebug($"resetting all lists");
        }

        private void PopulateList(ref List<Material> list, string key, in CP_Loader loader)
        {
            if(loader.MaterialGroups.TryGetValue(key, out List<Material> materials))
            {
                list = new List<Material>(materials);
            }
            else
            {
                list = new List<Material>();
            }
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
                Seed = HostSeed;        //use host seed
            
            if (currentState == ModState.Client)
                Seed = ReceivedSeed;    //use client seed
                        
            var rng = new System.Random(Seed);            

            //save all used images from last round for late joiners
            ListUsedPaintingsAllPrevRound        = new List<int>(ListUsedPaintingsAll);
            ListUsedPaintingsPortraitPrevRound   = new List<int>(ListUsedPaintingsPortrait);
            ListUsedPaintingsLandscapePrevRound  = new List<int>(ListUsedPaintingsLandscape);
            ListUsedPaintingsSquarePrevRound     = new List<int>(ListUsedPaintingsSquare);



            if (CP_Config.ChaosMode.Value == true && CP_Config.HostControl.Value == false && currentState != ModState.SinglePlayer|| ChaosState == true && CP_Config.HostControl.Value == true && currentState != ModState.SinglePlayer|| CP_Config.ChaosMode.Value == true && currentState == ModState.SinglePlayer)
            {
                DisplayMode = DisplayMode.Chaos;
            }
            else if (CP_Config.ChaosMode.Value == false && CP_Config.HostControl.Value == false && currentState != ModState.SinglePlayer|| ChaosState == false && CP_Config.HostControl.Value == true && currentState != ModState.SinglePlayer|| CP_Config.ChaosMode.Value == false && currentState == ModState.SinglePlayer)
            {
                if (CP_Config.RugsAndBanners.Value == true && CP_Config.HostControl.Value == false && currentState != ModState.SinglePlayer || RBState == true && CP_Config.HostControl.Value == true && currentState != ModState.SinglePlayer|| CP_Config.RugsAndBanners.Value == true && currentState == ModState.SinglePlayer)
                {
                    DisplayMode = DisplayMode.RugsAndBanners;
                }
                else if (CP_Config.RugsAndBanners.Value == false && CP_Config.HostControl.Value == false && currentState != ModState.SinglePlayer|| RBState == false && CP_Config.HostControl.Value == true && currentState != ModState.SinglePlayer|| CP_Config.RugsAndBanners.Value == false && currentState == ModState.SinglePlayer)
                {
                    DisplayMode = DisplayMode.Normal;
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
                using var _ = UnityEngine.Pool.ListPool<GameObject>.Get(out var rootObjects);
                using var __ = UnityEngine.Pool.ListPool<MeshRenderer>.Get(out var renderers);
                Dictionary<ShapeType, List<(Renderer renderer, int matIndex)>> groups = new();
                scene.GetRootGameObjects(rootObjects);
                HashSet<string> uniqueMaterials = new HashSet<string>();
                System.Text.StringBuilder sb = new ();

                foreach (GameObject obj in rootObjects)
                {
                    renderers.Clear();
                    obj.GetComponentsInChildren(renderers);

                    using var ___ = UnityEngine.Pool.ListPool<Material>.Get(out var materials);
                    foreach (MeshRenderer renderer in renderers)
                    {
                        renderer.GetSharedMaterials(materials);

                        for (int i = 0; i < materials.Count; i++)
                        {
                            if(materials[i] == null) continue;

                            materialsChecked++;  // Increment checked materials count
                            string matName = materials[i].name.Trim();

                            if (uniqueMaterials.Add(matName))
                            {
                                sb.Append(matName).Append('\n');
                            }
                            if (matName.Contains("Painting Frame Vertical Gold") || matName.Contains("Painting Frame Horizontal Gold"))
                            {

                                continue; // Skip this material
                            }

                            if (!grouper.HasDisplayMode(matName, DisplayMode)) continue;
                            if (!grouper.TryGetShapeOf(matName, out ShapeType shape)) continue;

                            if(!groups.TryGetValue(shape, out List<(Renderer renderer, int matIndex)> group))
                            {
                                group = new List<(Renderer renderer, int matIndex)>();
                                groups.Add(shape, group);
                            }

                            group.Add((renderer, i));

                            // if (loader.LoadedMaterials.Count > 0)
                            // {
                            //     if (ListPaintingsAll.Count == 0)
                            //     {
                            //         ListPaintingsAll.AddRange(loader.LoadedMaterials);                                            
                            //         ListUsedPaintingsAll.Clear();
                            //     }

                            //     // Use the seed based random number generator to choose the next image based on the remaining ones in the list
                            //     int index = rng.Next(0, ListPaintingsAll.Count);
                            //     ListUsedPaintingsAll.Add(index);

                            //     materials[i] = ListPaintingsAll[index];
                            //     paintingsChangedCount++;  // Increment the count of paintings changed                               
                            //     logger.LogToFileOnly("DEBUG", $"painting used random index number | {index, -13} | to change a painting");

                            //     ListPaintingsAll.RemoveAt(index);
                                
                            // }
                        }

                        // renderer.sharedMaterials = materials.ToArray();
                    }
                                 
                }

                logger.LogToFileOnly("DEBUG", sb.ToString());

                m_loaderV2.CleanUp();
                using var poolList = UnityEngine.Pool.ListPool<string>.Get(out var paths);
                foreach(var (shape, group) in groups)
                {
                    paths.Clear();
                    int totalCount = m_loaderV2.GetTotalImageCount(shape);
                    m_loaderV2.RetrieveImagePaths(shape, startIndex: rng.Next(totalCount + 1), size: group.Count, paths);

                    int remaining = group.Count;
                    for(int i = 0; i < group.Count; i++)
                    {
                        int index = rng.Next(0, remaining);

                        m_loaderV2.RequestLoadTexture(paths[index], group[i].renderer, group[i].matIndex);

                        remaining--;
                        paths[index] = paths[remaining];
                    }
                }
                logger.LogInfo($"Total paintings changed in this scene: {paintingsChangedCount}");
            }
            else if (CP_Config.SeperateImages.Value == true && SeperateState == "Singleplayer" || SeperateState == "on" && CP_Config.HostControl.Value == true || CP_Config.HostControl.Value == false && CP_Config.SeperateImages.Value == true)
            {
                foreach (GameObject obj in scene.GetRootGameObjects())
                {
                    foreach (MeshRenderer renderer in obj.GetComponentsInChildren<MeshRenderer>())
                    {
                        Material[] materials = renderer.sharedMaterials;

                        for (int i = 0; i < materials.Length; i++)
                        {
                            if(materials[i] == null) continue;
                            materialsChecked++;  // Increment checked materials count

                            string matName = materials[i].name.Trim();
                            if (matName.Contains("Painting Frame Vertical Gold") || matName.Contains("Painting Frame Horizontal Gold"))
                            {
                                continue; // Skip this material
                            }
                            if (!grouper.TryGetShapeOf(matName, out ShapeType shape)) continue;

                            if (shape == ShapeType.Landscape)
                            {
                                if (ListPaintingsLandscape.Count == 0)
                                {
                                    ListPaintingsLandscape.AddRange(loader.MaterialGroups["Landscape"]);
                                    ListUsedPaintingsLandscape.Clear();
                                }

                                // Use the seed based random number generator to choose the next image based on the remaining ones in the list
                                int index = rng.Next(0, ListPaintingsLandscape.Count);
                                materials[i] = ListPaintingsLandscape[index];
                                LandscapeChangedCount++;  // Increment the count of paintings changed 
                                logger.LogToFileOnly("DEBUG", $"painting used random index number | {index,-13} | to change landscape painting");

                                ListPaintingsLandscape.RemoveAt(index);
                                ListUsedPaintingsLandscape.Add(index);

                            }
                            else if (shape == ShapeType.Square)
                            {
                                if (ListPaintingsSquare.Count == 0)
                                {
                                    ListPaintingsSquare.AddRange(loader.MaterialGroups["Square"]);
                                    ListUsedPaintingsSquare.Clear();
                                }

                                // Use the seed based random number generator to choose the next image based on the remaining ones in the list
                                int index = rng.Next(0, ListPaintingsSquare.Count);
                                materials[i] = ListPaintingsSquare[index];
                                SquareChangedCount++;  // Increment the count of paintings changed 
                                logger.LogToFileOnly("DEBUG", $"painting used random index number | {index,-13} | to change square painting");

                                ListPaintingsSquare.RemoveAt(index);
                                ListUsedPaintingsSquare.Add(index);
                            }
                            else if (shape == ShapeType.Portrait)
                            {
                                if (ListPaintingsPortrait.Count == 0)
                                {
                                    ListPaintingsPortrait.AddRange(loader.MaterialGroups["Portrait"]);
                                    ListUsedPaintingsPortrait.Clear();
                                }

                                // Use the seed based random number generator to choose the next image based on the remaining ones in the list
                                int index = rng.Next(0, ListPaintingsPortrait.Count);
                                materials[i] = ListPaintingsPortrait[index];
                                PortraitChangedCount++;  // Increment the count of paintings changed 
                                logger.LogToFileOnly("DEBUG", $"painting used random index number | {index,-13} | to change portrait painting");

                                ListPaintingsPortrait.RemoveAt(index);
                                ListUsedPaintingsPortrait.Add(index);
                            }
                            renderer.sharedMaterials = materials;
                        }
                    }
                }
                                
                logger.LogInfo($"Total materials checked: {materialsChecked}");

                // Log how many paintings were changed in this scene
                logger.LogInfo($"Total paintings changed in this scene: {LandscapeChangedCount + SquareChangedCount + PortraitChangedCount}\n" + $"Landscape: {LandscapeChangedCount}\n" + $"Square: {SquareChangedCount}\n" + $"Portrait: {PortraitChangedCount}");
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