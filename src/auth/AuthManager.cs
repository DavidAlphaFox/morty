namespace Morty.Auth;

public class AuthManager
{
    private readonly AuthStore _store;

    public AuthManager()
    {
        _store = new AuthStore();
    }

    public Task LoginAsync(string provider, string apiKey)
    {
        var credentials = _store.Load();
        credentials[provider.ToLower()] = new Credential { Type = "api", Key = apiKey };
        _store.Save(credentials);
        return Task.CompletedTask;
    }

    public string? GetApiKey(string provider)
    {
        var credentials = _store.Load();
        return credentials.TryGetValue(provider.ToLower(), out var cred) ? cred.Key : null;
    }

    public IEnumerable<string> ListProviders()
    {
        return _store.Load().Keys;
    }

    public Task LogoutAsync(string provider)
    {
        var credentials = _store.Load();
        credentials.Remove(provider.ToLower());
        _store.Save(credentials);
        return Task.CompletedTask;
    }
}
