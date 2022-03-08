#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using ARFoundationRemote.Runtime;
using Telepathy;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;
using EventType = Telepathy.EventType;


namespace ARFoundationRemote.RuntimeEditor {
    public class TelepathyReceiverConnection : TelepathyConnection, IReceiverConnection {
        readonly Client client = new Client();


        protected override IEnumerator startConnectionCor() {
            var ip = Settings.Instance.ARCompanionAppIP;
            if (!IPAddress.TryParse(ip, out _)) {
                Debug.LogError("Please enter correct AR Companion app IP in Assets/Plugins/ARFoundationRemoteInstaller/Resources/Settings");
                yield break;
            }
            
            client.MaxMessageSize = maxMessageSize;
            client.Connect(ip, port);
            
            while (client.Connecting) {
                yield return null;
            }
            
            if (Settings.Instance.logStartupErrors && !isConnected_internal) {
                Debug.LogError($"{Constants.packageName}: connection to AR Companion app failed. Please check that:\n" +
                               "1. Unity Editor and AR Device are on the same Wi-Fi network.\n" +
                               "2. AR Companion is running and device is unlocked.\n" +
                               "3. The IP is correct in Assets/Plugins/ARFoundationRemoteInstaller/Resources/Settings\n\n" +
                               "If the connection is still failing, please try to configure your AR Device's Wi-Fi to have a static IP.\n" +
                               "iOS: https://www.mobi-pos.com/web/guide/settings/static-ip-configuration\n" +
                               "Android: https://service.uoregon.edu/TDClient/2030/Portal/KB/ArticleDet?ID=33742\n\n" +
                               "OR\n" +
                               "Try to create a hotspot on your AR device and then connect your computer to it.\n\n" +
                               "OR" +
                               "Try a wired connection (see Documentation).");
            }
        }

        protected override Common getCommon() {
            return client;
        }

        protected override bool isConnected_internal => client.Connected;

        protected override void send(byte[] payload) {
            Assert.IsTrue(isConnected_internal);
            client.Send(payload);
        }

        ResponseType IReceiverConnection.BlockUntilReceive<ResponseType>(BlockingMessage payload) {
            BlockingCallsFrequency.Check();
            Assert.IsFalse(payload.blockingMessageGuid.HasValue);
            var guid = Guid.NewGuid();
            Assert.AreNotEqual(Guid.Empty, guid);
            payload.blockingMessageGuid = guid;
            Send(payload);
            
            var stopwatch = Stopwatch.StartNew();
            while (true) {
                if (!isConnected_internal) {
                    throw new Exception($"{Constants.packageName}: please don't call blocking methods while AR Companion is not connected");
                }
                
                if (stopwatch.Elapsed > TimeSpan.FromSeconds(timeoutInSeconds)) {
                    throw new Exception($"{Constants.packageName}: BlockUntilReceive() timeout.");
                }

                foreach (var msg in incomingMessages) {
                    if (msg.eventType == EventType.Data) {
                        if (msg.message is ResponseType response && response.blockingMessageGuid == guid) {
                            return response;
                        }
                    }
                }
            }
        }
        
        const double timeoutInSeconds = 10;

        protected override void onDestroyInternal() {
            client.Disconnect();
        }


        static class BlockingCallsFrequency {
            static readonly RollingFilter<object> rollingFilter = new RollingFilter<object>(1);
            
            
            public static void Check() {
                rollingFilter.AddNewEntryAndRemoveOldEntries(null);
                if (rollingFilter.entries.Count > 10) {
                    Debug.LogError($"{Constants.packageName}: you're calling synchronous AR Foundation API too often.\n" +
                                        "When the synchronous AR Foundation API is called, the plugin blocks main Unity Editor thread and waits for AR Companion app's response. Examples:\n" +
                                        " - ARCameraManager.currentConfiguration \n" +
                                        " - ARCameraManager.GetConfigurations() \n" +
                                        " - ARAnchorManager.AddAnchor() \n" +
                                        " - ARAnchorManager.TryRemoveAnchor() \n" +
                                        "Please call synchronous API less frequently to prevent Editor freezes.\n");
                }
            }
        }


        void IReceiverConnection.BlockUntilSent(object payload) {
            var stopwatch = Stopwatch.StartNew();
            Send(payload);
            while (client.IsSendingData) {
                if (!isConnected_internal) {
                    throw new Exception($"{Constants.packageName}: please don't call blocking methods while AR Companion is not connected");
                }
                
                if (stopwatch.Elapsed > TimeSpan.FromSeconds(timeoutInSeconds)) {
                    throw new Exception($"{Constants.packageName}: BlockUntilSent timeout.");
                }
                
                Thread.Sleep(1000/60);
            }
        }
    }
}
#endif
