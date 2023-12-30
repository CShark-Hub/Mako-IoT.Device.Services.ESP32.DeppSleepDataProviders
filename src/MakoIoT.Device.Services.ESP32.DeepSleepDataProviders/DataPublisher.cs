using MakoIoT.Device.Services.Interface;
using nanoFramework.Hardware.Esp32;
using System;
using System.Device.Gpio;
using Microsoft.Extensions.DependencyInjection;

namespace MakoIoT.Device.Services.ESP32.DeepSleepDataProviders
{
    internal sealed class DataPublisher
    {
        private const byte MaxRetryAttempts = 3;

        private readonly IMessageBus _messageBus;
        private readonly GpioPin _wakeupGpio;
        private readonly Sleep.WakeupGpioPin _wakeupGpioPin;
        private readonly ILog _logger;
        private readonly IConfigurationService _configService;

        public DataPublisher(IMessageBus messageBus, ILog logger,
            IConfigurationService configService, GpioController gpioController, DeepSleepDataProviderConfiguration configuration)
        {
            _messageBus = messageBus;
            _logger = logger;
            _configService = configService;
            if (configuration.WakeUpGpioPin != DeepSleepDataProviderConfiguration.WakeUpDisabled)
            {
                _wakeupGpioPin = MapPinNumberToWakeUpEnum(configuration.WakeUpGpioPin);
                _wakeupGpio = gpioController.OpenPin(configuration.WakeUpGpioPin, PinMode.InputPullDown);
            }
        }

        private void ProviderOnDataReceived(object sender, MessageEventArgs e)
        {
            _logger.Trace($"Message {e.Message.MessageType} received from data provider");
            _messageBus.Publish(e.Message);
        }

        public void InitializeDataProviders(IDevice device)
        {
            try
            {
                var wakeupCause = Sleep.GetWakeupCause();
                if (wakeupCause != Sleep.WakeupCause.Undefined)
                {
                    _logger.Information($"Wake up cause: {wakeupCause}");
                }

                DoWork(device);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
            finally
            {
                if (ShouldGoIntoDeepSleep())
                {
                    var config = (DeepSleepDataProviderConfig)_configService.GetConfigSection(DeepSleepDataProviderConfig.SectionName, typeof(DeepSleepDataProviderConfig));
                    _logger.Information($"Stopping device and going to sleep for {config.SleepTime}");
                    device.Stop();
                    Sleep.EnableWakeupByTimer(config.SleepTime);
                    Sleep.EnableWakeupByMultiPins(_wakeupGpioPin, Sleep.WakeupMode.AnyHigh);
                    Sleep.StartDeepSleep();
                }

                _logger.Information($"Deep sleep is disabled due to pin state.");
            }
        }

        private static Sleep.WakeupGpioPin MapPinNumberToWakeUpEnum(short pinNumber)
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
                var providers = device.ServiceProvider.GetServices(typeof(IDataProvider));
                foreach (IDataProvider dataProvider in providers)
                {
                    _logger.Trace($"Initializing {dataProvider.Id} data provider.");
                    HandleDataProvider(dataProvider);
                }
            }
            catch (Exception ex)
            {
                if (attempt <= MaxRetryAttempts)
                {
                    _logger.Warning(ex, "Error when processing. Executing retry.");
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
                _logger.Error($"Error when calling {provider.GetType().FullName}", ex);
            }
            finally
            {
                provider.DataReceived -= ProviderOnDataReceived;
            }
        }

        private bool ShouldGoIntoDeepSleep()
        {
            if (_wakeupGpio == null)
            {
                return true;
            }

            if (_wakeupGpio.Read() == PinValue.High)
            {
                return false;
            }

            return true;
        }
    }
}
