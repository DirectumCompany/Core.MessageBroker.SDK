# Описание комплекта (SDK) для разработки плагина к MessageBroker

## Комплект поставки

В комплект поставки входят файлы для разработки .NET-приложения плагина брокера сообщений

ExampleTransport - пример плагина

## Реализация плагина брокера сообщений

При разработке плагина используйте пример плагина из комплекта поставки.

Необходимо придумать корректное и осмысленное название для плагина и при реализации по примеру ExampleTransport во всех местах проекта необходимо изменить наименование 'Example' на придуманное имя.
В качестве примера можно привести гипотетическую компанию Contoso. В этом случае упоминания ExampleTransport, ExampleTransportProxy и т.д., будут заменены на ContosoTransport, ContosoTransportProxy и т.д. во всём проекте, включая настройки в appsettings.json и docker-compose.

Состав примера плагина:

* **Models** - модели;
* **Plugin** - файлы плагинной системы;
* **Configuration** - настройки транспортного посредника;
* **ConfigurationValidator** - валидатор настроек;
* **ExampleTransportProxy** - транспортный посредник.

В качестве TargetFramework рекомендуется использовать netstandard2.1 (не обязательно, но очень желательно). Если используется другой таргет, то лучше собирать nuget-пакет с опцией self-contained.

1. Необходимо реализовать транспортный посредник для передачи информации между провайдером и брокером сообщений.
1. При реализации посредника понадобятся модели и настройки транспортного посредника.
1. Для проверки корректности настроек, необходимо добавить валидатор, который проверяет как базовые, так и дополнительные настройки, необходимые для конкретного транспортного посредника.
1. В проект необходимо добавить транспортный плагин и загрузчик, по аналогии с файлами Plugin из примера.

Важно корректно настроить файл проекта плагина `*.csproj`, для этого необходимо добавить:

* блок с описанием пакета;
* блок c копированием в пакет документации, символов отладки и dll зависимостей;
* блок с внешними библиотеками, где необходимо указать зависимость от nuget-пакета транспорта `Core.MessageBroker.Transport.*.nupkg`, идущего в комплекте дистрибутива MessageBroker: `<PackageReference Include="Core.MessageBroker.Transport" Version="*" />`
* блок с основными настройками:
  * `<TargetFramework>netstandard2.1</TargetFramework>` заменить на используемый TargetFramework;
  * `<AssemblyName>ExampleTransport</AssemblyName>` заменить на наименование транспортного посредника;
  * `<RootNamespace>ExampleTransport</RootNamespace>`;
  * `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>`;
  * `<GenerateDocumentationFile>true</GenerateDocumentationFile>`;
  * `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`;
  * `<TargetsForTfmSpecificContentInPackage>$(TargetsForTfmSpecificContentInPackage);IncludeInPackage</TargetsForTfmSpecificContentInPackage>`.

### Содержание модели сообщения Message

Message:

* `string` **Id** - идентификатор сообщения;
* `string` **Title** - заголовок сообщения;
* `string` **Content** - содержимое сообщения;
* `MessagePriority` **Priority** - приоритет сообщения:
  * `enum MessagePriority`:
    * `Low = -1` - низкий приоритет;
    * `Normal = 0` - нормальный приоритет;
    * `High = 1` - высокий приоритет.
* `DateTimeOffset` **BestBefore** - срок действительности сообщения. Если сообщение не удалось отправить за указанное время, оно помечается как сообщение с истёкшим скором действия и не будет отправлено;
* `Dictionary<string, string>` **Properties** - свойства сообщения. Параметр используется в push-уведомлениях. Также поле может использоваться для передачи специфических параметров сообщения;
* `List<string>` **Tags** - тэги сообщения. Параметр пока не используется;
* `MessageIdentity` **Identity** - получатель сообщения:
  * `class MessageIdentity`:
    * `string` **CredentialType** - тип реквизита удостоверения получателя. В стандартной реализации заполняется значениями ClaimTypes `HTTP://SCHEMAS.XMLSOAP.ORG/WS/2005/05/IDENTITY/CLAIMS/MOBILEPHONE` и `HTTP://SCHEMAS.XMLSOAP.ORG/WS/2005/05/IDENTITY/CLAIMS/EMAILADDRESS` из пространства `System.Security.Claims`. В собственной реализации можно использовать другие значения;
    * `string` **CredentialValue** - значение реквизита удостоверения получателя:
      * для реквизитов с типом `HTTP://SCHEMAS.XMLSOAP.ORG/WS/2005/05/IDENTITY/CLAIMS/MOBILEPHONE` выполняется нормализация значения. Так номера телефонов, которые начинаются на '7' или '8' длиной 11 цифр, считаются валидными. В остальных случаях первым символом всегда должен быть '+' и удовлетворять международному формату.
* `List<DeviceInfo>` **DevicesInfo** - информация об устройствах пользователя. Параметр используется для отправки push-уведомлений:
  * `class DeviceInfo`:
    * `string` **DeviceId** - идентификатор устройства;
    * `Dictionary<string, string>` **TokensInfo** - информация о токенах:
      * ключом словаря является название системы отправки push-уведомлений. Возможные значения: 'FCM', 'RuStore';
      * значением словаря являются пользовательские токены устройств.

### Исключения, которые можно использовать в плагине

* **IncorrectMessageDataException** - исключение, используемое при некорректных данных в сообщении;
* **IncorrectPhoneNumberException** - исключение, используемое при некорректном номере телефона;
* **InvalidCredentialTypeException** - исключение, используемое при указании неверного типа реквизита получателя;
* **MessageDeliveryException** - исключение, используемое при ошибке доставки сообщения;
* **MessagePendingException** - исключение, используемое при временной приостановке отправки корректного сообщения;
* **MessageTransmitBlockedException** - исключение, используемое при возникновении блокировки со стороны транспорта;
* **MessageTransmitException** - исключение, используемое при неудачной попытке отправки сообщения;
* **TransportAuthorizeException** - исключение, используемое при ошибках авторизации транспорта;
* **PriorityLimitException** - исключение, используемое при достижение лимита сообщений по приоритету;
* **PushDeliveryException** - исключение, используемое при отправке push-уведомления;
* **TransportAuthorizeException** - исключение, используемое при ошибках авторизации транспорта;
* **TransportPendingException** - исключение, используемое при ожидании транспорта.

## Подключение плагина к MessageBroker

Для подключения плагина предварительно соберите реализованный проект в nuget-пакет. Далее действуйте, исходя из используемой ОС.

### Windows

1. В папке с планировщиком заданий `CoreMessageBroker\Scheduler` в конфигурационном файле `appsettings.json` заполните параметры.
1. Собранный в результате сборки, nupkg плагина скопировать в директорию указанную в настройках, по умолчанию это `./Plugins` проекта `CoreMessageBroker.Scheduler`.
1. Укажите название плагина транспортного посредника в настройке `*DeliveryProxy`, где вместо `*` нужно указать тип сообщений, отправка которых реализуется в плагине. Пример для `SmsDeliveryProxy`:

```json
  "Transport": {
    ...
    "SmsDeliveryProxy": "ExampleTransportPlugin",
    "ExampleTransportPlugin": {
      "Setting1": "",
      "Setting2": "",
      "Setting3": "",
      ...
    },
    ...
  }
```

### Linux

1. Укажите название плагина транспортного посредника в настройке `Transport__*DeliveryProxy`, где `*` нужно указать тип сообщений, отправка которых реализуется в плагине.
1. Добавьте собранный nupkg с плагином в образ:

* примонтируйте папку, в которой находится плагин;
* переопределите `entrypoint` для копирования плагинов из `volume` в основную директорию.

  ```yml
  message-broker-scheduler:
    image: registry.directum.ru/hrpro/directum.message.scheduler:*.*.*.*
    entrypoint: "sh -c 'cp /Plugins/*nupkg /app/Plugins && dotnet Core.MessageBroker.Scheduler.dll'"
    environment:
      ...
      Transport__*DeliveryProxy: "ExampleTransportPlugin"

      Transport__Proxies__ExampleTransportPlugin__Setting1: ""
      Transport__Proxies__ExampleTransportPlugin__Setting2: ""
      Transport__Proxies__ExampleTransportPlugin__Setting3: ""
      ...
    volumes:
      - ./Plugins:/Plugins
    ports:
      - "*:*"
  ```
