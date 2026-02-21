using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ProxyBridge.GUI.ViewModels;

namespace ProxyBridge.GUI.Views;

public partial class ProxySettingsWindow : Window
{
    private bool _isUpdatingFromViewModel = false;

    public ProxySettingsWindow()
    {
        InitializeComponent();

        this.DataContextChanged += OnDataContextChanged;

        this.Opened += (s, e) =>
        {
            if (DataContext is ProxySettingsViewModel vm)
                UpdateEditTypeComboBox(vm.NewType);
        };
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is ProxySettingsViewModel vm)
        {
            vm.PropertyChanged += ViewModel_PropertyChanged;

            var editComboBox = this.FindControl<ComboBox>("EditTypeComboBox");
            if (editComboBox != null)
            {
                editComboBox.SelectionChanged -= EditTypeComboBox_SelectionChanged;
                editComboBox.SelectionChanged += EditTypeComboBox_SelectionChanged;
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ProxySettingsViewModel vm && e.PropertyName == nameof(ProxySettingsViewModel.NewType))
        {
            UpdateEditTypeComboBox(vm.NewType);
        }
    }

    private void UpdateEditTypeComboBox(string typeTag)
    {
        var comboBox = this.FindControl<ComboBox>("EditTypeComboBox");
        if (comboBox == null) return;

        _isUpdatingFromViewModel = true;
        foreach (var obj in comboBox.Items)
        {
            if (obj is ComboBoxItem item && item.Tag is string tag &&
                tag.Equals(typeTag, System.StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                break;
            }
        }
        _isUpdatingFromViewModel = false;
    }

    private void EditTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingFromViewModel) return;

        if (sender is ComboBox comboBox &&
            comboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag &&
            DataContext is ProxySettingsViewModel vm)
        {
            vm.NewType = tag;
        }
    }
}
