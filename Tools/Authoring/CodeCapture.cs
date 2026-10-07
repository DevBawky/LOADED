using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using UnityPipeline.Microsoft.CodeAnalysis;
using UnityPipeline.Microsoft.CodeAnalysis.CSharp;
using UnityPipeline.Microsoft.CodeAnalysis.CSharp.Syntax;

public static class LoadedCodeCapture
{
    public static string Main()
    {
        string output = "outputs/authoring-20261002/code-texts.json";
        var entries = new List<Dictionary<string, string>>();
        int count = 0;
        foreach (var rawPath in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            string path = rawPath.Replace('\\', '/');
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));
            var root = tree.GetRoot(); var occurrences = new Dictionary<string, int>(); var visited = new HashSet<int>();
            foreach (var candidate in root.DescendantNodes().Where(n => n is LiteralExpressionSyntax || n is InterpolatedStringExpressionSyntax))
            {
                var node = candidate;
                if (node.Ancestors().Any(n => n is AttributeSyntax || n is InterpolatedStringExpressionSyntax)) continue;
                if (node is LiteralExpressionSyntax literal && !literal.IsKind(SyntaxKind.StringLiteralExpression)) continue;
                while (node.Parent is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression)) node = node.Parent;
                if (!visited.Add(node.SpanStart)) continue;
                var arguments = new List<string>(); string value = Format(node, arguments);
                var statement = node.Ancestors().FirstOrDefault(n => n is StatementSyntax || n is ArrowExpressionClauseSyntax)?.ToString() ?? "";
                if (Regex.IsMatch(statement, @"\b(Debug\.(Log|Assert)|throw\b|EditorPrefs|PlayerPrefs|Find\(|Load\()")) continue;
                if (!Regex.IsMatch(value, "[가-힣]") && !(Regex.IsMatch(statement, @"\.text\s*=|SetText\(|Append(Line)?\(") && Regex.IsMatch(value, "[A-Za-z]{2}"))) continue;
                var line = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
                string digest;
                using (var sha = System.Security.Cryptography.SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(path + "\n" + value))).Replace("-", "").ToLowerInvariant().Substring(0, 20);
                occurrences.TryGetValue(digest, out int ordinal); occurrences[digest] = ordinal + 1;
                statement = statement.Replace("\r", "").Replace("\n", " ");
                entries.Add(new Dictionary<string, string> { ["key"] = "code." + digest + "." + ordinal, ["scope"] = "Code", ["source"] = path + ":" + line, ["field"] = "literal", ["ko"] = value, ["en"] = "", ["status"] = "Draft", ["note"] = "Arguments: " + string.Join("; ", arguments.Select((a, i) => i + "=" + a)) + " | " + statement.Substring(0, Math.Min(statement.Length, 220)) });
                count++;
            }
        }
        var jsonType = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert"); var json = (string)jsonType.GetMethod("SerializeObject", new[] { typeof(object) }).Invoke(null, new object[] { entries }); File.WriteAllText(output, json, new UTF8Encoding(false));
        return "Captured " + count + " code text sites.";
    }
    static string Format(SyntaxNode node, List<string> arguments)
    {
        if (node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)) return literal.Token.ValueText;
        if (node is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression)) return Format(binary.Left, arguments) + Format(binary.Right, arguments);
        if (node is InterpolatedStringExpressionSyntax interpolation)
        {
            var b = new StringBuilder();
            foreach (var part in interpolation.Contents)
                if (part is InterpolatedStringTextSyntax t) b.Append(t.TextToken.ValueText);
                else if (part is InterpolationSyntax item) { b.Append("{").Append(arguments.Count).Append(item.AlignmentClause?.ToString()).Append(item.FormatClause?.ToString()).Append("}"); arguments.Add(item.Expression.ToString()); }
            return b.ToString();
        }
        string token = "{" + arguments.Count + "}"; arguments.Add(node.ToString()); return token;
    }
}
