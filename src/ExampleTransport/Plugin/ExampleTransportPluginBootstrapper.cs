using System.Net.Http;
using Core.MessageBroker.Transport;
using Core.MessageBroker.Transport.Plugin;
using Core.MessageBroker.Transport.Utils;
using Microsoft.Extensions.DependencyInjection;
using Prise.Plugin;

namespace ExampleTransport.Plugin
{
  /// <summary>
  /// Загрузчик транспортного плагина.
  /// </summary>
  [PluginBootstrapper(PluginType = typeof(ExampleTransportPlugin))]
  public class ExampleTransportPluginBootstrapper : IPluginBootstrapper
  {
    /// <summary>
    /// Сервис конфигурации плагина.
    /// </summary>
    [BootstrapperService(ServiceType = typeof(IConfigurationService), ProxyType = typeof(ConfigurationServiceProxy))]
    private readonly IConfigurationService _configurationService;

    /// <summary>
    /// Утилиты для номера телефона.
    /// </summary>
    [BootstrapperService(ServiceType = typeof(IPhoneNumberUtilities), ProxyType = typeof(PhoneNumberUtilitiesProxy))]
    private readonly IPhoneNumberUtilities _phoneNumberUtilities;

    /// <summary>
    /// Системные часы.
    /// </summary>
    [BootstrapperService(ServiceType = typeof(ISystemClock), ProxyType = typeof(SystemClockProxy))]
    private readonly ISystemClock _systemClock;

    /// <summary>
    /// Фабрика HTTP клиентов.
    /// </summary>
    [BootstrapperService(ServiceType = typeof(IHttpClientFactory), ProxyType = typeof(HttpClientFactoryProxy))]
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Расширяет список сервисов, доступных для плагина из DI.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Расширенная коллекция сервисов.</returns>
    public IServiceCollection Bootstrap(IServiceCollection services)
    {
      return services
        .AddSingleton(_configurationService)
        .AddSingleton(_phoneNumberUtilities)
        .AddSingleton(_systemClock)
        .AddSingleton(_httpClientFactory)
        .AddTransient<IMessageTransportProxy, ExampleTransportProxy>();
    }
  }
}
