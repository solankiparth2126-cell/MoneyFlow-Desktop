using System;
using System.Windows.Forms;

namespace MoneyFlow.Desktop.Navigation;

/// <summary>
/// Factory contract to dynamically create and resolve Windows Forms using the DI container.
/// </summary>
public interface IFormFactory
{
    /// <summary>
    /// Creates an instance of the specified form type with all injected dependencies satisfied.
    /// </summary>
    TForm Create<TForm>(params object[] parameters) where TForm : Form;

    /// <summary>
    /// Creates an instance of the specified form type with all injected dependencies satisfied.
    /// </summary>
    Form Create(Type formType, params object[] parameters);
}
