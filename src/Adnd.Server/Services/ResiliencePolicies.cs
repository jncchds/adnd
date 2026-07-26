namespace Adnd.Server.Services;

public interface IResiliencePolicies
{
    int MaxRetries { get; }
    int RetryDelayMs { get; }
}

public class ResiliencePolicies(IConfiguration config) : IResiliencePolicies
{
    public int MaxRetries => int.Parse(config["Resilience:MaxRetries"] ?? "3");
    public int RetryDelayMs => int.Parse(config["Resilience:RetryDelayMs"] ?? "500");
}
