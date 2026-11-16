using System.Net;
using System.Text;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Replaces worker-owned Arma admission declarations without discarding restored game settings.</summary>
internal static class Arma3AdmissionConfiguration
{
    /// <summary>Produces deterministic admission lists for the current attributed topology.</summary>
    /// <param name="configuration">The restored server configuration.</param>
    /// <param name="topology">The command's immutable service endpoints.</param>
    /// <returns>The configuration with current headless and local client permissions.</returns>
    /// <exception cref="InvalidDataException">An address or configuration cannot be interpreted safely.</exception>
    public static string Reconcile(string configuration, IReadOnlyCollection<WorkerNodeEndpoint> topology)
    {
        if (topology.Count(endpoint => endpoint.IsPrimary) > 1)
            throw new InvalidDataException("Arma admission topology contains multiple primary endpoints.");
        var primary = topology.SingleOrDefault(endpoint => endpoint.IsPrimary);
        var primaryAddress = primary is null ? null : ParseAddress(primary.Address);
        var addresses = topology
            .Where(endpoint => endpoint.ServiceIdentifier == "headless-client" && !endpoint.IsPrimary)
            .Select(endpoint => ParseAddress(endpoint.Address))
            .Select(address => address.Equals(primaryAddress) ? IPAddress.Loopback : address)
            .Distinct()
            .OrderBy(address => address.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (addresses.Length > 0 && primaryAddress is null)
            throw new InvalidDataException("Arma admission topology has no primary endpoint.");

        var tokens = Tokenize(configuration);
        var removed = new List<(int Start, int End, int FirstToken, int LastToken)>();
        var nesting = new Stack<string>();
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (nesting.Count == 0
                && (string.Equals(token.Value, "headlessClients", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token.Value, "localClient", StringComparison.OrdinalIgnoreCase)))
            {
                var end = ReadDeclaration(tokens, index);
                removed.Add((token.Start, tokens[end].End, index, end));
                index = end;
                continue;
            }
            switch (token.Value)
            {
                case "{":
                case "[":
                case "(":
                    nesting.Push(token.Value);
                    break;
                case "}":
                case "]":
                case ")":
                    var expected = token.Value switch { "}" => "{", "]" => "[", _ => "(" };
                    if (!nesting.TryPop(out var opening) || opening != expected)
                        throw new InvalidDataException("Arma configuration has unbalanced delimiters.");
                    break;
            }
        }
        if (nesting.Count != 0)
            throw new InvalidDataException("Arma configuration has unbalanced delimiters.");

        var result = new StringBuilder();
        var offset = 0;
        foreach (var range in removed)
        {
            result.Append(configuration.AsSpan(offset, range.Start - offset));
            // Keep comments in managed declarations while removing their effective tokens.
            var cursor = range.Start;
            for (var index = range.FirstToken; index <= range.LastToken; index++)
            {
                var token = tokens[index];
                result.Append(configuration.AsSpan(cursor, token.Start - cursor));
                cursor = token.End;
            }
            offset = range.End;
        }
        result.Append(configuration.AsSpan(offset));
        var preserved = result.ToString().TrimEnd();
        return (preserved.Length == 0 ? string.Empty : preserved + Environment.NewLine)
               + Format("headlessClients", addresses)
               + Format("localClient", addresses.Where(IPAddress.IsLoopback));
    }

    /// <summary>Validates an endpoint before inserting it into an Arma string literal.</summary>
    /// <param name="value">The provider-attributed address.</param>
    /// <returns>The normalized IP address.</returns>
    /// <exception cref="InvalidDataException">The value is not a usable endpoint address.</exception>
    private static IPAddress ParseAddress(string value)
    {
        if (!IPAddress.TryParse(value, out var address))
            throw new InvalidDataException("Arma admission topology contains an invalid address.");
        address = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            throw new InvalidDataException("Arma admission topology contains an invalid address.");
        return address;
    }

    /// <summary>Formats one managed array using validated addresses.</summary>
    /// <param name="name">The Arma-owned directive name.</param>
    /// <param name="addresses">The normalized addresses to authorize.</param>
    /// <returns>A complete configuration declaration.</returns>
    private static string Format(string name, IEnumerable<IPAddress> addresses)
        => $"{name}[] = {{{string.Join(',', addresses.Select(address => $"\"{address}\""))}}};"
           + Environment.NewLine;

    /// <summary>Reads a managed literal array and rejects ambiguous assignment syntax.</summary>
    /// <param name="tokens">The significant configuration tokens.</param>
    /// <param name="start">The directive token index.</param>
    /// <returns>The terminating semicolon index.</returns>
    /// <exception cref="InvalidDataException">The managed declaration is malformed.</exception>
    private static int ReadDeclaration(List<(string Value, int Start, int End)> tokens, int start)
    {
        var index = start + 1;
        foreach (var expected in new[] { "[", "]", "=", "{" })
        {
            if (index >= tokens.Count || tokens[index++].Value != expected)
                throw new InvalidDataException("Arma admission declaration is malformed.");
        }
        var expectValue = true;
        while (index < tokens.Count && tokens[index].Value != "}")
        {
            var value = tokens[index++].Value;
            if (expectValue ? !value.StartsWith('"') : value != ",")
                throw new InvalidDataException("Arma admission declaration must contain literal address strings.");
            expectValue = !expectValue;
        }
        if (index + 1 >= tokens.Count || tokens[index].Value != "}" || tokens[index + 1].Value != ";")
            throw new InvalidDataException("Arma admission declaration is unterminated.");
        return index + 1;
    }

    /// <summary>Scans significant tokens while retaining source positions and ignoring comments.</summary>
    /// <param name="text">The source configuration.</param>
    /// <returns>Tokens whose spans leave original comments and whitespace available for preservation.</returns>
    /// <exception cref="InvalidDataException">A string/comment is unterminated or preprocessing is required.</exception>
    private static List<(string Value, int Start, int End)> Tokenize(string text)
    {
        var result = new List<(string Value, int Start, int End)>();
        for (var index = 0; index < text.Length;)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }
            if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                while (index < text.Length && text[index] is not '\r' and not '\n')
                    index++;
                continue;
            }
            if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '*')
            {
                var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                if (end < 0)
                    throw new InvalidDataException("Arma configuration has an unterminated comment.");
                index = end + 2;
                continue;
            }
            if (text[index] == '#')
                throw new InvalidDataException("Arma admission reconciliation cannot safely resolve preprocessor directives.");
            var start = index++;
            if (text[start] == '"')
            {
                var closed = false;
                while (index < text.Length)
                {
                    if (text[index++] != '"')
                        continue;
                    if (index < text.Length && text[index] == '"')
                    {
                        index++;
                        continue;
                    }
                    closed = true;
                    break;
                }
                if (!closed)
                    throw new InvalidDataException("Arma configuration has an unterminated string.");
            }
            else if (char.IsLetter(text[start]) || text[start] == '_')
            {
                while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
                    index++;
            }
            result.Add((text[start..index], start, index));
        }
        return result;
    }
}
