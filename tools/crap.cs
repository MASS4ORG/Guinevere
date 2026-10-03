#!/usr/bin/env dotnet
// Reports CRAP scores for the methods changed since a git base, read from the Cobertura coverage `./build.sh test`
// writes. CRAP = complexity² × (1 − coverage)³ + complexity. Exits 1 when a method is over the limit.
//
// Usage: dotnet run tools/crap.cs -- [--base <ref>] [--max <n>] [--coverage <file>] [--all]
//   --base      git ref to diff against, working tree and untracked files included (default: main)
//   --max       highest acceptable CRAP score (default: 15)
//   --coverage  Cobertura file (default: coverage/coverage.xml)
//   --all       report every method in a changed file, not only methods whose lines changed

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var baseRef = Option("--base") ?? "main";
var limit = double.Parse(Option("--max") ?? "15", CultureInfo.InvariantCulture);
var coveragePath = Option("--coverage") ?? "coverage/coverage.xml";
var wholeFiles = args.Contains("--all");

var repository = Git("rev-parse", "--show-toplevel").Trim();
var changed = ChangedRanges(repository, baseRef);
if (!File.Exists(coveragePath))
{
    Console.Error.WriteLine($"No coverage at {coveragePath}; run ./build.sh test first.");
    return 2;
}

var rows = new List<(double Crap, int Complexity, double Coverage, string Location, string Method)>();
foreach (var type in XDocument.Load(coveragePath).Descendants("class"))
{
    var file = Path.GetFullPath((string?)type.Attribute("filename") ?? "");
    if (!changed.TryGetValue(file, out var ranges)) continue;

    foreach (var method in type.Descendants("method"))
    {
        var lines = method.Descendants("line").Select(line => (int)line.Attribute("number")!).ToList();
        if (lines.Count == 0) continue;
        var (first, last) = (lines.Min(), lines.Max());
        if (!wholeFiles && !ranges.Any(range => range.Start <= last && range.End >= first)) continue;

        var coverage = Number(method, "line-rate");
        var complexity = (int)Number(method, "complexity");
        var crap = (complexity * complexity * Math.Pow(1 - coverage, 3)) + complexity;
        var name = $"{ShortName((string)type.Attribute("name")!)}::{(string)method.Attribute("name")!}";
        rows.Add((crap, complexity, coverage, $"{Path.GetRelativePath(repository, file)}:{first}", name));
    }
}

var over = 0;
Console.WriteLine($"{"CRAP",7} {"cc",3} {"cov",5}  method (changed since {baseRef})");
foreach (var row in rows.OrderByDescending(row => row.Crap).ThenBy(row => row.Location, StringComparer.Ordinal))
{
    var flag = row.Crap > limit ? "  <-- over " + limit.ToString(CultureInfo.InvariantCulture) : "";
    if (row.Crap > limit) over++;
    Console.WriteLine($"{row.Crap,7:F1} {row.Complexity,3} {row.Coverage,5:P0}  {row.Method}  {row.Location}{flag}");
}

Console.WriteLine(rows.Count == 0
    ? "No covered methods changed."
    : $"{rows.Count} method(s), {over} over {limit.ToString(CultureInfo.InvariantCulture)}.");
return over == 0 ? 0 : 1;

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static double Number(XElement element, string attribute) =>
    double.Parse((string?)element.Attribute(attribute) ?? "0", CultureInfo.InvariantCulture);

// Drops the namespace and turns compiler-generated nesting (Outer/<>c__DisplayClass) into something readable.
static string ShortName(string fullName)
{
    var outer = fullName.Split('/')[0];
    var simple = outer[(outer.LastIndexOf('.') + 1)..];
    return fullName.Contains('/') ? simple + "/" + fullName[(fullName.IndexOf('/') + 1)..] : simple;
}

// Changed line ranges per absolute .cs path: diff hunks against the base, plus untracked files whole.
static Dictionary<string, List<(int Start, int End)>> ChangedRanges(string repository, string baseRef)
{
    var ranges = new Dictionary<string, List<(int Start, int End)>>(StringComparer.Ordinal);
    var hunk = new Regex(@"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@");
    List<(int, int)>? current = null;
    foreach (var line in Git("diff", "-U0", "--no-color", "--no-ext-diff", baseRef, "--", "*.cs").Split('\n'))
    {
        if (line.StartsWith("+++ ", StringComparison.Ordinal))
        {
            current = line == "+++ /dev/null" ? null : For(ranges, Path.Combine(repository, line[6..]));
            continue;
        }

        if (current is null || hunk.Match(line) is not { Success: true } match) continue;
        var start = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var count = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
        // A pure deletion still changes the method around it: mark the line it happened at.
        current.Add((start, start + Math.Max(count, 1) - 1));
    }

    foreach (var file in Git("ls-files", "--others", "--exclude-standard", "--", "*.cs").Split('\n'))
        if (file.Length > 0)
            For(ranges, Path.Combine(repository, file)).Add((1, int.MaxValue));

    return ranges;
}

static List<(int Start, int End)> For(Dictionary<string, List<(int Start, int End)>> ranges, string path)
{
    path = Path.GetFullPath(path);
    if (!ranges.TryGetValue(path, out var list)) ranges[path] = list = [];
    return list;
}

static string Git(params string[] arguments)
{
    var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    return output;
}
