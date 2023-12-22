using System;

namespace MakoIoT.Device.Services.ESP32.DeepSleepDataProviders
{
    public sealed class DeepSleepDataProviderConfiguration
    {
        internal const short WakeUpDisabled = -1;
        public short WakeUpGpioPin { get; set; }
    }
}
