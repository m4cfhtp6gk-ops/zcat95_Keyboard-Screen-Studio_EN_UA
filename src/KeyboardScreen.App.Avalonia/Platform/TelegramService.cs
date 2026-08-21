using KeyboardScreen.Core;
using TL;

namespace KeyboardScreen.App.Avalonia.Platform;

public enum TelegramState
{
    Disconnected,
    Connecting,
    WaitingForCode,
    WaitingForPassword,
    Connected,
    Failed
}

/// <summary>
/// The signed-in Telegram account, via WTelegramClient (MTProto). The session
/// auth key is the most sensitive piece of data the app stores: it lives in a
/// file next to the settings, never leaves this machine, and logging out
/// deletes it. Login is interactive: the library asks for the code (and the
/// 2FA password when set) through <see cref="SubmitCode"/> /
/// <see cref="SubmitPassword"/>.
/// </summary>
public sealed class TelegramService : IDisposable
{
    private readonly object _gate = new();
    private readonly string _sessionPath;
    private WTelegram.Client? _client;
    private WTelegram.UpdateManager? _manager;
    private TaskCompletionSource<string>? _pendingInput;
    private TelegramSettings _settings = new();
    private readonly Dictionary<long, DateTime> _muteCache = [];
    private readonly Dictionary<long, DateTime> _muteCheckedAt = [];

    public TelegramService(string sessionDirectory)
    {
        Directory.CreateDirectory(sessionDirectory);
        _sessionPath = Path.Combine(sessionDirectory, "telegram.session");
        WTelegram.Helpers.Log = static (_, _) => { };
    }

    public TelegramState State { get; private set; } = TelegramState.Disconnected;

    public string? LastError { get; private set; }

    public bool HasStoredSession => File.Exists(_sessionPath);

    public event EventHandler? StateChanged;

    public event EventHandler<TelegramMessageEvent>? MessageReceived;

    public void Connect(TelegramSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            if (State is TelegramState.Connecting or TelegramState.WaitingForCode or TelegramState.WaitingForPassword)
            {
                return;
            }

            _settings = settings;
            SetState(TelegramState.Connecting);
        }

        _ = Task.Run(LoginAsync);
    }

    public void SubmitCode(string code) => CompletePendingInput(code);

    public void SubmitPassword(string password) => CompletePendingInput(password);

    public async Task LogoutAsync()
    {
        WTelegram.Client? client;
        lock (_gate)
        {
            client = _client;
            _client = null;
            _manager = null;
            _pendingInput?.TrySetCanceled();
            _pendingInput = null;
        }

        if (client is not null)
        {
            try
            {
                await client.Auth_LogOut();
            }
            catch
            {
                // Logging out of a dead session still ends with a wiped key below.
            }

            client.Dispose();
        }

        TryDeleteSession();
        LastError = null;
        SetState(TelegramState.Disconnected);
    }

    private async Task LoginAsync()
    {
        try
        {
            WTelegram.Client client;
            lock (_gate)
            {
                _client?.Dispose();
                _client = new WTelegram.Client(Config);
                client = _client;
            }

            var me = await client.LoginUserIfNeeded();
            lock (_gate)
            {
                if (!ReferenceEquals(_client, client))
                {
                    return;
                }

                _manager = client.WithUpdateManager(OnUpdateAsync);
            }

            _ = me;
            LastError = null;
            SetState(TelegramState.Connected);
        }
        catch (OperationCanceledException)
        {
            SetState(TelegramState.Disconnected);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            lock (_gate)
            {
                _client?.Dispose();
                _client = null;
                _manager = null;
            }

            SetState(TelegramState.Failed);
        }
    }

    private string? Config(string what)
    {
        switch (what)
        {
            case "api_id":
                return _settings.ApiId.Trim();
            case "api_hash":
                return _settings.ApiHash.Trim();
            case "phone_number":
                return _settings.PhoneNumber.Trim();
            case "session_pathname":
                return _sessionPath;
            case "verification_code":
                return WaitForInput(TelegramState.WaitingForCode);
            case "password":
                return WaitForInput(TelegramState.WaitingForPassword);
            default:
                return null;
        }
    }

    /// <summary>Blocks the library's login task until the UI hands over the value.</summary>
    private string WaitForInput(TelegramState prompt)
    {
        TaskCompletionSource<string> pending;
        lock (_gate)
        {
            pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingInput = pending;
        }

        SetState(prompt);
        string value = pending.Task.GetAwaiter().GetResult();
        SetState(TelegramState.Connecting);
        return value;
    }

    private void CompletePendingInput(string value)
    {
        TaskCompletionSource<string>? pending;
        lock (_gate)
        {
            pending = _pendingInput;
            _pendingInput = null;
        }

        pending?.TrySetResult(value.Trim());
    }

    private async Task OnUpdateAsync(Update update)
    {
        if (update is not UpdateNewMessage { message: Message message } ||
            message.flags.HasFlag(Message.Flags.out_) ||
            string.IsNullOrWhiteSpace(message.message) && message.media is null)
        {
            return;
        }

        if (await IsMutedAsync(message.peer_id))
        {
            return;
        }

        MessageReceived?.Invoke(this, new TelegramMessageEvent(
            ResolveSenderName(message),
            string.IsNullOrWhiteSpace(message.message) ? Loc.T("TelegramMediaMessage") : message.message,
            DateTimeOffset.Now));
    }

    private string ResolveSenderName(Message message)
    {
        WTelegram.UpdateManager? manager;
        lock (_gate)
        {
            manager = _manager;
        }

        long fromId = message.from_id?.ID ?? message.peer_id.ID;
        if (manager is not null)
        {
            if (manager.Users.TryGetValue(fromId, out User? user))
            {
                string name = (user.first_name + " " + user.last_name).Trim();
                return name.Length > 0 ? name : user.username ?? "Telegram";
            }

            if (manager.Chats.TryGetValue(message.peer_id.ID, out ChatBase? chat))
            {
                return chat.Title;
            }
        }

        return "Telegram";
    }

    /// <summary>Muted chats never pop up; verdicts are cached for ten minutes.</summary>
    private async Task<bool> IsMutedAsync(Peer peer)
    {
        WTelegram.Client? client;
        WTelegram.UpdateManager? manager;
        lock (_gate)
        {
            client = _client;
            manager = _manager;
            if (_muteCheckedAt.TryGetValue(peer.ID, out DateTime checkedAt) &&
                DateTime.UtcNow - checkedAt < TimeSpan.FromMinutes(10))
            {
                return _muteCache.TryGetValue(peer.ID, out DateTime muteUntil) && muteUntil > DateTime.UtcNow;
            }
        }

        if (client is null)
        {
            return false;
        }

        try
        {
            InputPeer? inputPeer = ResolveInputPeer(peer, manager);
            if (inputPeer is null)
            {
                return false;
            }

            PeerNotifySettings notify = await client.Account_GetNotifySettings(new InputNotifyPeer { peer = inputPeer });
            DateTime muteUntil = notify.mute_until == default ? DateTime.MinValue : notify.mute_until;
            lock (_gate)
            {
                _muteCache[peer.ID] = muteUntil;
                _muteCheckedAt[peer.ID] = DateTime.UtcNow;
            }

            return muteUntil > DateTime.UtcNow;
        }
        catch
        {
            // If the check fails, showing the popup beats silently dropping it.
            return false;
        }
    }

    private static InputPeer? ResolveInputPeer(Peer peer, WTelegram.UpdateManager? manager)
    {
        if (manager is null)
        {
            return null;
        }

        return peer switch
        {
            PeerUser { user_id: var userId } when manager.Users.TryGetValue(userId, out User? user) =>
                new InputPeerUser(user.id, user.access_hash),
            PeerChat { chat_id: var chatId } => new InputPeerChat(chatId),
            PeerChannel { channel_id: var channelId } when manager.Chats.TryGetValue(channelId, out ChatBase? chat) && chat is Channel channel =>
                new InputPeerChannel(channel.id, channel.access_hash),
            _ => null
        };
    }

    private void SetState(TelegramState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TryDeleteSession()
    {
        try
        {
            if (File.Exists(_sessionPath))
            {
                File.Delete(_sessionPath);
            }
        }
        catch
        {
            // A locked session file is deleted on the next logout attempt.
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _pendingInput?.TrySetCanceled();
            _pendingInput = null;
            _client?.Dispose();
            _client = null;
            _manager = null;
        }
    }
}
