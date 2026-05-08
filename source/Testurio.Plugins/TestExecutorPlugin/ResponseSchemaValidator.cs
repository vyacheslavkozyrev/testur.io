using System.Text.Json;
using System.Text.RegularExpressions;

namespace Testurio.Plugins.TestExecutorPlugin;

/// <summary>
/// Validates an HTTP response against an expected status code and response body schema.
/// Both validations must pass for a step to be considered Passed (AC-011).
/// </summary>
public sealed partial class ResponseSchemaValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Validates the actual HTTP status code against the expected status code.
    /// Returns null on pass; returns a failure message on mismatch (AC-009).
    /// </summary>
    public string? ValidateStatusCode(int actualStatusCode, int? expectedStatusCode)
    {
        if (expectedStatusCode is null)
            return null;

        if (actualStatusCode != expectedStatusCode.Value)
            return $"Expected status {expectedStatusCode.Value} but got {actualStatusCode}";

        return null;
    }

    /// <summary>
    /// Validates the actual response body against the expected schema.
    /// The schema is a JSON object whose property values are type names (e.g. "string", "number", "boolean", "array", "object").
    /// Missing required properties or type mismatches produce failure messages (AC-010).
    /// Returns null when <paramref name="expectedSchema"/> is null/empty or when validation passes.
    /// </summary>
    public string? ValidateSchema(string? actualBody, string? expectedSchema)
    {
        if (string.IsNullOrWhiteSpace(expectedSchema))
            return null;

        if (string.IsNullOrWhiteSpace(actualBody))
            return "Response body is empty but a schema was expected";

        JsonElement schemaElement;
        try
        {
            schemaElement = JsonDocument.Parse(expectedSchema).RootElement;
        }
        catch (JsonException)
        {
            // Invalid expected schema — treat as no schema constraint rather than failing the step.
            return null;
        }

        JsonElement actualElement;
        try
        {
            actualElement = JsonDocument.Parse(actualBody).RootElement;
        }
        catch (JsonException)
        {
            return "Response body is not valid JSON";
        }

        if (schemaElement.ValueKind != JsonValueKind.Object)
            return null;

        var failures = new List<string>();
        foreach (var property in schemaElement.EnumerateObject())
        {
            var fieldName = property.Name;
            var expectedTypeName = property.Value.GetString() ?? string.Empty;

            if (!TryGetProperty(actualElement, fieldName, out var actualProperty))
            {
                failures.Add($"Missing required field '{fieldName}'");
                continue;
            }

            var typeFailure = ValidateFieldType(actualProperty, expectedTypeName, fieldName);
            if (typeFailure is not null)
                failures.Add(typeFailure);
        }

        return failures.Count > 0
            ? string.Join("; ", failures)
            : null;
    }

    /// <summary>
    /// Extracts the expected HTTP status code from an expectedResult string such as
    /// "HTTP 200 OK", "201 Created", or "HTTP 404".
    /// Returns null if no status code pattern is found.
    /// </summary>
    public int? ParseExpectedStatusCode(string? expectedResult)
    {
        if (string.IsNullOrWhiteSpace(expectedResult))
            return null;

        var match = StatusCodePattern().Match(expectedResult);
        if (!match.Success)
            return null;

        return int.TryParse(match.Groups["code"].Value, out var code) ? code : null;
    }

    /// <summary>
    /// Extracts the expected response schema from an expectedResult string.
    /// Looks for a JSON object literal in the string (e.g. `{"id":"string","status":"string"}`).
    /// Returns null if no JSON object is found.
    /// </summary>
    public string? ParseExpectedSchema(string? expectedResult)
    {
        if (string.IsNullOrWhiteSpace(expectedResult))
            return null;

        var match = JsonObjectPattern().Match(expectedResult);
        if (!match.Success)
            return null;

        return match.Value;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        // Case-insensitive property lookup.
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ValidateFieldType(JsonElement actual, string expectedTypeName, string fieldName)
    {
        var actualKind = actual.ValueKind;

        var mismatch = expectedTypeName.ToLowerInvariant() switch
        {
            "string" => actualKind != JsonValueKind.String
                ? $"Field '{fieldName}' expected string but got {DescribeKind(actualKind)}"
                : null,
            "number" => actualKind != JsonValueKind.Number
                ? $"Field '{fieldName}' expected number but got {DescribeKind(actualKind)}"
                : null,
            "boolean" => actualKind != JsonValueKind.True && actualKind != JsonValueKind.False
                ? $"Field '{fieldName}' expected boolean but got {DescribeKind(actualKind)}"
                : null,
            "array" => actualKind != JsonValueKind.Array
                ? $"Field '{fieldName}' expected array but got {DescribeKind(actualKind)}"
                : null,
            "object" => actualKind != JsonValueKind.Object
                ? $"Field '{fieldName}' expected object but got {DescribeKind(actualKind)}"
                : null,
            // Unknown type name — skip type check.
            _ => null
        };

        return mismatch;
    }

    private static string DescribeKind(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Array => "array",
        JsonValueKind.Object => "object",
        JsonValueKind.Null => "null",
        _ => kind.ToString().ToLowerInvariant()
    };

    // Matches 3-digit HTTP status codes, optionally preceded by "HTTP ".
    [GeneratedRegex(@"(?:HTTP\s+)?(?<code>[1-5]\d{2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex StatusCodePattern();

    // Matches a JSON object literal (opening brace to matching closing brace).
    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex JsonObjectPattern();
}
