using System.Windows;
using System.Windows.Controls;
using SoundFXStudio.Views.Dialogs;
using Xunit;

namespace SoundFXStudio.Tests;

public class DialogInteractionTests
{
    [Fact]
    public void IsInteractiveElement_ReturnsTrue_ForButtonControls()
    {
        Exception? threadException = null;

        var thread = new Thread(() =>
        {
            try
            {
                var button = new Button();
                var text = new TextBlock { Text = "Save" };
                button.Content = text;

                Assert.True(DialogInteractionHelper.IsInteractiveElement(button));
                Assert.False(DialogInteractionHelper.IsInteractiveElement(text));
                Assert.False(DialogInteractionHelper.IsInteractiveElement(new Border()));
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadException is not null)
        {
            throw threadException;
        }
    }
}
