using System.Text.RegularExpressions;

namespace Cascade.CodeGen.Generation;

/// <summary>
/// Provides code optimization utilities.
/// </summary>
public static class CodeOptimizer
{
    /// <summary>
    /// Optimizes generated code by removing redundant whitespace and formatting.
    /// </summary>
    public static string Optimize(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            return sourceCode;

        // Remove multiple blank lines
        var optimized = Regex.Replace(sourceCode, @"(\r?\n\s*){3,}", "\r\n\r\n", RegexOptions.Multiline);

        // Trim trailing whitespace from lines
        optimized = Regex.Replace(optimized, @"[ \t]+(\r?\n)", "$1", RegexOptions.Multiline);

        return optimized;
    }
}

