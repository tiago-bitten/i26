using System.Text.Json;
using i26.Core.Ids;
using i26.Core.Ids.Json;

namespace i26.Core.Tests.Ids;

public class TypedIdJsonTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new TypedIdJsonConverterFactory());
        return options;
    }

    private sealed record Order(TestOrderId Id, TestUserId OwnerId, TestUserId? ReviewerId);

    [Fact]
    public void Serializes_as_a_prefixed_string()
    {
        var id = TestUserId.New();

        Assert.Equal($"\"{id}\"", JsonSerializer.Serialize(id, Options));
    }

    [Fact]
    public void Deserializes_from_a_prefixed_string()
    {
        var id = TestUserId.New();

        var json = JsonSerializer.Serialize(id, Options);
        var parsed = JsonSerializer.Deserialize<TestUserId>(json, Options);

        Assert.Equal(id, parsed);
    }

    [Fact]
    public void Roundtrips_inside_an_object()
    {
        var order = new Order(TestOrderId.New(), TestUserId.New(), TestUserId.New());

        var json = JsonSerializer.Serialize(order, Options);
        var parsed = JsonSerializer.Deserialize<Order>(json, Options);

        Assert.Equal(order, parsed);
        Assert.Contains($"\"{order.Id}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"{order.OwnerId}\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Supports_a_nullable_id()
    {
        var order = new Order(TestOrderId.New(), TestUserId.New(), ReviewerId: null);

        var json = JsonSerializer.Serialize(order, Options);
        var parsed = JsonSerializer.Deserialize<Order>(json, Options);

        Assert.Null(parsed!.ReviewerId);
    }

    [Fact]
    public void Works_as_a_dictionary_key()
    {
        var first = TestUserId.New();
        var second = TestUserId.New();

        var source = new Dictionary<TestUserId, int> { [first] = 1, [second] = 2 };

        var json = JsonSerializer.Serialize(source, Options);
        var parsed = JsonSerializer.Deserialize<Dictionary<TestUserId, int>>(json, Options);

        Assert.Contains($"\"{first}\":", json, StringComparison.Ordinal);
        Assert.NotNull(parsed);
        Assert.Equal(2, parsed.Count);
        Assert.Equal(1, parsed[first]);
        Assert.Equal(2, parsed[second]);
    }

    [Theory]
    [InlineData("\"ord_01h455vb4pex5vsknk084sn02q\"")]
    [InlineData("\"usr_01H455VB4PEX5VSKNK084SN02Q\"")]
    [InlineData("\"usr_01h455vb4pex5vsknk084sn02i\"")]
    [InlineData("\"usr_\"")]
    [InlineData("\"\"")]
    [InlineData("\"01h455vb4pex5vsknk084sn02q\"")]
    public void Invalid_value_throws_JsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestUserId>(json, Options));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Non_string_token_throws_JsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestUserId>(json, Options));
    }

    [Fact]
    public void Invalid_dictionary_key_throws_JsonException()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Dictionary<TestUserId, int>>(
                """{"ord_01h455vb4pex5vsknk084sn02q":1}""",
                Options));
    }

    [Fact]
    public void Null_for_a_non_nullable_id_throws_JsonException()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestUserId>("null", Options));
    }

    [Fact]
    public void CanConvert_recognizes_every_typed_id()
    {
        var factory = new TypedIdJsonConverterFactory();

        Assert.True(factory.CanConvert(typeof(TestUserId)));
        Assert.True(factory.CanConvert(typeof(TestOrderId)));
        Assert.True(factory.CanConvert(typeof(TestExternalAuthId)));

        Assert.False(factory.CanConvert(typeof(Guid)));
        Assert.False(factory.CanConvert(typeof(string)));
        Assert.False(factory.CanConvert(typeof(int)));
        Assert.False(factory.CanConvert(typeof(Order)));
    }

    [Fact]
    public void A_single_registration_covers_different_id_types()
    {
        var user = TestUserId.New();
        var order = TestOrderId.New();
        var external = TestExternalAuthId.FromGuid(Uuid7.New());

        Assert.Equal($"\"{user}\"", JsonSerializer.Serialize(user, Options));
        Assert.Equal($"\"{order}\"", JsonSerializer.Serialize(order, Options));
        Assert.Equal($"\"{external}\"", JsonSerializer.Serialize(external, Options));
    }

    [Fact]
    public void Without_the_factory_the_id_serializes_as_an_object()
    {
        // Justifies the registration: by default the record struct becomes { "value": "<guid>" }.
        var json = JsonSerializer.Serialize(TestUserId.New());

        Assert.StartsWith("{", json, StringComparison.Ordinal);
    }
}
