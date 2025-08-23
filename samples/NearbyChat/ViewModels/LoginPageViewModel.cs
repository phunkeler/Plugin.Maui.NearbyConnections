using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Data;
using NearbyChat.Models;
using NearbyChat.Pages;

namespace NearbyChat.ViewModels;

public partial class LoginPageViewModel : BaseViewModel
{
    readonly AvatarRepository _avatarRepository;
    readonly ISeedDataService _seedDataService;
    readonly UserRepository _userRepository;

    bool _dataLoaded;
    bool _navigatedTo;

    [ObservableProperty]
    private List<Avatar> _avatars = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    string? _displayName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    Avatar? _selectedAvatar;

    [ObservableProperty]
    bool _isBusy;

    [ObservableProperty]
    bool _isRefreshing;

    public LoginPageViewModel(
        AvatarRepository avatarRepository,
        ISeedDataService seedDataService,
        UserRepository userRepository)
    {
        ArgumentNullException.ThrowIfNull(avatarRepository);
        ArgumentNullException.ThrowIfNull(seedDataService);
        ArgumentNullException.ThrowIfNull(userRepository);

        _avatarRepository = avatarRepository;
        _seedDataService = seedDataService;
        _userRepository = userRepository;
    }

    [RelayCommand]
    void AvatarSelectionChanged(SelectionChangedEventArgs e)
    {
        if (e.PreviousSelection.Count > 0 && e.PreviousSelection[0] is Avatar previousAvatar)
        {
            previousAvatar.BorderColor = Avatar.DefaultBorderColor.ToArgbHex();
            previousAvatar.BorderWidth = Avatar.DefaultBorderWidth;
        }

        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is Avatar currentAvatar)
        {
            currentAvatar.BorderColor = Colors.Red.ToArgbHex();
            currentAvatar.BorderWidth = 4;
        }
    }

    [RelayCommand]
    void NavigatedTo() =>
        _navigatedTo = true;

    [RelayCommand]
    void NavigatedFrom() =>
        _navigatedTo = false;

    [RelayCommand]
    async Task Appearing()
    {
        if (!_dataLoaded)
        {
            await InitData();
            _dataLoaded = true;
        }
        else if (!_navigatedTo)
        {
            await Refresh();
        }
    }

    [RelayCommand(CanExecute = nameof(CanLogin), IncludeCancelCommand = true)]
    async Task Login(CancellationToken cancellationToken = default)
    {
        // Save user info
        await _userRepository.SaveUserAsync(new User
        {
            Id = Guid.NewGuid().ToString(),
            DisplayName = DisplayName!,
            AvatarId = SelectedAvatar!.Id,
            CreatedOn = DateTime.UtcNow.ToString("o")
        }, cancellationToken);

        // Navigate to chat page
        await Shell.Current.GoToAsync($"//{nameof(ChatPage)}");
    }

    [RelayCommand]
    async Task Refresh()
    {
        try
        {
            IsRefreshing = true;
            await LoadData();
        }
        catch
        {
            // Handle exceptions, e.g., show an error message
            Console.WriteLine("Failed to refresh avatars.");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    bool CanLogin()
        => !string.IsNullOrWhiteSpace(DisplayName) && SelectedAvatar != null;

    private async Task InitData()
    {
        var isSeeded = Preferences.Default.ContainsKey("is_seeded");

        if (!isSeeded)
        {
            await _seedDataService.LoadSeedDataAsync();
        }

        Preferences.Default.Set("is_seeded", true);
        await Refresh();
    }

    private async Task LoadData()
    {
        try
        {
            IsBusy = true;
            Avatars = await _avatarRepository.ListAsync();
        }
        catch (Exception ex)
        {
            // Handle exceptions, e.g., log the error or show a message
            Console.WriteLine($"Error loading avatars: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

}