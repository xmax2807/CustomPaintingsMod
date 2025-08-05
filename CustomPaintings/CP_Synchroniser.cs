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

            logger.LogInfo("sharing seed with other clients");

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

            logger.LogInfo("sharing seperation setting");

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
            if (swapper.GetModState() == CP_Swapper.ModState.Host)
                return; 

            logger.LogInfo("requesting host sync");

            RaiseEventOptions options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.MasterClient, // Send to Host
            };

            // Raise the event to Host
            PhotonNetwork.RaiseEvent(SyncRequestCode, "", options, SendOptions.SendReliable);

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

                int ClientID = photonEvent.Sender;
                logger.LogInfo($"Client {ClientID} requested a host sync");
                NewClientSync(ClientID, swapper.ListUsedPaintingsAllPrevRound, swapper.ListUsedPaintingsPortraitPrevRound, swapper.ListUsedPaintingsSquarePrevRound, swapper.ListUsedPaintingsLandscapePrevRound);
            }

            if (photonEvent.Code == NewClientSyncCode)
            {                
                if (swapper.GetModState() == CP_Swapper.ModState.Host)
                {
                    logger.LogInfo("Data succesfully sent after a host sync request");
                    return;
                }               

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
                    logger.LogToFileOnly("DEBUG", $"removing images from 'all' list");
                    for (int i = 0; i < all.Count; i++)
                    {
                        swapper.ListPaintingsAll.RemoveAt(all[i]);
                        logger.LogToFileOnly("DEBUG", $"{all[i]}");
                    }
                }

                if (portrait.Count != 0)
                {
                    logger.LogToFileOnly("DEBUG", $"removing images from 'portrait' list");
                    for (int i = 0; i < portrait.Count; i++)
                    {
                        swapper.ListPaintingsAll.RemoveAt(portrait[i]);
                        logger.LogToFileOnly("DEBUG", $"{portrait[i]}");
                    }
                }

                if (square.Count != 0)
                {
                    logger.LogToFileOnly("DEBUG", $"removing images from 'square' list");
                    for (int i = 0; i < square.Count; i++)
                    {
                        swapper.ListPaintingsAll.RemoveAt(square[i]);
                        logger.LogToFileOnly("DEBUG", $"{square[i]}");
                    }
                }

                if (landscape.Count != 0)
                {
                    logger.LogToFileOnly("DEBUG", $"removing images from 'landscape' list");
                    for (int i = 0; i < landscape.Count; i++)
                    {
                        swapper.ListPaintingsAll.RemoveAt(landscape[i]);
                        logger.LogToFileOnly("DEBUG", $"{landscape[i]}");
                    }
                }

                swapper.SyncedToHost = true;
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