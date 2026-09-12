using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace MoneyFlow.Desktop.Navigation;

/// <summary>
/// Default implementation of IFormFactory leveraging Microsoft.Extensions.DependencyInjection.
/// </summary>
public class FormFactory : IFormFactory
{
    private readonly IServiceProvider _serviceProvider;

    public FormFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public TForm Create<TForm>(params object[] parameters) where TForm : Form
    {
        if (parameters == null || parameters.Length == 0)
        {
            return _serviceProvider.GetRequiredService<TForm>();
        }

        return ActivatorUtilities.CreateInstance<TForm>(_serviceProvider, parameters);
    }

    public Form Create(Type formType, params object[] parameters)
    {
        if (formType == null) throw new ArgumentNullException(nameof(formType));

        if (parameters == null || parameters.Length == 0)
        {
            return (Form)_serviceProvider.GetRequiredService(formType);
        }

        return (Form)ActivatorUtilities.CreateInstance(_serviceProvider, formType, parameters);
    }
}
