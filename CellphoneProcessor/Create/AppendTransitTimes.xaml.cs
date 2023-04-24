using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace CellphoneProcessor.Create;

/// <summary>
/// Interaction logic for CreateTripsPage.xaml
/// </summary>
public partial class AppendTransitTimes : Page
{
    public AppendTransitTimes()
    {
        InitializeComponent();
    }

    private async void TestCode_Clicked(object sender, RoutedEventArgs e)
    {
        try
        {
            await ((AppendTransitTimesViewModel)DataContext).GetOTPData();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
        }
        finally
        {
        }
    }

    private void SelectTrips_Clicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppendTransitTimesViewModel vm)
        {
            var dialog = new OpenFileDialog()
            {
                Multiselect = false,
            };
            if (dialog.ShowDialog() == true)
            {
                vm.TripFilePath = dialog.FileName;
            }
        }
    }

    private void SelectOutputFile_Clicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppendTransitTimesViewModel vm)
        {
            var dialog = new SaveFileDialog();
            if (dialog.ShowDialog() == true)
            {
                vm.OutputFilePath = dialog.FileName;
            }
        }
    }
}
