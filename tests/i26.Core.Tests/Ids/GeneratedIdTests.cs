using System.Text.Json;
using i26.Core.Ids;
using i26.Core.Ids.Json;

namespace i26.Core.Tests.Ids;

/// <summary>
/// The generated ids, used as ids. Nothing here mentions the generator: the point is that an id it
/// wrote is indistinguishable from one written by hand, so every one of these would pass against
/// either.
/// </summary>
public class GeneratedIdTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new TypedIdJsonConverterFactory());
        return options;
    }

    [Fact]
    public void The_declaration_alone_gives_a_working_id()
    {
        var id = GeneratedId.New();

        Assert.Matches("^gen_[0-9a-hjkmnp-tv-z]{26}$", id.ToString());
        Assert.Equal(id, GeneratedId.Parse(id.ToString()));
        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void It_carries_the_prefix_from_the_attribute()
    {
        Assert.Equal("gen", GeneratedId.Prefix);
        Assert.Equal("gen", TypedIdPrefix.Validate<GeneratedId>());
    }

    [Fact]
    public void The_attribute_opts_a_longer_prefix_in()
    {
        Assert.Equal("generated", GeneratedExtendedId.Prefix);
        Assert.True(GeneratedExtendedId.UsesExtendedPrefix);
        Assert.Equal("generated", TypedIdPrefix.Validate<GeneratedExtendedId>());
        Assert.StartsWith("generated_", GeneratedExtendedId.New().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_plain_struct_is_generated_as_a_plain_struct()
    {
        var id = GeneratedStructId.New();

        Assert.StartsWith("gns_", id.ToString(), StringComparison.Ordinal);
        Assert.Equal(id, GeneratedStructId.Parse(id.ToString()));
    }

    [Fact]
    public void An_internal_id_is_generated_internal()
    {
        var id = GeneratedInternalId.New();

        Assert.StartsWith("gin_", id.ToString(), StringComparison.Ordinal);
        Assert.Equal(id, GeneratedInternalId.FromGuid(id.Value));
    }

    [Fact]
    public void TryParse_is_as_strict_as_a_hand_written_one()
    {
        Assert.True(GeneratedId.TryParse(GeneratedId.New().ToString(), null, out _));
        Assert.False(GeneratedId.TryParse("gns_01h455vb4pex5vsknk084sn02q", null, out _));
        Assert.False(GeneratedId.TryParse("GEN_01h455vb4pex5vsknk084sn02q", null, out _));
        Assert.False(GeneratedId.TryParse(null, null, out _));
    }

    [Fact]
    public void It_answers_to_every_helper_a_hand_written_id_does()
    {
        var first = GeneratedId.New();
        Thread.Sleep(2);
        var second = GeneratedId.New();

        Assert.True(TypedId.Compare(first, second) < 0);
        Assert.InRange(TypedId.GetTimestamp(second), DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Equal(first.ToString(), TypedId.Format(first));
    }

    [Fact]
    public void It_serializes_through_the_same_converter()
    {
        var id = GeneratedId.New();

        Assert.Equal($"\"{id}\"", JsonSerializer.Serialize(id, Options));
        Assert.Equal(id, JsonSerializer.Deserialize<GeneratedId>($"\"{id}\"", Options));
    }

    [Fact]
    public void It_is_found_by_the_assembly_scan()
    {
        var found = TypedId.FindTypedIds(typeof(GeneratedId).Assembly).ToArray();

        Assert.Contains(typeof(GeneratedId), found);
        Assert.Contains(typeof(GeneratedStructId), found);
        Assert.Contains(typeof(GeneratedInternalId), found);
    }

    [Fact]
    public void It_has_value_equality_like_any_other_id()
    {
        var guid = Uuid7.New();

        Assert.Equal(GeneratedId.FromGuid(guid), GeneratedId.FromGuid(guid));
        Assert.NotEqual(GeneratedId.FromGuid(guid), GeneratedId.New());
        Assert.Equal(GeneratedId.FromGuid(guid).GetHashCode(), GeneratedId.FromGuid(guid).GetHashCode());
    }

    [Fact]
    public void The_value_can_be_reached_and_rebuilt()
    {
        var guid = Uuid7.New();
        var id = new GeneratedId(guid);

        Assert.Equal(guid, id.Value);
        Assert.Equal(id, GeneratedId.FromGuid(guid));
    }

    [Fact]
    public void Two_generated_ids_do_not_mix()
    {
        var guid = Uuid7.New();

        // They share a Guid and nothing else: no conversion exists between them, and the text differs.
        Assert.NotEqual(GeneratedId.FromGuid(guid).ToString(), GeneratedStructId.FromGuid(guid).ToString());
        Assert.Throws<FormatException>(() => GeneratedStructId.Parse(GeneratedId.FromGuid(guid).ToString()));
    }

    [Fact]
    public void The_prefixes_of_this_assembly_are_valid_and_unique()
    {
        // The sweep a service runs over its own ids, here covering the generated ones as well.
        TypedIdPrefix.ValidateAll(
        [
            typeof(GeneratedId),
            typeof(GeneratedExtendedId),
            typeof(GeneratedStructId),
            typeof(GeneratedInternalId),
        ]);
    }
}
