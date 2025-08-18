using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using Photon.Pun;
using ExitGames.Client.Photon;
using Photon.Realtime;
using CustomPaintings;
using static CustomPaintings.CP_Swapper;
using System.Net;
using System.Security.Cryptography;

namespace CustomPaintings
{
    public class CP_Synchroniser : MonoBehaviourPunCallbacks, IOnEventCallback
    {

        private readonly CP_Logger logger;
        private readonly CP_Swapper swapper;
        private static CustomPaintings CP_main;

        // assign a specific code to different events
        public const byte SeedEventCode = 1;
        public const byte HostSettingsCode = 2;
        public const byte SyncRequestCode = 3;
        public const byte NewClientSyncCode = 4;


        public CP_Synchroniser(CP_Logger logger, CP_Swapper swapper)
        {
            this.logger = logger;
            this.swapper = swapper;
            logger.LogInfo("CP_Synchroniser initialized.");
        }

        public void SendSeed(int seed)
        {
            // Create an event data object that will carry the seed information
            object[] content = new object[] { seed };

            logger.LogInfo("Sharing seed with other clients");

            RaiseEventOptions options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.All, // Send to everyone, including yourself
                CachingOption = EventCaching.AddToRoomCache // cache data for late joiners
            };

            // Raise the event to all clients (using the custom event code)
            PhotonNetwork.RaiseEvent(SeedEventCode, content, options, SendOptions.SendReliable);

        }

        public void SendHostSettings(string HostSeperateState, bool HostRBState, bool Chaosstate)
        {


            // Create an event data object that will carry the seperation state information
            object[] content = new object[] { HostSeperateState, HostRBState, Chaosstate };

            logger.LogInfo("Sharing settings");

            RaiseEventOptions options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.All, // Send to everyone, including yourself
                CachingOption = EventCaching.AddToRoomCache // cache data for late joiners
            };

            // Raise the event to all clients (using the custom event code)
            PhotonNetwork.RaiseEvent(HostSettingsCode, content, options, SendOptions.SendReliable);

        }

        public void SyncRequest()
        {
          /*  if (swapper.GetModState() == CP_Swapper.ModState.Host)
                return; */

            logger.LogInfo("Requesting host sync type 'Sync'");

            object[] content = new object[] { "Sync" };

            RaiseEventOptions options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.MasterClient, // Send to Host
            };

            // Raise the event to Host
            PhotonNetwork.RaiseEvent(SyncRequestCode, content, options, SendOptions.SendReliable);

        }

        public void SyncRequestOnJoin()
        {
            /*  if (swapper.GetModState() == CP_Swapper.ModState.Host)
                  return; */

            logger.LogInfo("Requesting host sync type 'LateJoin'");

            object[] content = new object[] { "LateJoin" };

            RaiseEventOptions options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.MasterClient, // Send to Host                
            };

            // Raise the event to Host
            PhotonNetwork.RaiseEvent(SyncRequestCode, content, options, SendOptions.SendReliable);

        }

        public void NewClientSync(int ClientID, List<int> all, List<int> portrait, List<int> square, List<int> landscape)
        {
            //turn lists into arrays
            int[] allArr            = all.ToArray();
            int[] portraitArr       = portrait.ToArray();
            int[] squareArr         = square.ToArray();
            int[] landscapeArr      = landscape.ToArray();

            // Create an event data object that will carry the seed information
            object[] content = new object[] 
            {
                allArr,
                portraitArr,
                squareArr,
                landscapeArr
            };

            logger.LogInfo("Syncing data with new client");

            RaiseEventOptions options = new RaiseEventOptions
            {
                TargetActors = new int[] { ClientID },
                CachingOption = EventCaching.AddToRoomCache // cache data for late joiners
            };

            // Raise the event to all clients (using the custom event code)
            PhotonNetwork.RaiseEvent(NewClientSyncCode, content, options, SendOptions.SendReliable);

        }



        public void OnEvent(EventData photonEvent)
        {
            //retrieve seed data from event
            if (photonEvent.Code == SeedEventCode)
            {
                object[] data = (object[])photonEvent.CustomData;
                int seed = (int)data[0];

                logger.LogInfo($"Received seed: {seed}");
                ReceivedSeed = seed;
            }

            if (photonEvent.Code == SyncRequestCode)
            {
                if (swapper.GetModState() != CP_Swapper.ModState.Host)
                     return;

                object[] data = (object[])photonEvent.CustomData;
                string RequestType = (string)data[0];

                int ClientID = photonEvent.Sender;
                logger.LogInfo($"Client {ClientID} requested a host sync");
                if (RequestType == "Sync")
                {
                    NewClientSync(ClientID, swapper.ListUsedPaintingsAll, swapper.ListUsedPaintingsPortrait, swapper.ListUsedPaintingsSquare, swapper.ListUsedPaintingsLandscape);
                    logger.LogDebug($"Sending used image lists for Sync request type 'Sync'");
                    if (swapper.ListUsedPaintingsAll.Count != 0)
                    {
                        logger.LogToFileOnly("DEBUG", $"Sending images from 'UsedAll' list");
                        for (int i = 0; i < swapper.ListUsedPaintingsAll.Count; i++)
                            logger.LogToFileOnly("DEBUG", $"Sending |{swapper.ListUsedPaintingsAll[i],-13}|");
                    }
                    else
                        logger.LogToFileOnly("DEBUG", $"'UsedAll' list is empty");

                    if (swapper.ListUsedPaintingsPortrait.Count != 0)
                    {
                        logger.LogToFileOnly("DEBUG", $"Sending images from 'UsedPortrait' list");
                        for (int i = 0; i < swapper.ListUsedPaintingsPortrait.Count; i++)
                            logger.LogToFileOnly("DEBUG", $"Sending |{swapper.ListUsedPaintingsPortrait[i],-13}|");
                    }
                    else
                        logger.LogToFileOnly("DEBUG", $"'UsedPortrait' list is empty");

                    if (swapper.ListUsedPaintingsSquare.Count != 0)
                    {
                        logger.LogToFileOnly("DEBUG", $"Sending images from 'UsedSquare' list");
                        for (int i = 0; i < swapper.ListUsedPaintingsSquare.Count; i++)
                            logger.LogToFileOnly("DEBUG", $"Sending |{swapper.ListUsedPaintingsSquare[i],-13}|");
                    }
                    else
                        logger.LogToFileOnly("DEBUG", $"'UsedSquare' list is empty");

                    if (swapper.ListUsedPaintingsLandscape.Count != 0)
                    {
                        logger.LogToFileOnly("DEBUG", $"Sending images from 'UsedLandscape' list");
                        for (int i = 0; i < swapper.ListUsedPaintingsLandscape.Count; i++)
                            logger.LogToFileOnly("DEBUG", $"Sending |{swapper.ListUsedPaintingsLandscape[i],-13}|");
                    }
                    else
                        logger.LogToFileOnly("DEBUG", $"'UsedLandscape' list is empty");

                }

                if (RequestType == "LateJoin")
                {
                    NewClientSync(ClientID, swapper.ListUsedPaintingsAllPrevRound, swapper.ListUsedPaintingsPortraitPrevRound, swapper.ListUsedPaintingsSquarePrevRound, swapper.ListUsedPaintingsLandscapePrevRound);
                    logger.LogDebug($"Sending used image lists for Sync request type 'LateJoin'");
                    if (swapper.ListUsedPaintingsAllPrevRound.Count != 0)
                    {
                        logger.LogToFileOnly("DEBUG", $"Sending images from 'UsedAllPrevRound' list");
                        for (int i = 0; i < swapper.ListUsedPaintingsAllPrevRound.Count; i++)
                            logger.LogToFileOnly("DEBUG", $"Sending |{swapper.ListUsedPaintingsAllPrevRound[i],-13}|");
                    }
                    else
                        logger.LogToFileOnly("DEBUG", $"'UsedAllPrevRound' list is empty");

                    if (swapper.ListUsedPaintingsPortraitPrevRound.Count != 0)
                    {
                        logger.LogToFileOnly("DEBUG", $"Sending images from 'UsedPortraitPrevRound' list");
                        for (int i = 0; i < swapper.ListUsedPaintingsPortraitPrevRound.Count; i++)
                            logger.LogToFileOnly("DEBUG", $"Sending |{swapper.ListUsedPaintingsPortraitPrevRound[i],-13}|");
                    }
                    else
                        logger.LogToFileOnly("DEBUG", $"'UsedPortraitPrevRound' list is empty");

                    if (swapper.ListUsedPaintingsSquarePrevRound.Count != 0)
                    {
                        logger.LogToFileOnly("DEBUG", $"Sending images from 'UsedSquarePrevRound' list");
                        for (int i = 0; i < swapper.ListUsedPaintingsSquarePrevRound.Count; i++)
                            logger.LogToFileOnly("DEBUG", $"Sending |{swapper.ListUsedPaintingsSquarePrevRound[i],-13}|");
                    }
                    else
                        logger.LogToFileOnly("DEBUG", $"'UsedSquarePrevRound' list is empty");

                    if (swapper.ListUsedPaintingsLandscapePrevRound.Count != 0)
                    {
                        logger.LogToFileOnly("DEBUG", $"Sending images from 'UsedLandscapePrevRound' list");
                        for (int i = 0; i < swapper.ListUsedPaintingsLandscapePrevRound.Count; i++)
                            logger.LogToFileOnly("DEBUG", $"Sending |{swapper.ListUsedPaintingsLandscapePrevRound[i],-13}|");
                    }
                    else
                        logger.LogToFileOnly("DEBUG", $"'UsedLandscapePrevRound' list is empty");

                }
            }

            if (photonEvent.Code == NewClientSyncCode)
            {                
               /* if (swapper.GetModState() == CP_Swapper.ModState.Host)
                {
                    logger.LogInfo("Data succesfully sent after a host sync request");
                    return;
                } */              

                logger.LogInfo("Receiving data after a host sync request");

                object[] data           = (object[])photonEvent.CustomData;
                int[] allData           = (int[])data[0];
                int[] portraitData      = (int[])data[1];
                int[] squareData        = (int[])data[2];
                int[] landscapeData     = (int[])data[3];

                // turn arrays back into lists
                List<int> all           = new List<int>(allData);
                List<int> portrait      = new List<int>(portraitData);
                List<int> square        = new List<int>(squareData);
                List<int> landscape     = new List<int>(landscapeData);

                swapper.ResetTempLists();

                //remove used images from the lists
                if (all.Count != 0)
                {
                    logger.LogToFileOnly("DEBUG", $"Removing images from 'all' list");
                    for (int i = 0; i < all.Count; i++)
                    {
                        logger.LogToFileOnly("DEBUG", $"Removing |{all[i], -13}|");
                        swapper.ListPaintingsAll.RemoveAt(all[i]);
                    }
                }

                if (portrait.Count != 0)
                {
                    logger.LogToFileOnly("DEBUG", $"Removing images from 'portrait' list");
                    for (int i = 0; i < portrait.Count; i++)
                    {
                        logger.LogToFileOnly("DEBUG", $"Removing |{portrait[i], -13}|");
                        swapper.ListPaintingsAll.RemoveAt(portrait[i]);
                    }
                }

                if (square.Count != 0)
                {
                    logger.LogToFileOnly("DEBUG", $"Removing images from 'square' list");
                    for (int i = 0; i < square.Count; i++)
                    {
                        logger.LogToFileOnly("DEBUG", $"Removing |{square[i], -13}|");
                        swapper.ListPaintingsAll.RemoveAt(square[i]);
                    }
                }

                if (landscape.Count != 0)
                {
                    logger.LogToFileOnly("DEBUG", $"Removing images from 'landscape' list");
                    for (int i = 0; i < landscape.Count; i++)
                    {
                        logger.LogToFileOnly("DEBUG", $"Removing |{landscape[i], -13}|");
                        swapper.ListPaintingsAll.RemoveAt(landscape[i]);
                    }
                }

                swapper.SyncedToHost = true;
                logger.LogInfo("Sync completed");
            }

            //retrieve seperation state data from event
            if (photonEvent.Code == HostSettingsCode)
            {
                object[] data = (object[])photonEvent.CustomData;
                string HostSeperateState = (string)data[0];
                bool HostRBState = (bool)data[1];
                bool Chaosstate = (bool)data[2];

                logger.LogInfo($"Received seperate state: {HostSeperateState}");
                logger.LogInfo($"Received Rug and Banner state: {HostRBState}");
                logger.LogInfo($"Received chaos state: {Chaosstate}");
                SeperateState = HostSeperateState;
                RBState = HostRBState;
                ChaosState = Chaosstate;


            }

        }
    }
}