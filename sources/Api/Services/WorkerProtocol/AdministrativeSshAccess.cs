using System.Net;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Validates and projects installation-owned, key-only emergency SSH access for deployment hosts.
/// </summary>
[Singleton<AdministrativeSshAccess>]
public sealed class AdministrativeSshAccess(IOptions<WorkerProvisioningOptions> options)
{
    private readonly WorkerProvisioningOptions _options = options.Value;

    public IReadOnlyList<RuntimeInboundNetworkRequirement> AddTo(
        IReadOnlyList<RuntimeInboundNetworkRequirement> gameRequirements)
    {
        if (!_options.AdministrativeSshEnabled)
            return gameRequirements;
        Validate();
        return
        [
            ..gameRequirements,
            new RuntimeInboundNetworkRequirement(
                ERuntimeNetworkProtocol.Tcp,
                22,
                22,
                "UTF emergency administrator SSH",
                _options.AdministrativeSshSourceCidrs),
        ];
    }

    public string? GetCommand(string publicAddress)
    {
        if (!_options.AdministrativeSshEnabled || string.IsNullOrWhiteSpace(publicAddress))
            return null;
        Validate();
        var host = IPAddress.TryParse(publicAddress, out var address)
                   && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{publicAddress}]"
            : publicAddress;
        return $"ssh {_options.AdministrativeSshUser}@{host}";
    }

    public string RenderCloudInitFragment()
    {
        if (!_options.AdministrativeSshEnabled)
            return string.Empty;
        Validate();
        var keys = string.Join(
            Environment.NewLine,
            _options.AdministrativeSshAuthorizedKeys.Select(q => $"      - '{q.Replace("'", "''")}'"));
        return $"""
               users:
                 - default
                 - name: {_options.AdministrativeSshUser}
                   groups: [ adm, sudo ]
                   shell: /bin/bash
                   sudo: ALL=(ALL) NOPASSWD:ALL
                   lock_passwd: true
                   ssh_authorized_keys:
               {keys}
               ssh_pwauth: false
               disable_root: true
               """;
    }

    private void Validate()
    {
        if (!_options.AdministrativeSshUser.All(q => char.IsAsciiLetterOrDigit(q) || q is '-' or '_')
            || _options.AdministrativeSshUser.Length is < 1 or > 32
            || _options.AdministrativeSshAuthorizedKeys.Length is < 1 or > 32
            || _options.AdministrativeSshAuthorizedKeys.Any(q => !IsPublicKey(q))
            || _options.AdministrativeSshSourceCidrs.Length is < 1 or > 32
            || _options.AdministrativeSshSourceCidrs.Any(q => !q.Contains('/') || !IPNetwork.TryParse(q, out _)))
        {
            throw new InvalidOperationException(
                "Administrative SSH requires a safe user name, at least one valid OpenSSH public key, and at least "
                + "one valid source CIDR.");
        }
    }

    private static bool IsPublicKey(string value)
    {
        if (value.Length is < 32 or > 8192 || value.Any(char.IsControl))
            return false;
        var fields = value.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 2
            || fields[0] is not "ssh-ed25519" and not "ssh-rsa"
            && !fields[0].StartsWith("ecdsa-sha2-", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            return Convert.FromBase64String(fields[1]).Length >= 16;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
