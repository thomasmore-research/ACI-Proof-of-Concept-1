

namespace ARFoundationRemote.Runtime {
    public static class Constants {
        public const string packageName = "AR Foundation Remote";
        public static string silentStartupErrorsMessage => GetSilentLogMessage("Log Startup Errors");


        public static string GetSilentLogMessage(string settingName) {
            return $"To silent this error, please disable the 'Settings/Debug Settings/{settingName}'.";
        }
    }
}
