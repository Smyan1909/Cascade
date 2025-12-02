using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cascade.UIAutomation.Elements;

/// <summary>
/// A serializable snapshot of a UI element's state at a point in time.
/// </summary>
public class ElementSnapshot
{
    /// <summary>
    /// Gets or sets the runtime ID of the element.
    /// </summary>
    public string RuntimeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the automation ID of the element.
    /// </summary>
    public string? AutomationId { get; set; }

    /// <summary>
    /// Gets or sets the name of the element.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the class name of the element.
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Gets or sets the control type name of the element.
    /// </summary>
    public string ControlType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the control type ID of the element.
    /// </summary>
    public int ControlTypeId { get; set; }

    /// <summary>
    /// Gets or sets the bounding rectangle of the element.
    /// </summary>
    [JsonConverter(typeof(RectangleJsonConverter))]
    public Rectangle BoundingRectangle { get; set; }

    /// <summary>
    /// Gets or sets whether the element is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether the element is offscreen.
    /// </summary>
    public bool IsOffscreen { get; set; }

    /// <summary>
    /// Gets or sets whether the element is a content element.
    /// </summary>
    public bool IsContentElement { get; set; }

    /// <summary>
    /// Gets or sets whether the element is a control element.
    /// </summary>
    public bool IsControlElement { get; set; }

    /// <summary>
    /// Gets or sets whether the element has keyboard focus.
    /// </summary>
    public bool HasKeyboardFocus { get; set; }

    /// <summary>
    /// Gets or sets the process ID of the element's owning process.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the list of supported patterns.
    /// </summary>
    public List<string> SupportedPatterns { get; set; } = new();

    /// <summary>
    /// Gets or sets the child element snapshots.
    /// </summary>
    public List<ElementSnapshot> Children { get; set; } = new();

    /// <summary>
    /// Gets or sets the depth in the tree (0 = root).
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Gets or sets the current value if ValuePattern is supported.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets additional properties as key-value pairs.
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>
    /// Serializes this snapshot to JSON.
    /// </summary>
    public string ToJson(bool indented = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserializes a snapshot from JSON.
    /// </summary>
    public static ElementSnapshot? FromJson(string json)
    {
        return JsonSerializer.Deserialize<ElementSnapshot>(json);
    }

    /// <summary>
    /// Returns a string representation of this snapshot.
    /// </summary>
    public override string ToString()
    {
        var name = !string.IsNullOrEmpty(Name) ? Name : "(no name)";
        var automationId = !string.IsNullOrEmpty(AutomationId) ? $"[{AutomationId}]" : "";
        return $"{ControlType}: {name} {automationId}".Trim();
    }
}

/// <summary>
/// JSON converter for System.Drawing.Rectangle.
/// </summary>
internal class RectangleJsonConverter : JsonConverter<Rectangle>
{
    public override Rectangle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        int x = 0, y = 0, width = 0, height = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName?.ToLowerInvariant())
                {
                    case "x":
                        x = reader.GetInt32();
                        break;
                    case "y":
                        y = reader.GetInt32();
                        break;
                    case "width":
                        width = reader.GetInt32();
                        break;
                    case "height":
                        height = reader.GetInt32();
                        break;
                }
            }
        }

        return new Rectangle(x, y, width, height);
    }

    public override void Write(Utf8JsonWriter writer, Rectangle value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteNumber("width", value.Width);
        writer.WriteNumber("height", value.Height);
        writer.WriteEndObject();
    }
}

