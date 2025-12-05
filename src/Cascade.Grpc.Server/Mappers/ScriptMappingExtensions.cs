using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Database.Filters;
using ProtoScript = Cascade.Grpc.CodeGen.Script;
using ProtoScriptResponse = Cascade.Grpc.CodeGen.ScriptResponse;

namespace Cascade.Grpc.Server.Mappers;

internal static class ScriptMappingExtensions
{
    public static ProtoScriptResponse ToProto(this Script script)
    {
        return new ProtoScriptResponse
        {
            Result = ProtoResults.Success(),
            Script = script.ToProtoScript()
        };
    }

    public static ProtoScript ToProtoScript(this Script script)
    {
        var proto = new ProtoScript
        {
            Id = script.Id.ToString(),
            Name = script.Name,
            Description = script.Description,
            SourceCode = script.SourceCode,
            Type = script.Type.ToString(),
            CurrentVersion = script.CurrentVersion,
            CreatedAt = script.CreatedAt.ToString("O"),
            UpdatedAt = script.UpdatedAt.ToString("O")
        };

        foreach (var kvp in script.Metadata)
        {
            proto.Metadata.Add(kvp.Key, kvp.Value);
        }

        return proto;
    }

    public static ScriptType ToDomain(this string type)
    {
        return Enum.TryParse<ScriptType>(type, true, out var result) ? result : ScriptType.Utility;
    }
}

