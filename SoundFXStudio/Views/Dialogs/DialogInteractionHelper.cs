using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SoundFXStudio.Views.Dialogs;

public static class DialogInteractionHelper
{
    public static bool IsInteractiveElement(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is ButtonBase
                or TextBoxBase
                or ComboBox
                or ListBox
                or ListView
                or MenuItem
                or CheckBox
                or TabItem
                or Slider
                or PasswordBox
                or ScrollBar
                or Thumb
                or ScrollViewer)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
