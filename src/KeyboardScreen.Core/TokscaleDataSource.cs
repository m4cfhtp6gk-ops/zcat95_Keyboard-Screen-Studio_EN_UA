using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace KeyboardScreen.Core;

public enum TokscaleStatus
{
    NotInstalled,
    Ready,
    NoData,
    Error
}

public enum TokscaleDataKind
{
    Subscription,
    Model
}

public sealed record TokscaleDataOption(
    string Key,
    TokscaleDataKind Kind,
    string DisplayText,
    string Provider,
    string Client,
    string Model,
    decimal Tokens,
    decimal Cost,
    double? RemainingPercent,
    string MetricLabel,
    string Plan,
    DateTimeOffset? ResetsAt);

public sealed record TokscaleCatalog(
    TokscaleStatus Status,
    IReadOnlyList<TokscaleDataOption> Options,
    string Message,
    DateTimeOffset UpdatedAt,
    string? ExecutablePath = null)
{
    public static TokscaleCatalog NotInstalled(string message) =>
        new(TokscaleStatus.NotInstalled, [], message, DateTimeOffset.Now);
}

public sealed class TokscaleDataSource
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TokscaleCatalog? _cached;
    private DateTimeOffset _cacheExpiresAt;

    public async Task<TokscaleCatalog> ReadCatalogAsync(
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (!force && _cached is not null && DateTimeOffset.Now < _cacheExpiresAt)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!force && _cached is not null && DateTimeOffset.Now < _cacheExpiresAt)
            {
                return _cached;
            }

            string? executable = FindExecutable();
            if (executable is null)
            {
                return Cache(TokscaleCatalog.NotInstalled(
                    Loc.T("TokscaleNotDetected")));
            }

            CommandResult usage = await RunAsync(executable, "usage --json", cancellationToken);
            CommandResult models = await RunAsync(
                executable,
                "models --json --group-by client,provider,model",
                cancellationToken);

            var options = new List<TokscaleDataOption>();
            if (usage.Success)
            {
                options.AddRange(ParseUsage(usage.StandardOutput));
            }
            if (models.Success)
            {
                options.AddRange(ParseModels(models.StandardOutput));
            }

            TokscaleCatalog catalog;
            if (options.Count > 0)
            {
                catalog = new TokscaleCatalog(
                    TokscaleStatus.Ready,
                    options,
                    Loc.T("TokscaleReadCount", options.Count),
                    DateTimeOffset.Now,
                    executable);
            }
            else if (usage.Success || models.Success)
            {
                catalog = new TokscaleCatalog(
                    TokscaleStatus.NoData,
                    [],
                    Loc.T("TokscaleNoData"),
                    DateTimeOffset.Now,
                    executable);
            }
            else
            {
                string detail = FirstUsefulError(usage, models);
                catalog = new TokscaleCatalog(
                    TokscaleStatus.Error,
                    [],
                    string.IsNullOrWhiteSpace(detail)
                        ? Loc.T("TokscaleReadFailed")
                        : Loc.T("TokscaleReadFailedDetail", detail),
                    DateTimeOffset.Now,
                    executable);
            }

            return Cache(catalog);
        }
        finally
        {
            _gate.Release();
        }
    }

    public static IReadOnlyList<TokscaleDataOption> Filter(
        TokscaleCatalog catalog,
        AiUsageDataKind kind) =>
        catalog.Options
            .Where(option => kind == AiUsageDataKind.SubscriptionRemaining
                ? option.Kind == TokscaleDataKind.Subscription
                : option.Kind == TokscaleDataKind.Model)
            .OrderByDescending(option => kind == AiUsageDataKind.ModelCost ? option.Cost : option.Tokens)
            .ThenBy(option => option.DisplayText, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public static AiQuotaSnapshot CreateSnapshot(
        AiQuotaSettings settings,
        TokscaleCatalog catalog)
    {
        IReadOnlyList<TokscaleDataOption> candidates = Filter(catalog, settings.DataKind);
        TokscaleDataOption? option = candidates.FirstOrDefault(item =>
            string.Equals(item.Key, settings.SelectedItemKey, StringComparison.Ordinal))
            ?? candidates.FirstOrDefault();
        if (option is null)
        {
            return AiQuotaSnapshot.Empty;
        }

        string name = string.IsNullOrWhiteSpace(settings.DisplayName)
            ? DefaultDisplayName(option)
            : settings.DisplayName.Trim();

        if (settings.DataKind == AiUsageDataKind.SubscriptionRemaining)
        {
            return new AiQuotaSnapshot(
                true,
                name,
                AiAccessType.Subscription,
                option.RemainingPercent ?? 0,
                ResetPeriod: option.ResetsAt.HasValue ? AiResetPeriod.Custom : AiResetPeriod.None,
                ResetsAt: option.ResetsAt,
                MetricDisplay: string.IsNullOrWhiteSpace(option.Plan)
                    ? "TOKSCALE · SUBSCRIPTION"
                    : $"TOKSCALE · {option.Plan.ToUpperInvariant()}");
        }

        decimal value = settings.DataKind == AiUsageDataKind.ModelCost ? option.Cost : option.Tokens;
        decimal target = Math.Max(0, settings.ProgressTarget);
        double fill = target > 0 ? (double)Math.Clamp(value / target * 100m, 0m, 100m) : 0d;
        string valueDisplay = settings.DataKind == AiUsageDataKind.ModelCost
            ? FormatMoney(value)
            : FormatCompact(value);
        string metric = settings.DataKind == AiUsageDataKind.ModelCost ? "COST" : "TOKENS";
        string metricDisplay = target > 0
            ? $"{metric} · {Math.Round(fill):0}% TARGET"
            : $"TOKSCALE · {metric}";

        return new AiQuotaSnapshot(
            true,
            name,
            AiAccessType.ApiKey,
            fill,
            ValueDisplay: valueDisplay,
            MetricDisplay: metricDisplay);
    }

    public static IReadOnlyList<TokscaleDataOption> ParseUsage(string json)
    {
        var options = new List<TokscaleDataOption>();
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement providers = root.ValueKind == JsonValueKind.Array
            ? root
            : TryGet(root, "providers", out JsonElement nested) && nested.ValueKind == JsonValueKind.Array
                ? nested
                : default;
        if (providers.ValueKind != JsonValueKind.Array)
        {
            return options;
        }

        foreach (JsonElement providerElement in providers.EnumerateArray())
        {
            string provider = ReadString(providerElement, "provider", "name") ?? "AI";
            string account = ReadString(providerElement, "account", "email") ?? string.Empty;
            string plan = ReadString(providerElement, "plan") ?? string.Empty;
            if (!TryGet(providerElement, "metrics", out JsonElement metrics) || metrics.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement metric in metrics.EnumerateArray())
            {
                double? remaining = ReadDouble(metric, "remaining_percent", "remainingPercent");
                double? used = ReadDouble(metric, "used_percent", "usedPercent");
                remaining ??= used.HasValue ? 100d - used.Value : null;
                if (!remaining.HasValue)
                {
                    continue;
                }

                string label = ReadString(metric, "label", "name") ?? "Quota";
                DateTimeOffset? resetsAt = ReadDate(metric, "resets_at", "resetsAt");
                string key = $"subscription:{provider}:{account}:{label}".ToLowerInvariant();
                string display = JoinNonEmpty(" · ", provider, label, account);
                options.Add(new TokscaleDataOption(
                    key, TokscaleDataKind.Subscription, display, provider, string.Empty,
                    string.Empty, 0, 0, Math.Clamp(remaining.Value, 0, 100), label, plan, resetsAt));
            }
        }

        return options;
    }

    public static IReadOnlyList<TokscaleDataOption> ParseModels(string json)
    {
        var options = new List<TokscaleDataOption>();
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!TryGet(root, "entries", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return options;
        }

        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string client = ReadString(entry, "client", "source") ?? string.Empty;
            string provider = ReadString(entry, "provider") ?? string.Empty;
            string model = ReadString(entry, "model") ?? "Unknown model";
            decimal tokens = ReadDecimal(entry, "total", "totalTokens", "tokens")
                ?? Sum(entry, "input", "output", "cacheRead", "cacheWrite", "reasoning");
            decimal cost = ReadDecimal(entry, "cost", "totalCost") ?? 0;
            string key = $"model:{client}:{provider}:{model}".ToLowerInvariant();
            string display = JoinNonEmpty(" · ", model, provider, client);
            options.Add(new TokscaleDataOption(
                key, TokscaleDataKind.Model, display, provider, client, model,
                Math.Max(0, tokens), Math.Max(0, cost), null, string.Empty, string.Empty, null));
        }

        return options;
    }

    private TokscaleCatalog Cache(TokscaleCatalog catalog)
    {
        _cached = catalog;
        _cacheExpiresAt = DateTimeOffset.Now.AddSeconds(60);
        return catalog;
    }

    private static string DefaultDisplayName(TokscaleDataOption option)
    {
        string value = option.Kind == TokscaleDataKind.Subscription ? option.Provider : option.Model;
        return string.IsNullOrWhiteSpace(value) ? "AI" : value.Trim();
    }

    private static string? FindExecutable()
    {
        string fileName = OperatingSystem.IsWindows() ? "tokscale.cmd" : "tokscale";
        var candidates = new List<string>();
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            candidates.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(folder => Path.Combine(folder.Trim('"'), fileName)));
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            candidates.Add(Path.Combine(appData, "npm", "tokscale.cmd"));
            candidates.Add(Path.Combine(appData, "npm", "tokscale.exe"));
        }
        else
        {
            candidates.Add(Path.Combine(home, ".local", "bin", "tokscale"));
            candidates.Add("/opt/homebrew/bin/tokscale");
            candidates.Add("/usr/local/bin/tokscale");
            candidates.Add("/usr/bin/tokscale");
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static async Task<CommandResult> RunAsync(
        string executable,
        string arguments,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        bool commandShim = OperatingSystem.IsWindows() &&
            (executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
             executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase));
        string fileName = commandShim
            ? Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe"
            : executable;
        string actualArguments = commandShim
            ? $"/d /s /c \"\"{executable}\" {arguments}\""
            : arguments;
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = actualArguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (OperatingSystem.IsWindows())
        {
            string nodeFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs");
            if (Directory.Exists(nodeFolder))
            {
                string currentPath = startInfo.Environment.TryGetValue("PATH", out string? current)
                    ? current ?? string.Empty
                    : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                startInfo.Environment["PATH"] = nodeFolder + Path.PathSeparator + currentPath;
            }
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(timeout.Token);
            return new CommandResult(process.ExitCode == 0, await outputTask, await errorTask);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CommandResult(false, string.Empty, Loc.T("CommandTimedOut"));
        }
        catch (Exception ex)
        {
            return new CommandResult(false, string.Empty, ex.Message);
        }
    }

    private static string FirstUsefulError(params CommandResult[] results) =>
        results.Select(result => result.StandardError.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (TryGet(element, name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        return null;
    }

    private static double? ReadDouble(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (!TryGet(element, name, out JsonElement value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number)) return number;
            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return number;
        }
        return null;
    }

    private static decimal? ReadDecimal(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (!TryGet(element, name, out JsonElement value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal number)) return number;
            if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return number;
        }
        return null;
    }

    private static DateTimeOffset? ReadDate(JsonElement element, params string[] names)
    {
        string? value = ReadString(element, names);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
            ? parsed
            : null;
    }

    private static decimal Sum(JsonElement element, params string[] names) =>
        names.Sum(name => ReadDecimal(element, name) ?? 0m);

    private static string JoinNonEmpty(string separator, params string[] values) =>
        string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));

    private static string FormatMoney(decimal value) => value >= 1000m
        ? $"${value:0,0}"
        : $"${value:0.00}";

    private static string FormatCompact(decimal value)
    {
        decimal absolute = Math.Abs(value);
        if (absolute >= 1_000_000_000m) return $"{value / 1_000_000_000m:0.#}B";
        if (absolute >= 1_000_000m) return $"{value / 1_000_000m:0.#}M";
        if (absolute >= 1_000m) return $"{value / 1_000m:0.#}K";
        return value.ToString("0", CultureInfo.InvariantCulture);
    }

    private sealed record CommandResult(bool Success, string StandardOutput, string StandardError);
}
