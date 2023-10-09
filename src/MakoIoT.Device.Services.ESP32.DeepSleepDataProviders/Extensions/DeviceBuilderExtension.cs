using MakoIoT.Device.Services.Interface;
using nanoFramework.DependencyInjection;

namespace MakoIoT.Device.Services.ESP32.DeepSleepDataProviders.Extensions
{

    public static class DeviceBuilderExtension
    {
        public static IDeviceBuilder AddDeepSleepDataProviders(this IDeviceBuilder builder)
        {
            builder.DeviceStarting += Builder_DeviceStarting;
            return builder;
        }

        private static void Builder_DeviceStarting(IDevice sender)
        {
            var dp = (DataPublisher)ActivatorUtilities.CreateInstance(sender.ServiceProvider, typeof(DataPublisher));
            dp.InitializeDataProviders(sender);
        }
    }
}
