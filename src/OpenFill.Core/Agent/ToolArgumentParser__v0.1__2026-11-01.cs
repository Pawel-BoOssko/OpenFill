using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

// A22: normalize duplicate JSON keys in tool arguments before creating
// JsonObject. Identical duplicates from model output are harmless;
// conflicting ones become a recoverable tool error, never a crashed task.
public static class ToolArgumentParser
{
    public static (JsonObject Args, string? Error) Parse(string? text)
    {
        try
        {
            using var document = JsonDocument.Parse(text ?? "{}");
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return (new JsonObject(), "arguments_root_not_object");
            var value = Normalize(document.RootElement, "$");
            return (value!.AsObject(), null);
        }
        catch (JsonException ex)
        {
            // Do not echo JSON argument values, only a short structural error.
            return (new JsonObject(), ex.Message.StartsWith("duplicate_conflicting_property:")
                ? ex.Message : "arguments_invalid_json:" + ex.GetType().Name);
        }
        catch (ArgumentException)
        {
            return (new JsonObject(), "arguments_invalid_structure");
        }
    }

    private static JsonNode? Normalize(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JsonObject();
                foreach (var entry in element.EnumerateObject())
                {
                    var child = Normalize(entry.Value, path + "." + entry.Name);
                    if (obj.ContainsKey(entry.Name))
                    {
                        if (!JsonNode.DeepEquals(obj[entry.Name], child))
                            throw new JsonException("duplicate_conflicting_property:" + path + "." + entry.Name);
                        continue;
                    }
                    obj[entry.Name] = child;
                }
                return obj;
            case JsonValueKind.Array:
                var array = new JsonArray();
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    array.Add(Normalize(item, path + "[" + index++ + "]"));
                return array;
            case JsonValueKind.String:
                return JsonValue.Create(element.GetString());
            case JsonValueKind.Number:
                return JsonNode.Parse(element.GetRawText());
            case JsonValueKind.True:
                return JsonValue.Create(true);
            case JsonValueKind.False:
                return JsonValue.Create(false);
            case JsonValueKind.Null:
                return null;
            default:
                throw new JsonException("unsupported_json_value");
        }
    }
}
