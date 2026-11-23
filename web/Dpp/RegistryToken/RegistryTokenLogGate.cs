namespace BatteryPassWeb.Dpp.RegistryToken;

/// <summary>
/// R3 round two, row 4: a refused token (401) is one log line per caller's address per minute, so a flood of junk tokens
/// can't fill the log; the next line says how many were left out. The gate remembers at most
/// <see cref="MaxAddresses"/> addresses; past that it forgets the ones whose minute is over, or all of them.
/// </summary>
public sealed class RegistryTokenLogGate(TimeProvider clock)
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    public const int MaxAddresses = 10_000;

    private readonly Dictionary<string, (DateTimeOffset Since, int Skipped)> _addresses = new(StringComparer.Ordinal);

    /// <summary>The addresses remembered now.</summary>
    public int Count
    {
        get
        {
            lock (_addresses)
            {
                return _addresses.Count;
            }
        }
    }

    /// <summary>Whether this address's 401 is logged now; when it is, how many of its 401s were left out since the last line.</summary>
    public bool Allow(string address, out int skipped)
    {
        ArgumentNullException.ThrowIfNull(address);
        var now = clock.GetUtcNow();
        lock (_addresses)
        {
            if (_addresses.TryGetValue(address, out var entry) && now - entry.Since < Window)
            {
                _addresses[address] = entry with { Skipped = entry.Skipped + 1 };
                skipped = 0;
                return false;
            }

            if (_addresses.Count >= MaxAddresses && !_addresses.ContainsKey(address))
            {
                foreach (var old in _addresses.Where(pair => now - pair.Value.Since >= Window).Select(pair => pair.Key).ToList())
                {
                    _addresses.Remove(old);
                }

                if (_addresses.Count >= MaxAddresses)
                {
                    _addresses.Clear();
                }
            }

            skipped = _addresses.TryGetValue(address, out entry) ? entry.Skipped : 0;
            _addresses[address] = (now, 0);
            return true;
        }
    }
}
