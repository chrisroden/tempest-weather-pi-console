using Avalonia.Controls;
using Tempest.UI.ViewModels;

namespace Tempest.UI.Views;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        DataContext = new SetupWindowViewModel(this);
    }
}
