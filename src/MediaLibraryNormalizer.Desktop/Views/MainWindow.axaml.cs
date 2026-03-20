using Avalonia.Controls;
using MediaLibraryNormalizer.Desktop.ViewModels;

namespace MediaLibraryNormalizer.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.MissingEpisodeFinder.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MissingEpisodeFinderViewModel.SelectedSeries)
                    && SeriesListBox.SelectedItem is { } item)
                {
                    SeriesListBox.ScrollIntoView(item);
                }
            };
        }
    }
}