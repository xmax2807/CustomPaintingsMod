using UnityEngine;
using BepInEx;
using HarmonyLib;
using static CustomPaintings.CP_Swapper;
using Photon.Pun;
using System.Threading.Tasks;
using BepInEx.Configuration;
using System;
using System.IO;
using System.Collections;

namespace CustomPaintings
{
    [BepInPlugin("UnderratedJunk.CustomPaintings", "CustomPaintings", "1.2.0")]
    public class CustomPaintings : BaseUnityPlugin
    {
        private static CP_Logger logger;
        private static CP_Loader loader;
        private static CP_Swapper swapper;
        private static CP_Synchroniser sync;
        private static CP_GroupList grouper;
        private static CP_Config configfile;
        private static CP_GifVidManager GifVidManager;
        private static CustomPaintings CP_Main;
        private static CP_LoaderV2 m_loaderV2;

        public static int? receivedSeed = null;
        public static int? oldreceivedSeed = null;
        public static readonly int maxWaitTimeMs = 1000;
        private bool PreviousHostControlValue = false;

        private static bool _isUpdatingRenderers = false;

        private readonly Harmony harmony = new Harmony("UnderratedJunk.CustomPaintings");

        private void Awake()
        {
            logger = new CP_Logger("CustomPaintings");
            logger.LogInfo("CustomPaintings mod initialized.");

            GifVidManager = new CP_GifVidManager(logger);

            CP_Config.Init(Config);

            loader = new CP_Loader(logger, GifVidManager);

            m_loaderV2 = new CP_LoaderV2(logger, new MaterialPropertyBlock(), Paths.PluginPath, RunCoroutine);

            configfile = new CP_Config();

            grouper = new CP_GroupList(logger, PaintingDataReader.Read(Path.Combine(Directory.GetParent(Info.Location).FullName, "materialNames.txt")));

            swapper = new CP_Swapper(logger, loader, grouper, m_loaderV2);

            sync = new CP_Synchroniser(logger, swapper);

            // Subscribe config event here since OnEnable may never fire
            CP_Config.HostControl.SettingChanged += OnHostControlChanged;

            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        }

        private static void RunCoroutine(Func<IEnumerator> func)
        {
            // Instead of StartCoroutine, set flag for the Harmony Update patch to tick
            _isUpdatingRenderers = true;
        }

        private void OnHostControlChanged(object sender, EventArgs e)
        {
            if (PhotonNetwork.InRoom)
            {
                sync.SyncRequestOnJoin();

                Task.Run(async () =>
                {
                    int waited = 0;
                    int interval = 50;

                    while (swapper.SyncedToHost == false && waited < maxWaitTimeMs)
                    {
                        await Task.Delay(interval);
                        waited += interval;
                    }
                    if (swapper.SyncedToHost == false)
                        logger.LogError("failed to sync to the host");
                });
            }
        }

        [HarmonyPatch(typeof(GameDirector), "Update")]
        public class GameUpdatePatch
        {
            private static void Postfix()
            {
                // Replaces the old Update() hotkey checks
                if (Input.GetKeyDown(configfile.ForceSwapKey))
                {
                    swapper.ReplacePaintings();
                }
                if (Input.GetKeyDown(configfile.SyncRequestKey))
                {
                    sync.SyncRequest();
                }

                if (_isUpdatingRenderers)
                {
                    logger.LogInfo("Updating renderers");
                    if (!m_loaderV2.UpdateRenderers())
                    {
                        logger.LogInfo("Done updating renderers");
                        _isUpdatingRenderers = false;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), "LoadingLevelAnimationCompletedRPC")]
        public class PaintingSwapPatch
        {
            private static void Postfix()
            {
                Task.Run(async () =>
                {
                    if (swapper.GetModState() == CP_Swapper.ModState.Client || swapper.GetModState() == CP_Swapper.ModState.Host)
                    {
                        int waited = 0;
                        int interval = 50;

                        while (!receivedSeed.HasValue && waited < maxWaitTimeMs)
                        {
                            await Task.Delay(interval);
                            waited += interval;
                        }

                        if (receivedSeed.HasValue)
                        {
                            logger.LogInfo($"[Postfix] Client using received seed: {receivedSeed.Value}");
                            oldreceivedSeed = ReceivedSeed;
                            ReceivedSeed = receivedSeed.Value;
                            receivedSeed = null;
                        }
                        else if (ReceivedSeed == oldreceivedSeed)
                        {
                            logger.LogWarning("[Postfix] Client did not receive seed in time. Proceeding without it.");
                        }
                    }

                    swapper.ReplacePaintings();
                });
            }

            private static void Prefix()
            {
                if (swapper.GetModState() == ModState.Client)
                {
                    PhotonNetwork.AddCallbackTarget(sync);
                }

                if (swapper.GetModState() == ModState.Host)
                {
                    HostSeed = UnityEngine.Random.Range(0, int.MaxValue);
                    logger.LogInfo($"Generated Hostseed: {HostSeed}");
                    PhotonNetwork.AddCallbackTarget(sync);

                    sync.SendSeed(HostSeed);

                    if (CP_Config.SeperateImages.Value == true)
                    {
                        sync.SendHostSettings("on", CP_Config.RugsAndBanners.Value, CP_Config.ChaosMode.Value);
                    }
                    else if (CP_Config.SeperateImages.Value == false)
                    {
                        sync.SendHostSettings("off", CP_Config.RugsAndBanners.Value, CP_Config.ChaosMode.Value);
                    }
                }

                loader.UpdateGrungeMaterialParameters();
            }
        }

        [HarmonyPatch(typeof(NetworkConnect), "TryJoiningRoom")]
        public class JoinLobbyPatch
        {
            private static void Postfix()
            {
                if (CP_Config.HostControl.Value == true)
                {
                    Task.Run(async () =>
                    {
                        if (swapper.GetModState() == ModState.Client)
                        {
                            int waited = 0;
                            int interval = 50;

                            while (swapper.SyncedToHost == false && waited < maxWaitTimeMs)
                            {
                                await Task.Delay(interval);
                                waited += interval;
                            }
                            if (swapper.SyncedToHost == false)
                                logger.LogError("failed to sync to the host");
                        }
                    });
                }
            }

            private static void Prefix()
            {
                if (swapper.GetModState() != ModState.Host)
                {
                    swapper.SetState(ModState.Client);

                    if (CP_Config.HostControl.Value == true)
                        sync.SyncRequestOnJoin();
                }
            }
        }

        [HarmonyPatch(typeof(SteamManager), "HostLobby")]
        public class HostLobbyPatch
        {
            private static bool Prefix()
            {
                swapper.SetState(ModState.Host);
                return true;
            }
        }

        [HarmonyPatch(typeof(SteamManager), "LeaveLobby")]
        public class LeaveLobbyPatch
        {
            private static void Postfix()
            {
                PhotonNetwork.RemoveCallbackTarget(sync);

                swapper.SetState(ModState.SinglePlayer);
                swapper.ResetTempLists();
                SeperateState = "Singleplayer";
                swapper.SyncedToHost = false;
            }
        }
    }
}