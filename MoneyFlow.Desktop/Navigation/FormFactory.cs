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
        TForm form;
        if (parameters == null || parameters.Length == 0)
        {
            form = _serviceProvider.GetRequiredService<TForm>();
        }
        else
        {
            form = ActivatorUtilities.CreateInstance<TForm>(_serviceProvider, parameters);
        }

        return EnsureFormIcon(form);
    }

    public Form Create(Type formType, params object[] parameters)
    {
        if (formType == null) throw new ArgumentNullException(nameof(formType));

        Form form;
        if (parameters == null || parameters.Length == 0)
        {
            form = (Form)_serviceProvider.GetRequiredService(formType);
        }
        else
        {
            form = (Form)ActivatorUtilities.CreateInstance(_serviceProvider, formType, parameters);
        }

        return EnsureFormIcon(form);
    }

    private static T EnsureFormIcon<T>(T form) where T : Form
    {
        try
        {
            if (form.Icon == null || form.Icon == SystemIcons.Application)
            {
                var icon = Styling.ExecLedgerIcons.GetAppIcon();
                if (icon != null)
                {
                    form.Icon = icon;
                    form.ShowIcon = true;
                }
            }
        }
        catch
        {
            // Ignore if running under non-interactive or unit test contexts
        }

        return form;
    }
}
