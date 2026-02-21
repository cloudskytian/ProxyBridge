using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Input;
using ProxyBridge.GUI.Services;
using ProxyBridge.GUI.Common;

namespace ProxyBridge.GUI.ViewModels;

public class ProxySettingsViewModel : ViewModelBase
{
    private readonly Loc _loc = Loc.Instance;
    public Loc Loc => _loc;

    private readonly ObservableCollection<ProxyConfig> _proxyConfigs;
    private readonly ProxyBridgeService? _proxyService;
    private readonly Action? _onConfigsChanged;
    private readonly Action? _onClose;

    // edit panel state
    private bool _isEditPanelOpen;
    private uint _editingConfigId;

    private string _newType = "SOCKS5";
    private string _newHost = "";
    private string _newPort = "";
    private string _newUsername = "";
    private string _newPassword = "";
    private string _hostError = "";
    private string _portError = "";

    // test panel state
    private bool _isTestViewOpen;
    private string _testTargetHost = "google.com";
    private string _testTargetPort = "80";
    private string _testOutput = "";
    private bool _isTesting;
    private uint _testingConfigId;

    public ObservableCollection<ProxyConfig> ProxyConfigs => _proxyConfigs;

    public bool IsEditPanelOpen
    {
        get => _isEditPanelOpen;
        set => SetProperty(ref _isEditPanelOpen, value);
    }

    public string EditPanelTitle => _editingConfigId > 0 ? "Edit Proxy Config" : "Add Proxy Config";

    public string NewType
    {
        get => _newType;
        set => SetProperty(ref _newType, value);
    }

    public string NewHost
    {
        get => _newHost;
        set { SetProperty(ref _newHost, value); HostError = ""; }
    }

    public string NewPort
    {
        get => _newPort;
        set { SetProperty(ref _newPort, value); PortError = ""; }
    }

    public string NewUsername
    {
        get => _newUsername;
        set => SetProperty(ref _newUsername, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public string HostError
    {
        get => _hostError;
        set => SetProperty(ref _hostError, value);
    }

    public string PortError
    {
        get => _portError;
        set => SetProperty(ref _portError, value);
    }

    public bool IsTestViewOpen
    {
        get => _isTestViewOpen;
        set => SetProperty(ref _isTestViewOpen, value);
    }

    public string TestTargetHost
    {
        get => _testTargetHost;
        set => SetProperty(ref _testTargetHost, value);
    }

    public string TestTargetPort
    {
        get => _testTargetPort;
        set => SetProperty(ref _testTargetPort, value);
    }

    public string TestOutput
    {
        get => _testOutput;
        set => SetProperty(ref _testOutput, value);
    }

    public bool IsTesting
    {
        get => _isTesting;
        set => SetProperty(ref _isTesting, value);
    }

    public ICommand OpenAddPanelCommand { get; }
    public ICommand SaveConfigCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand EditConfigCommand { get; }
    public ICommand DeleteConfigCommand { get; }
    public ICommand OpenTestPanelCommand { get; }
    public ICommand CloseTestPanelCommand { get; }
    public ICommand StartTestCommand { get; }
    public ICommand CloseCommand { get; }

    public ProxySettingsViewModel(
        ObservableCollection<ProxyConfig> proxyConfigs,
        ProxyBridgeService? proxyService,
        Action? onConfigsChanged,
        Action? onClose)
    {
        _proxyConfigs = proxyConfigs;
        _proxyService = proxyService;
        _onConfigsChanged = onConfigsChanged;
        _onClose = onClose;

        OpenAddPanelCommand = new RelayCommand(() =>
        {
            _editingConfigId = 0;
            OnPropertyChanged(nameof(EditPanelTitle));
            NewType = "SOCKS5";
            NewHost = "";
            NewPort = "";
            NewUsername = "";
            NewPassword = "";
            HostError = "";
            PortError = "";
            IsEditPanelOpen = true;
        });

        SaveConfigCommand = new RelayCommand(() =>
        {
            bool valid = ValidationHelper.ValidateIpOrDomain(NewHost, IsValidIpOrDomain, msg => HostError = msg)
                      && ValidationHelper.ValidatePort(NewPort, msg => PortError = msg);
            if (!valid) return;

            if (!ushort.TryParse(NewPort, out ushort portNum)) return;

            if (_editingConfigId > 0)
            {
                if (_proxyService != null)
                    _proxyService.EditProxyConfig(_editingConfigId, NewType, NewHost, portNum, NewUsername, NewPassword);

                var existing = FindConfig(_editingConfigId);
                if (existing != null)
                {
                    existing.Type = NewType;
                    existing.Host = NewHost;
                    existing.Port = NewPort;
                    existing.Username = NewUsername;
                    existing.Password = NewPassword;
                }
            }
            else
            {
                uint newId = 0;
                if (_proxyService != null)
                    newId = _proxyService.AddProxyConfig(NewType, NewHost, portNum, NewUsername, NewPassword);

                if (newId == 0) newId = (uint)(DateTime.Now.Ticks & 0xFFFFFFFF);

                _proxyConfigs.Add(new ProxyConfig
                {
                    Id = newId,
                    Type = NewType,
                    Host = NewHost,
                    Port = NewPort,
                    Username = NewUsername,
                    Password = NewPassword
                });
            }

            IsEditPanelOpen = false;
            _onConfigsChanged?.Invoke();
        });

        CancelEditCommand = new RelayCommand(() =>
        {
            IsEditPanelOpen = false;
        });

        EditConfigCommand = new RelayCommandWithParameter<ProxyConfig>(config =>
        {
            if (config == null) return;
            _editingConfigId = config.Id;
            OnPropertyChanged(nameof(EditPanelTitle));
            NewType = config.Type;
            NewHost = config.Host;
            NewPort = config.Port;
            NewUsername = config.Username;
            NewPassword = config.Password;
            HostError = "";
            PortError = "";
            IsEditPanelOpen = true;
        });

        DeleteConfigCommand = new RelayCommandWithParameter<ProxyConfig>(config =>
        {
            if (config == null) return;
            if (_proxyService != null)
                _proxyService.DeleteProxyConfig(config.Id);
            _proxyConfigs.Remove(config);
            _onConfigsChanged?.Invoke();
        });

        OpenTestPanelCommand = new RelayCommandWithParameter<ProxyConfig>(config =>
        {
            if (config == null) return;
            _testingConfigId = config.Id;
            TestOutput = "";
            IsTestViewOpen = true;
        });

        CloseTestPanelCommand = new RelayCommand(() =>
        {
            IsTestViewOpen = false;
        });

        StartTestCommand = new RelayCommand(async () =>
        {
            if (IsTesting) return;
            if (string.IsNullOrWhiteSpace(TestTargetHost)) { TestOutput = "ERROR: Enter target host"; return; }
            if (!ushort.TryParse(TestTargetPort, out ushort targetPortNum)) { TestOutput = "ERROR: Invalid target port"; return; }

            IsTesting = true;
            TestOutput = "Testing...\n";
            try
            {
                if (_proxyService != null)
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        TestOutput = _proxyService.TestProxyConfig(_testingConfigId, TestTargetHost, targetPortNum);
                    });
                }
                else
                {
                    TestOutput = "ERROR: Proxy service not available";
                }
            }
            catch (Exception ex)
            {
                TestOutput += $"\nERROR: {ex.Message}";
            }
            finally
            {
                IsTesting = false;
            }
        });

        CloseCommand = new RelayCommand(() => _onClose?.Invoke());
    }

    private ProxyConfig? FindConfig(uint configId)
    {
        foreach (var c in _proxyConfigs)
            if (c.Id == configId) return c;
        return null;
    }

    private static bool IsValidIpOrDomain(string input)
    {
        if (IPAddress.TryParse(input, out _)) return true;
        var domainRegex = new Regex(@"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?$");
        return domainRegex.IsMatch(input);
    }
}
