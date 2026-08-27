using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Sentinel.Desktop.ViewModels;

namespace Sentinel.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.IsPaletteOpen))
            return;
        if (DataContext is MainWindowViewModel { IsPaletteOpen: true })
            Dispatcher.UIThread.Post(() => PaletteSearchBox?.Focus(), DispatcherPriority.Input);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.IsPaletteOpen && e.Key == Key.Enter)
        {
            vm.ExecutePaletteItemCommand.Execute(vm.SelectedPaletteItem);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
