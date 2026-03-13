namespace Qyl.Agents.Generator.Generation;

internal static class EmitHelpers
{
    public static string Lit(string? value)
    {
        return value is null ? "null" : SymbolDisplay.FormatLiteral(value, true);
    }
}
