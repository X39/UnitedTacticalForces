using System.Text;

namespace X39.UnitedTacticalForces.Common;

// ReSharper disable once InconsistentNaming
public readonly record struct TS3AddressBuilder(string Host, ushort Port = 9987)
{
    public TS3AddressBuilder WithPassword(string password) => this with {Password = password};
    public TS3AddressBuilder WithChannel(string channel) => this with {Channel = channel, ChannelPassword = null};

    public TS3AddressBuilder WithChannel(string channel, string channelPassword) =>
        this with {Channel = channel, ChannelPassword = channelPassword};
    public string? Password { get; init; }
    public string? Channel { get; init; }
    public string? ChannelPassword { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder("ts3server://");
        sb.Append(Host);
        sb.Append('?');
        sb.Append("port=");
        sb.Append(Port);
        if (!string.IsNullOrWhiteSpace(Password))
        {
            sb.Append("&password=");
            sb.Append(Password);
        }
        if (!string.IsNullOrWhiteSpace(Channel))
        {
            sb.Append("&channel=");
            sb.Append(Channel);
        }
        if (!string.IsNullOrWhiteSpace(ChannelPassword))
        {
            sb.Append("&channelpassword=");
            sb.Append(ChannelPassword);
        }
        return sb.ToString();
    }
    public static implicit operator string(TS3AddressBuilder ts3AddressBuilder) => ts3AddressBuilder.ToString();
}