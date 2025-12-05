using Cascade.CodeGen.Compilation;
using Cascade.CodeGen.Generation;
using Cascade.Grpc.CodeGen;
using Google.Protobuf;
using System.Linq;

namespace Cascade.Grpc.Server.Mappers;

internal static class CodeGenMappingExtensions
{
    public static GeneratedCodeResponse ToProto(this GeneratedCode code)
    {
        var response = new GeneratedCodeResponse
        {
            Result = ProtoResults.Success(),
            SourceCode = code.SourceCode,
            FileName = code.FileName,
            Namespace = code.Namespace
        };

        response.RequiredUsings.Add(code.RequiredUsings);
        response.RequiredReferences.Add(code.RequiredReferences);
        return response;
    }

    public static CompileResponse ToProto(this CompilationResult result)
    {
        var response = new CompileResponse
        {
            Result = ProtoResults.Success(),
            CompilationSuccess = result.Success,
            AssemblyBytes = result.AssemblyBytes is null ? ByteString.Empty : ByteString.CopyFrom(result.AssemblyBytes),
            CompilationTimeMs = (int)result.CompilationTime.TotalMilliseconds
        };

        response.Errors.Add(result.Errors.Select(e => e.ToProto()));
        response.Warnings.Add(result.Warnings.Select(e => e.ToProto()));
        return response;
    }

    public static CompileError ToProto(this CompilationError error)
    {
        return new CompileError
        {
            Code = error.Code,
            Message = error.Message,
            Line = error.Line,
            Column = error.Column,
            Severity = error.Severity.ToString()
        };
    }
}

