using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public class Throttler {
        readonly float maxFps;
        float latSendTime;


        public Throttler(float maxFps) {
            this.maxFps = maxFps;
        }

        bool CanSend {
            get {
                if (Time.time - latSendTime > 1f / maxFps) {
                    latSendTime = Time.time;
                    return true;
                } else {
                    return false;
                }
            }
        }

        public bool CanSendNonCriticalMessage => Connection.CanSendNonCriticalMessage && CanSend;
    }
}
