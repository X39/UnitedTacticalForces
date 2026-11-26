using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Exercises the bounded parser of the human player records written by the server addon.</summary>
public sealed class Arma3PlayerEvidenceTests
{
    /// <summary>The canonical launch marker used only by these isolated parser fixtures.</summary>
    private const string Nonce = "0123456789abcdef0123456789abcdef";

    /// <summary>Accepts raw engine output, timestamped report records and quoted records.</summary>
    /// <param name="decoration">The valid prefix and suffix fixture.</param>
    [Theory]
    [InlineData("{0}")]
    [InlineData("  9:42:03.123 {0}  ")]
    [InlineData("12:42:03 \"{0}\"")]
    [InlineData("2026/11/16, 12:42:03 {0}")]
    public void Parse_AcceptsCurrentRecordWithAndWithoutDecoration(string decoration)
    {
        var parsed = Arma3PlayerEvidence.Parse(string.Format(decoration, Record(7, "3")), Nonce);

        Assert.Equal(new Arma3PlayerEvidence(7, 3), parsed);
    }

    /// <summary>Accepts the inclusive bounds of the human count.</summary>
    /// <param name="humans">The text of an in-range count.</param>
    /// <param name="expected">The expected parsed count.</param>
    [Theory]
    [InlineData("0", 0)]
    [InlineData("1024", 1024)]
    public void Parse_AcceptsCountBounds(string humans, int expected)
        => Assert.Equal(new Arma3PlayerEvidence(1, expected), Arma3PlayerEvidence.Parse(Record(1, humans), Nonce));

    /// <summary>Rejects malformed counters, counts and decoration rather than promoting ambiguous text.</summary>
    /// <param name="record">A record template with an unsafe protocol field or decoration.</param>
    [Theory]
    [InlineData("UTFPL|2|{0}|1|3")]
    [InlineData("UTFPL|1|{0}|0|3")]
    [InlineData("UTFPL|1|{0}|-1|3")]
    [InlineData("UTFPL|1|{0}|1.0|3")]
    [InlineData("UTFPL|1|{0}|10000000000000000|3")]
    [InlineData("UTFPL|1|{0}|1|1025")]
    [InlineData("UTFPL|1|{0}|1|99999")]
    [InlineData("UTFPL|1|{0}|1|100000")]
    [InlineData("UTFPL|1|{0}|1|-3")]
    [InlineData("UTFPL|1|{0}|1|three")]
    [InlineData("UTFPL|1|{0}|1|")]
    [InlineData("UTFPL|1|{0}|1|3|extra")]
    [InlineData("UTFHC|1|{0}|1|3|1")]
    [InlineData("player chat: UTFPL|1|{0}|1|3")]
    [InlineData("\"UTFPL|1|{0}|1|3")]
    [InlineData("UTFPL|1|{0}|1|3\"")]
    public void Parse_RejectsMalformedRecords(string record)
        => Assert.Null(Arma3PlayerEvidence.Parse(string.Format(record, Nonce), Nonce));

    /// <summary>Requires the exact current launch marker and bounds parser input.</summary>
    [Fact]
    public void Parse_RejectsOldLaunchAndOversizedInput()
    {
        Assert.Null(Arma3PlayerEvidence.Parse(Record(1, "3"), new string('f', 32)));
        Assert.Null(Arma3PlayerEvidence.Parse(Record(1, "3").Replace(Nonce, Nonce.ToUpperInvariant()), Nonce));
        Assert.Null(Arma3PlayerEvidence.Parse(new string(' ', 257) + Record(1, "3"), Nonce));
    }

    /// <summary>Formats trusted protocol fields without adding a terminating newline.</summary>
    /// <param name="sequence">The increasing engine sequence.</param>
    /// <param name="humans">The count text.</param>
    /// <returns>A raw addon record for the current fixture launch.</returns>
    private static string Record(long sequence, string humans) => $"UTFPL|1|{Nonce}|{sequence}|{humans}";
}
