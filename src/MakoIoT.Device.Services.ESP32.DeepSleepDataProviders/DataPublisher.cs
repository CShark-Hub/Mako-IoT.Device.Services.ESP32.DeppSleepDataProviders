using MakoIoT.Device.Services.Interface;
using Microsoft.Extensions.Logging;
using nanoFramework.Hardware.Esp32;
using System;
using System.Device.Gpio;
using nanoFramework.Runtime.Native;
using Microsoft.Extensions.DependencyInjection;

namespace MakoIoT.Device.Services.ESP32.DeepSleepDataProviders
{
    public class DataPublisher
    {
        private const byte MaxRetryAttepmpts = 3;

        private readonly IMessageBus _messageBus;
        private readonly IServiceProvider _serviceProvider;
        private readonly INetworkProvider _networkProvider;
        private readonly GpioPin _deepSleepDisablePin;
        private readonly ILogger _logger;
        private readonly DeepSleepDataProviderConfig _config;

        public DataPublisher(IMessageBus messageBus, IServiceProvider serviceCollection, ILogger logger,
            IConfigurationService configService, INetworkProvider networkProvider, GpioController gpioController)
        {
            _messageBus = messageBus;
            _serviceProvider = serviceCollection;
            _logger = logger;
            _config = (DeepSleepDataProviderConfig)configService.GetConfigSection(DeepSleepDataProviderConfig.SectionName, typeof(DeepSleepDataProviderConfig));
            _networkProvider = networkProvider;
            if (_config.DisableDeepSleepGpioPin != 0)
            {
                // Will throw exception for invalid pin number
                var _ = MapPinNumberToWakeUpEnum(_config.DisableDeepSleepGpioPin);
                _deepSleepDisablePin = gpioController.OpenPin(_config.DisableDeepSleepGpioPin);
            }
        }

        private void ProviderOnDataReceived(object sender, MessageEventArgs e)
        {
            _logger.LogDebug($"Message {e.Message.MessageType} received from data provider");
            _messageBus.Publish(e.Message);
        }

        public void InitializeDataProviders(IDevice device)
        {
            try
            {
                var wakeupCause = Sleep.GetWakeupCause();
                if (wakeupCause != Sleep.WakeupCause.Undefined)
                {
                    _logger.LogInformation($"Wake up cause: {wakeupCause}");
                }

                DoWork(device);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error.");
            }
            finally
            {
                _logger.LogDebug($"Stoping device and going to sleep for {_config.SleepTime}");
                Sleep.EnableWakeupByTimer(_config.SleepTime);
                Sleep.StartDeepSleep();
            }
        }

        private static Sleep.WakeupGpioPin MapPinNumberToWakeUpEnum(byte pinNumber)
        {
            return pinNumber switch
            {
                0 => Sleep.WakeupGpioPin.Pin0,
                2 => Sleep.WakeupGpioPin.Pin2,
                4 => Sleep.WakeupGpioPin.Pin4,
                12 => Sleep.WakeupGpioPin.Pin12,
                13 => Sleep.WakeupGpioPin.Pin13,
                14 => Sleep.WakeupGpioPin.Pin14,
                15 => Sleep.WakeupGpioPin.Pin15,
                25 => Sleep.WakeupGpioPin.Pin25,
                26 => Sleep.WakeupGpioPin.Pin26,
                27 => Sleep.WakeupGpioPin.Pin27,
                32 => Sleep.WakeupGpioPin.Pin32,
                33 => Sleep.WakeupGpioPin.Pin33,
                34 => Sleep.WakeupGpioPin.Pin34,
                35 => Sleep.WakeupGpioPin.Pin35,
                36 => Sleep.WakeupGpioPin.Pin36,
                37 => Sleep.WakeupGpioPin.Pin37,
                38 => Sleep.WakeupGpioPin.Pin38,
                39 => Sleep.WakeupGpioPin.Pin39,
                _ => throw new NotSupportedException($"Pin number {pinNumber} is not supported by ESP."),
            };
        }

        private void DoWork(IDevice device, byte attempt = 0)
        {
            try
            {
                if (!_networkProvider.IsConnected)
                {
                    _logger.LogError("Network is not connected. Restarting device.");
                    Power.RebootDevice();
                    return;
                }

                var providers = _serviceProvider.GetServices(typeof(IDataProvider));
                foreach (IDataProvider dataProvider in providers)
                {
                    _logger.LogDebug($"Initializing {dataProvider.Id} data provider.");
                    HandleDataProvider(dataProvider);
                }

                if (ShouldNotGoIntoDeepSleep())
                {
                    _logger.LogInformation($"Deep sleep is disabled due to pin state.");
                    return;
                }

                _logger.LogDebug("Stoping device");
                device.Stop();
                _logger.LogDebug("Device stopped");
            }
            catch (Exception ex)
            {
                if (attempt <= MaxRetryAttepmpts)
                {
                    _logger.LogWarning(ex, "Error when processing. Executing retry.");
                    DoWork(device, ++attempt);
                    return;
                }

                throw;
            }
        }

        private void HandleDataProvider(IDataProvider provider)
        {
            try
            {
                provider.DataReceived += ProviderOnDataReceived;
                provider.GetData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error when calling {provider.GetType().FullName}");
            }
            finally
            {
                provider.DataReceived -= ProviderOnDataReceived;
            }
        }

        private bool ShouldNotGoIntoDeepSleep()
        {
            if (_deepSleepDisablePin == null)
            {
                return false;
            }

            if (_deepSleepDisablePin.Read() == PinValue.High)
            {
                return true;
            }

            return false;
        }
    }
}
