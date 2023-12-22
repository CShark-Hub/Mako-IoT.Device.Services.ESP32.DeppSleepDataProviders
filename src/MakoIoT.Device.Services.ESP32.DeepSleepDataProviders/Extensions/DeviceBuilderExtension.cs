using MakoIoT.Device.Services.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace MakoIoT.Device.Services.ESP32.DeepSleepDataProviders.Extensions
{

    public static class DeviceBuilderExtension
    {
        public static IDeviceBuilder AddDeepSleepDataProviders(this IDeviceBuilder builder, short deepSleepDisableGpioPinNumber = DeepSleepDataProviderConfiguration.WakeUpDisabled)
        {
            builder.DeviceStarting += Builder_DeviceStarting;
            builder.Services.AddSingleton(typeof(DeepSleepDataProviderConfiguration), new DeepSleepDataProviderConfiguration() { WakeUpGpioPin = deepSleepDisableGpioPinNumber });
            return builder;
        }

        private static void Builder_DeviceStarting(IDevice sender)
        {
            var dp = (DataPublisher)ActivatorUtilities.CreateInstance(sender.ServiceProvider, typeof(DataPublisher));
            dp.InitializeDataProviders(sender);
        }
    }
}
