using Testurio.Plugins.TestExecutorPlugin;

namespace Testurio.UnitTests.Plugins;

public class ResponseSchemaValidatorTests
{
    private static ResponseSchemaValidator CreateSut() => new();

    // --- ValidateStatusCode ---

    [Fact]
    public void ValidateStatusCode_MatchingCode_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(sut.ValidateStatusCode(200, 200));
    }

    [Fact]
    public void ValidateStatusCode_MismatchedCode_ReturnsFailureMessage()
    {
        var sut = CreateSut();
        var result = sut.ValidateStatusCode(404, 200);
        Assert.NotNull(result);
        Assert.Contains("404", result);
        Assert.Contains("200", result);
    }

    [Fact]
    public void ValidateStatusCode_NullExpected_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(sut.ValidateStatusCode(500, null));
    }

    // --- ValidateSchema ---

    [Fact]
    public void ValidateSchema_NullSchema_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(sut.ValidateSchema("{\"id\":\"1\"}", null));
    }

    [Fact]
    public void ValidateSchema_EmptySchema_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(sut.ValidateSchema("{\"id\":\"1\"}", ""));
    }

    [Fact]
    public void ValidateSchema_AllFieldsPresentAndCorrectTypes_ReturnsNull()
    {
        var sut = CreateSut();
        var schema = """{"id":"string","count":"number","active":"boolean"}""";
        var body = """{"id":"abc","count":5,"active":true}""";
        Assert.Null(sut.ValidateSchema(body, schema));
    }

    [Fact]
    public void ValidateSchema_MissingRequiredField_ReturnsFailureMessage()
    {
        var sut = CreateSut();
        var schema = """{"id":"string","name":"string"}""";
        var body = """{"id":"abc"}""";
        var result = sut.ValidateSchema(body, schema);
        Assert.NotNull(result);
        Assert.Contains("name", result);
    }

    [Fact]
    public void ValidateSchema_WrongFieldType_ReturnsFailureMessage()
    {
        var sut = CreateSut();
        var schema = """{"count":"number"}""";
        var body = """{"count":"not-a-number"}""";
        var result = sut.ValidateSchema(body, schema);
        Assert.NotNull(result);
        Assert.Contains("count", result);
    }

    [Fact]
    public void ValidateSchema_EmptyBody_ReturnsFailureMessage()
    {
        var sut = CreateSut();
        var schema = """{"id":"string"}""";
        var result = sut.ValidateSchema("", schema);
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateSchema_InvalidBodyJson_ReturnsFailureMessage()
    {
        var sut = CreateSut();
        var schema = """{"id":"string"}""";
        var result = sut.ValidateSchema("not-json", schema);
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateSchema_InvalidSchemaJson_ReturnsNull()
    {
        // Invalid schema is treated as no constraint — step should not fail due to a bad schema definition.
        var sut = CreateSut();
        var result = sut.ValidateSchema("{\"id\":\"1\"}", "not-valid-json");
        Assert.Null(result);
    }

    [Fact]
    public void ValidateSchema_ArrayField_ValidatesCorrectly()
    {
        var sut = CreateSut();
        var schema = """{"items":"array"}""";
        var body = """{"items":[1,2,3]}""";
        Assert.Null(sut.ValidateSchema(body, schema));
    }

    [Fact]
    public void ValidateSchema_ObjectField_ValidatesCorrectly()
    {
        var sut = CreateSut();
        var schema = """{"data":"object"}""";
        var body = """{"data":{"key":"value"}}""";
        Assert.Null(sut.ValidateSchema(body, schema));
    }

    [Fact]
    public void ValidateSchema_BothPassAndFail_ReportsAllFailures()
    {
        var sut = CreateSut();
        var schema = """{"id":"string","count":"number","flag":"boolean"}""";
        var body = """{"id":"abc","count":"wrong","flag":123}""";
        var result = sut.ValidateSchema(body, schema);
        Assert.NotNull(result);
        Assert.Contains("count", result);
        Assert.Contains("flag", result);
    }

    // --- ParseExpectedStatusCode ---

    [Theory]
    [InlineData("HTTP 200 OK", 200)]
    [InlineData("HTTP 201 Created", 201)]
    [InlineData("HTTP 404", 404)]
    [InlineData("200 Created", 200)]
    [InlineData("Returns 204 No Content", 204)]
    public void ParseExpectedStatusCode_ValidPatterns_ExtractsCode(string input, int expected)
    {
        var sut = CreateSut();
        Assert.Equal(expected, sut.ParseExpectedStatusCode(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("The response should be successful")]
    public void ParseExpectedStatusCode_NoPattern_ReturnsNull(string? input)
    {
        var sut = CreateSut();
        Assert.Null(sut.ParseExpectedStatusCode(input));
    }

    // --- ParseExpectedSchema ---

    [Fact]
    public void ParseExpectedSchema_ValidJsonObject_ReturnsSchema()
    {
        var sut = CreateSut();
        var input = """HTTP 200 with {"id":"string","status":"string"}""";
        var result = sut.ParseExpectedSchema(input);
        Assert.NotNull(result);
        Assert.Contains("id", result);
        Assert.Contains("status", result);
    }

    [Fact]
    public void ParseExpectedSchema_NoJsonObject_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(sut.ParseExpectedSchema("HTTP 200 OK, no body"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseExpectedSchema_NullOrEmpty_ReturnsNull(string? input)
    {
        var sut = CreateSut();
        Assert.Null(sut.ParseExpectedSchema(input));
    }
}
