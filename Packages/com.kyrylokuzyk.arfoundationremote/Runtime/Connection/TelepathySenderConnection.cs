using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using Telepathy;


namespace ARFoundationRemote.Runtime {
    public class TelepathySenderConnection : TelepathyConnection {
        readonly Server server = new Server();


        [SuppressMessage("ReSharper", "IteratorNeverReturns")]
        protected override IEnumerator startConnectionCor() {
            server.MaxMessageSize = maxMessageSize;
            while (true) {
                if (!isActive) {
                    server.Start(port);
                }
                
                yield return new WaitForSeconds(1);
            }
        }
        
        protected override Common getCommon() {
            return server;
        }

        protected override bool isConnected_internal => Interlocked.CompareExchange(ref connectionId, 0, 0) != -1;
        
        protected override void send(byte[] payload) {
            if (isConnected_internal) {
                server.Send(connectionId, payload);
            }
        }

        public bool isActive => server.Active;

        protected override void onDestroyInternal() {
            server.Stop();
        }
        
        public void ResetConnection(IConnectionDelegate del) {
            UnityEngine.Assertions.Assert.AreEqual(connectionDelegate, del);
            connectionDelegate = null;
            incomingMessages = new System.Collections.Concurrent.ConcurrentQueue<IncomingMessage>();
            outgoingMessages = new System.Collections.Concurrent.ConcurrentQueue<object>();
        }
    }
}
