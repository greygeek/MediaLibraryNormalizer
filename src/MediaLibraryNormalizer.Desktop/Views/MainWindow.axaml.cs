using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using MediaLibraryNormalizer.Desktop.ViewModels;

namespace MediaLibraryNormalizer.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.MissingEpisodeFinder.PropertyChanged -= OnMissingEpisodeFinderPropertyChanged;
            _viewModel.MissingEpisodeFinder.ActivityLog.CollectionChanged -= OnMissingEpisodeActivityLogChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.MissingEpisodeFinder.PropertyChanged += OnMissingEpisodeFinderPropertyChanged;
        _viewModel.MissingEpisodeFinder.ActivityLog.CollectionChanged += OnMissingEpisodeActivityLogChanged;
        ScrollMissingEpisodeActivityLogToTail();
    }

    private void OnMissingEpisodeFinderPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MissingEpisodeFinderViewModel.SelectedSeries)
            && SeriesListBox.SelectedItem is { } item)
        {
            SeriesListBox.ScrollIntoView(item);
        }

        if (_viewModel is null)
        {
            return;
        }

        if ((args.PropertyName == nameof(MissingEpisodeFinderViewModel.FollowActivityLogTail)
                && _viewModel.MissingEpisodeFinder.FollowActivityLogTail)
            || (args.PropertyName == nameof(MissingEpisodeFinderViewModel.SelectedInspectorTabIndex)
                && _viewModel.MissingEpisodeFinder.SelectedInspectorTabIndex == 1
                && _viewModel.MissingEpisodeFinder.FollowActivityLogTail))
        {
            ScrollMissingEpisodeActivityLogToTail();
        }
    }

    private void OnMissingEpisodeActivityLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel?.MissingEpisodeFinder.FollowActivityLogTail != true)
        {
            return;
        }

        ScrollMissingEpisodeActivityLogToTail();
    }

    private void ScrollMissingEpisodeActivityLogToTail()
    {
        if (_viewModel is null || _viewModel.MissingEpisodeFinder.ActivityLog.Count == 0)
        {
            return;
        }

        var item = _viewModel.MissingEpisodeFinder.ActivityLog[^1];
        Dispatcher.UIThread.Post(() => MissingEpisodeActivityLogListBox.ScrollIntoView(item));
    }
}