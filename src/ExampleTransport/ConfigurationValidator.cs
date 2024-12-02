using System;
using FluentValidation;

namespace ExampleTransport
{
  /// <summary>
  /// Валидатор настроек транспортного посредника для Example.
  /// </summary>
  internal class ConfigurationValidator : AbstractValidator<Configuration>
  {
    /// <summary>
    /// Инициализирует валидатор настроек.
    /// </summary>
    public ConfigurationValidator()
    {
      RuleFor(cfg => cfg)
        .NotNull()
        .WithMessage("конфигурация плагина должна быть заполнена.")
        .DependentRules(() =>
        {
          RuleFor(cfg => cfg.Host)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Custom((host, context) =>
            {
              if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
                context.AddFailure(nameof(Configuration.Host), "неправильное имя хоста.");
            });

          RuleFor(cfg => cfg.Port)
            .ExclusiveBetween(0, 65536);

          RuleFor(cfg => cfg.Username)
            .NotEmpty();

          RuleFor(cfg => cfg.Password)
            .NotEmpty();

          RuleFor(cfg => cfg.Sender)
            .NotEmpty();
        });
    }
  }
}
