using System;

namespace MakoIoT.Device.Services.ESP32.DeepSleepDataProviders
{
    public sealed class DeepSleepDataProviderConfig
    {
        public static string SectionName => "DeepSleepDataProvider";

        public TimeSpan SleepTime { get; set; }
    }
}
