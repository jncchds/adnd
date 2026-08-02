using System.Net;
using System.Net.Sockets;

namespace Adnd.Server.Services;

public interface IOutboundUrlGuard
{
    /// <summary>Throws <see cref="ArgumentException"/> if the server must not fetch this URL.</summary>
    void EnsureAllowed(string? url);
}

/// <summary>
/// Validates caller-supplied URLs before the server fetches them.
/// "Query the models at this endpoint" is otherwise a server-side request forgery
/// primitive: an authenticated user could aim it at the cloud metadata service or any
/// host on the Docker network and read the response back through the API.
///
/// Private ranges are allowed by default because self-hosted inference endpoints
/// (Ollama on localhost, LM Studio on the LAN) are the normal case for this app.
/// Deployments open to untrusted users should set Security:AllowPrivateLlmEndpoints=false.
/// The cloud metadata range is always blocked — nothing legitimate lives there.
/// </summary>
public class OutboundUrlGuard(IConfiguration config) : IOutboundUrlGuard
{
    private readonly bool _allowPrivate =
        !bool.TryParse(config["Security:AllowPrivateLlmEndpoints"], out var allow) || allow;

    public void EnsureAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("An endpoint URL is required.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Endpoint URL is not a valid absolute URL.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Endpoint URL must use http or https.");

        // A hostname can resolve to a blocked address, so check the resolved addresses
        // rather than trusting the literal text of the host.
        foreach (var address in Resolve(uri.DnsSafeHost))
        {
            if (IsMetadataOrUnspecified(address))
                throw new ArgumentException($"Endpoint URL resolves to a blocked address ({address}).");

            if (!_allowPrivate && IsPrivate(address))
                throw new ArgumentException($"Endpoint URL resolves to a private address ({address}).");
        }
    }

    private static IEnumerable<IPAddress> Resolve(string host)
    {
        if (IPAddress.TryParse(host, out var literal))
            return [literal];

        try
        {
            return Dns.GetHostAddresses(host);
        }
        catch (SocketException)
        {
            throw new ArgumentException($"Endpoint host '{host}' could not be resolved.");
        }
    }

    /// <summary>Link-local (cloud instance metadata) and the unspecified range — never legitimate.</summary>
    private static bool IsMetadataOrUnspecified(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6) return IsMetadataOrUnspecified(address.MapToIPv4());
            return address.IsIPv6LinkLocal || address.Equals(IPAddress.IPv6Any);
        }

        var b = address.GetAddressBytes();
        return b[0] == 0 || (b[0] == 169 && b[1] == 254);
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6) return IsPrivate(address.MapToIPv4());
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;
            return (address.GetAddressBytes()[0] & 0xFE) == 0xFC; // fc00::/7 unique local
        }

        var b = address.GetAddressBytes();
        return b[0] switch
        {
            10 => true,                                 // 10.0.0.0/8
            127 => true,                                // loopback
            172 when b[1] >= 16 && b[1] <= 31 => true,  // 172.16.0.0/12 (Docker default bridge)
            192 when b[1] == 168 => true,               // 192.168.0.0/16
            _ => b[0] >= 224                            // multicast + reserved
        };
    }
}
