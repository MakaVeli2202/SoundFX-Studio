using System.Windows.Controls;
using SoundFXStudio.ViewModels;

namespace SoundFXStudio.Controls;

public partial class GamingPanel : UserControl
{
    public GamingPanel()
    {
        InitializeComponent();
    }

    internal GamingViewModel? ViewModel => DataContext as GamingViewModel;
}
