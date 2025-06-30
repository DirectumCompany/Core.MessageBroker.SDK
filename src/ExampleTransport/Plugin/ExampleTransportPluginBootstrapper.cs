using Core.MessageBroker.Transport;
using Core.MessageBroker.Transport.Plugin;
using Core.MessageBroker.Transport.Utils;
using Microsoft.Extensions.DependencyInjection;
using Prise.Plugin;

namespace ExampleTransport.Plugin
{
  /// <summary>
  /// Загрузчик транспортного плагина, настраивает и загружает указанные зависимости.
  /// </summary>
  /// <remarks>
  /// * Атрибут <see cref="PluginBootstrapperAttribute"/> связывает загрузчик с плагином, который необходимо инициализировать;
  /// * Зависимости плагина обозначаются полями с атрибутом <see cref="BootstrapperServiceAttribute"/>;
  /// * Прокси в зависимостях необходимы для работы с сервисами хост-приложения в плагине.
  /// </remarks>
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
    /// Утилиты для работы с приоритетами.
    /// </summary>
    [BootstrapperService(ServiceType = typeof(IPrioritiesUtility), ProxyType = typeof(PrioritiesUtilityProxy))]
    private readonly IPrioritiesUtility _prioritiesUtility;

    /// <summary>
    /// Логгер.
    /// </summary>
    [BootstrapperService(ServiceType = typeof(IPluginLogger), ProxyType = typeof(PluginLoggerProxy))]
    private readonly IPluginLogger _pluginLogger;

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
        .AddSingleton(_prioritiesUtility)
        .AddSingleton(_pluginLogger)
        .AddTransient<IMessageTransportProxy, ExampleTransportProxy>();
    }
  }
}
