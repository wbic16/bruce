// ===========================================================================================================
// Seed Spec - Layer 2 Phext Protocol
// Phext-native implementation: ONE file, coordinate-addressed scrolls, plain text serialization
//
// A Seed is a declarative artifact that initiates causality.
// It does not execute. It authorizes execution.
//
// Sprint 5 v2, Day 742 - January 9, 2026
// Copyright (c) 2026 Will Bickford
// ===========================================================================================================

using System.Security.Cryptography;
using System.Text;
using Phext;

// ═══════════════════════════════════════════════════════════════════════════════
// COORDINATE TOPOLOGY
// ═══════════════════════════════════════════════════════════════════════════════
//
// Library = project family (derived from seed_id hash, 1-9)
// Shelf   = seed major version
// Series  = phase:
//   1 = Spec (root seed)
//   2 = Generate
//   3 = Build  
//   4 = Test
//   5 = Package
//   6 = Explain
//   7 = Reseed
//
// Collection.Volume.Book = 1.1.1 (reserved for future)
// Chapter = iteration within phase
// Section = sub-scroll grouping
// Scroll  = individual scroll index
//
// Example: 3.1.1/1.1.1/1.1.1 = Project 3, Version 1, Spec phase, first scroll
// ═══════════════════════════════════════════════════════════════════════════════

namespace SeedSpec;

// ─────────────────────────────────────────────────────────────────────────────────
// SCROLL BASE - All scrolls serialize to plain text at a coordinate
// ─────────────────────────────────────────────────────────────────────────────────

public enum ScrollPhase { Spec = 1, Generate = 2, Build = 3, Test = 4, Package = 5, Explain = 6, Reseed = 7 }

public abstract record Scroll
{
    public required string ScrollId { get; init; }
    public required Coordinate Coord { get; init; }
    public required List<Coordinate> Parents { get; init; }
    public required DateTime EmittedAt { get; init; }
    public required string ContentHash { get; init; }
    public abstract ScrollPhase Phase { get; }

    // Serialize scroll to plain text for storage in phext
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"scroll_id: {ScrollId}");
        sb.AppendLine($"phase: {Phase}");
        sb.AppendLine($"coord: {Coord}");
        sb.AppendLine($"emitted_at: {EmittedAt:O}");
        sb.AppendLine($"parents: {(Parents.Count > 0 ? string.Join(", ", Parents) : "none")}");
        sb.AppendLine($"hash: {ContentHash}");
        sb.AppendLine("---");
        WriteContent(sb);
        return sb.ToString();
    }

    protected abstract void WriteContent(StringBuilder sb);

    // Parse scroll header from text
    public static ScrollHeader ParseHeader(string text)
    {
        var lines = text.Split('\n');
        var header = new ScrollHeader();
        
        foreach (var line in lines)
        {
            if (line == "---") break;
            var colonIdx = line.IndexOf(':');
            if (colonIdx <= 0) continue;
            
            var key = line[..colonIdx].Trim();
            var value = line[(colonIdx + 1)..].Trim();
            
            switch (key)
            {
                case "scroll_id": header.ScrollId = value; break;
                case "phase": Enum.TryParse<ScrollPhase>(value, out var p); header.Phase = p; break;
                case "coord": header.Coord = Coordinate.FromString(value); break;
                case "emitted_at": DateTime.TryParse(value, out var dt); header.EmittedAt = dt; break;
                case "parents": 
                    header.Parents = value == "none" ? new() : 
                        value.Split(',').Select(s => Coordinate.FromString(s.Trim())).ToList();
                    break;
                case "hash": header.ContentHash = value; break;
            }
        }
        return header;
    }

    // Get content portion (after ---)
    public static string GetContent(string text)
    {
        var idx = text.IndexOf("---\n");
        return idx >= 0 ? text[(idx + 4)..] : "";
    }

    public static string ComputeHash(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)))[..16].ToLower();
    }
}

public class ScrollHeader
{
    public string ScrollId { get; set; } = "";
    public ScrollPhase Phase { get; set; }
    public Coordinate Coord { get; set; }
    public DateTime EmittedAt { get; set; }
    public List<Coordinate> Parents { get; set; } = new();
    public string ContentHash { get; set; } = "";
}

// ─────────────────────────────────────────────────────────────────────────────────
// SPEC SCROLL - The root seed
// ─────────────────────────────────────────────────────────────────────────────────

public record SpecScroll : Scroll
{
    public override ScrollPhase Phase => ScrollPhase.Spec;
    
    // Seed fields stored as plain text
    public required string SeedId { get; init; }
    public required string Version { get; init; }
    public required string AuthorName { get; init; }
    public required string AuthorType { get; init; }
    public required string Purpose { get; init; }
    public required List<string> SuccessCriteria { get; init; }
    public required List<string> NonGoals { get; init; }
    public required List<GeneratorSpec> Generators { get; init; }

    protected override void WriteContent(StringBuilder sb)
    {
        sb.AppendLine($"seed_id: {SeedId}");
        sb.AppendLine($"version: {Version}");
        sb.AppendLine($"author: {AuthorName} ({AuthorType})");
        sb.AppendLine($"purpose: {Purpose}");
        sb.AppendLine();
        sb.AppendLine("success_criteria:");
        foreach (var c in SuccessCriteria) sb.AppendLine($"  - {c}");
        sb.AppendLine();
        if (NonGoals.Count > 0)
        {
            sb.AppendLine("non_goals:");
            foreach (var ng in NonGoals) sb.AppendLine($"  - {ng}");
            sb.AppendLine();
        }
        sb.AppendLine("generators:");
        foreach (var g in Generators)
        {
            sb.AppendLine($"  - phase: {g.Phase}");
            sb.AppendLine($"    tool: {g.ToolName} v{g.ToolVersion}");
            sb.AppendLine($"    outputs: {string.Join(", ", g.ExpectedOutputs)}");
            sb.AppendLine($"    on_failure: {g.OnFailure}");
        }
    }

    public static SpecScroll FromText(string text, Coordinate coord)
    {
        var header = ParseHeader(text);
        var content = GetContent(text);
        var lines = content.Split('\n');
        
        string seedId = "", version = "", authorName = "", authorType = "Human", purpose = "";
        var successCriteria = new List<string>();
        var nonGoals = new List<string>();
        var generators = new List<GeneratorSpec>();
        
        string? currentSection = null;
        GeneratorSpec? currentGen = null;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            if (trimmed == "success_criteria:") { currentSection = "success"; continue; }
            if (trimmed == "non_goals:") { currentSection = "nongoals"; continue; }
            if (trimmed == "generators:") { currentSection = "generators"; continue; }
            
            if (trimmed.StartsWith("- ") && currentSection == "success")
                successCriteria.Add(trimmed[2..]);
            else if (trimmed.StartsWith("- ") && currentSection == "nongoals")
                nonGoals.Add(trimmed[2..]);
            else if (trimmed.StartsWith("- phase:") && currentSection == "generators")
            {
                if (currentGen != null) generators.Add(currentGen);
                currentGen = new GeneratorSpec { Phase = trimmed[8..].Trim() };
            }
            else if (currentGen != null)
            {
                if (trimmed.StartsWith("tool:"))
                {
                    var parts = trimmed[5..].Trim().Split(" v");
                    currentGen.ToolName = parts[0];
                    currentGen.ToolVersion = parts.Length > 1 ? parts[1] : "1.0.0";
                }
                else if (trimmed.StartsWith("outputs:"))
                    currentGen.ExpectedOutputs = trimmed[8..].Split(',').Select(s => s.Trim()).ToList();
                else if (trimmed.StartsWith("on_failure:"))
                    currentGen.OnFailure = trimmed[11..].Trim();
            }
            else if (trimmed.StartsWith("seed_id:")) seedId = trimmed[8..].Trim();
            else if (trimmed.StartsWith("version:")) version = trimmed[8..].Trim();
            else if (trimmed.StartsWith("author:"))
            {
                var auth = trimmed[7..].Trim();
                var paren = auth.IndexOf('(');
                if (paren > 0)
                {
                    authorName = auth[..paren].Trim();
                    authorType = auth[(paren+1)..].TrimEnd(')');
                }
                else authorName = auth;
            }
            else if (trimmed.StartsWith("purpose:")) purpose = trimmed[8..].Trim();
        }
        if (currentGen != null) generators.Add(currentGen);

        return new SpecScroll
        {
            ScrollId = header.ScrollId,
            Coord = coord,
            Parents = header.Parents,
            EmittedAt = header.EmittedAt,
            ContentHash = header.ContentHash,
            SeedId = seedId,
            Version = version,
            AuthorName = authorName,
            AuthorType = authorType,
            Purpose = purpose,
            SuccessCriteria = successCriteria,
            NonGoals = nonGoals,
            Generators = generators
        };
    }
}

public class GeneratorSpec
{
    public string Phase { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string ToolVersion { get; set; } = "";
    public List<string> ExpectedOutputs { get; set; } = new();
    public string OnFailure { get; set; } = "Halt";
}

// ─────────────────────────────────────────────────────────────────────────────────
// GENERATE SCROLL
// ─────────────────────────────────────────────────────────────────────────────────

public record GenerateScroll : Scroll
{
    public override ScrollPhase Phase => ScrollPhase.Generate;
    public required string ToolName { get; init; }
    public required string ToolVersion { get; init; }
    public required Dictionary<string, string> InputHashes { get; init; }
    public required List<(string Name, string Type, string Hash)> Artifacts { get; init; }
    public required TimeSpan Duration { get; init; }

    protected override void WriteContent(StringBuilder sb)
    {
        sb.AppendLine($"tool: {ToolName} v{ToolVersion}");
        sb.AppendLine($"duration: {Duration.TotalMilliseconds:F0}ms");
        sb.AppendLine();
        sb.AppendLine("input_hashes:");
        foreach (var (k, v) in InputHashes) sb.AppendLine($"  {k}: {v}");
        sb.AppendLine();
        sb.AppendLine("artifacts:");
        foreach (var (name, type, hash) in Artifacts)
            sb.AppendLine($"  - {name} ({type}) [{hash}]");
    }
}

// ─────────────────────────────────────────────────────────────────────────────────
// BUILD SCROLL
// ─────────────────────────────────────────────────────────────────────────────────

public record BuildScroll : Scroll
{
    public override ScrollPhase Phase => ScrollPhase.Build;
    public required string Os { get; init; }
    public required string Arch { get; init; }
    public required Dictionary<string, string> Toolchains { get; init; }
    public required List<(string Path, string Hash, long Size)> Outputs { get; init; }
    public required List<string> Warnings { get; init; }
    public required int ExitCode { get; init; }
    public required TimeSpan Duration { get; init; }

    protected override void WriteContent(StringBuilder sb)
    {
        sb.AppendLine($"environment: {Os} / {Arch}");
        sb.AppendLine($"exit_code: {ExitCode}");
        sb.AppendLine($"duration: {Duration.TotalMilliseconds:F0}ms");
        sb.AppendLine();
        sb.AppendLine("toolchains:");
        foreach (var (k, v) in Toolchains) sb.AppendLine($"  {k}: {v}");
        sb.AppendLine();
        sb.AppendLine("outputs:");
        foreach (var (path, hash, size) in Outputs)
            sb.AppendLine($"  - {path} [{hash}] ({size} bytes)");
        if (Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("warnings:");
            foreach (var w in Warnings) sb.AppendLine($"  - {w}");
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────────
// TEST SCROLL
// ─────────────────────────────────────────────────────────────────────────────────

public record TestScroll : Scroll
{
    public override ScrollPhase Phase => ScrollPhase.Test;
    public required string Framework { get; init; }
    public required string FrameworkVersion { get; init; }
    public required List<(string Name, string Status, string? Message)> Results { get; init; }
    public required int Total { get; init; }
    public required int Passed { get; init; }
    public required int Failed { get; init; }
    public required int Skipped { get; init; }
    public required TimeSpan Duration { get; init; }

    protected override void WriteContent(StringBuilder sb)
    {
        sb.AppendLine($"framework: {Framework} v{FrameworkVersion}");
        sb.AppendLine($"duration: {Duration.TotalMilliseconds:F0}ms");
        sb.AppendLine($"summary: {Passed}/{Total} passed, {Failed} failed, {Skipped} skipped");
        sb.AppendLine();
        sb.AppendLine("results:");
        foreach (var (name, status, msg) in Results)
        {
            sb.Append($"  [{status}] {name}");
            if (!string.IsNullOrEmpty(msg)) sb.Append($" - {msg}");
            sb.AppendLine();
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────────
// EXPLAIN SCROLL - The trust bridge
// ─────────────────────────────────────────────────────────────────────────────────

public record ExplainScroll : Scroll
{
    public override ScrollPhase Phase => ScrollPhase.Explain;
    public required string Markdown { get; init; }

    protected override void WriteContent(StringBuilder sb)
    {
        sb.Append(Markdown);
    }
}

// ─────────────────────────────────────────────────────────────────────────────────
// RESEED SCROLL - Candidates requiring explicit acceptance
// ─────────────────────────────────────────────────────────────────────────────────

public record ReseedScroll : Scroll
{
    public override ScrollPhase Phase => ScrollPhase.Reseed;
    public required string Trigger { get; init; }
    public required List<(string SeedId, string Version, string Rationale, List<string> Changes)> Candidates { get; init; }

    protected override void WriteContent(StringBuilder sb)
    {
        sb.AppendLine($"trigger: {Trigger}");
        sb.AppendLine();
        sb.AppendLine("candidates:");
        foreach (var (seedId, version, rationale, changes) in Candidates)
        {
            sb.AppendLine($"  - seed_id: {seedId}");
            sb.AppendLine($"    version: {version}");
            sb.AppendLine($"    rationale: {rationale}");
            sb.AppendLine($"    changes:");
            foreach (var c in changes) sb.AppendLine($"      - {c}");
            sb.AppendLine($"    status: [REQUIRES EXPLICIT ACCEPTANCE]");
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────────
// LINEAGE - Manages the phext document containing all scrolls
// ─────────────────────────────────────────────────────────────────────────────────

public class Lineage
{
    private string _phext = "";
    private readonly Dictionary<string, Coordinate> _scrollIndex = new();
    private readonly Dictionary<(int lib, int shelf, ScrollPhase phase), int> _counters = new();

    public string Phext => _phext;

    // Allocate next coordinate for a scroll
    public Coordinate Allocate(int library, int shelf, ScrollPhase phase)
    {
        var key = (library, shelf, phase);
        if (!_counters.TryGetValue(key, out var scroll))
            scroll = 1;
        _counters[key] = scroll + 1;

        // lib.shelf.series / 1.1.1 / 1.1.scroll
        return new Coordinate(library, shelf, (int)phase, 1, 1, 1, 1, 1, scroll);
    }

    // Emit a scroll to the phext
    public Coordinate Emit(Scroll scroll)
    {
        var text = scroll.ToText();
        _phext = PhextEngine.Replace(_phext, scroll.Coord, text);
        _scrollIndex[scroll.ScrollId] = scroll.Coord;
        return scroll.Coord;
    }

    // Fetch scroll text at coordinate
    public string Fetch(Coordinate coord) => PhextEngine.Fetch(_phext, coord);

    // Get all scrolls as positioned scrolls
    public List<PositionedScroll> GetAllScrolls() => PhextEngine.Phokenize(_phext);

    // Get textmap for navigation
    public string Textmap() => PhextEngine.Textmap(_phext);

    // Get coordinate for scroll ID
    public Coordinate? GetCoord(string scrollId) => 
        _scrollIndex.TryGetValue(scrollId, out var c) ? c : null;

    // Load from file
    public void Load(string path)
    {
        if (File.Exists(path))
        {
            _phext = File.ReadAllText(path);
            RebuildIndex();
        }
    }

    // Save to file
    public void Save(string path)
    {
        File.WriteAllText(path, _phext);
    }

    private void RebuildIndex()
    {
        _scrollIndex.Clear();
        _counters.Clear();
        
        foreach (var ps in PhextEngine.Phokenize(_phext))
        {
            var header = Scroll.ParseHeader(ps.Scroll);
            if (!string.IsNullOrEmpty(header.ScrollId))
            {
                _scrollIndex[header.ScrollId] = ps.Coord;
                
                var key = (ps.Coord.Library, ps.Coord.Shelf, header.Phase);
                var scroll = ps.Coord.Scroll;
                if (!_counters.TryGetValue(key, out var max) || scroll >= max)
                    _counters[key] = scroll + 1;
            }
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────────
// SEED PROCESSOR - Orchestrates lifecycle, emits to phext
// ─────────────────────────────────────────────────────────────────────────────────

public class SeedProcessor
{
    private readonly Lineage _lineage = new();
    private readonly Action<string>? _log;

    public SeedProcessor(Action<string>? log = null)
    {
        _log = log;
    }

    public Lineage Lineage => _lineage;

    public List<Scroll> Process(string seedYaml)
    {
        var scrolls = new List<Scroll>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Parse seed from YAML
        var seed = ParseSeed(seedYaml);
        var library = Math.Abs(seed.SeedId.GetHashCode()) % 9 + 1;
        var shelf = int.Parse(seed.Version.Split('.')[0]);

        Log($"Processing seed: {seed.SeedId} v{seed.Version}");
        Log($"Coordinate space: Library {library}, Shelf {shelf}");
        Log("");

        // 1. SPEC PHASE
        Log("═══ SPEC ═══");
        var specCoord = _lineage.Allocate(library, shelf, ScrollPhase.Spec);
        var specScroll = new SpecScroll
        {
            ScrollId = $"{seed.SeedId}-spec-{seed.Version}",
            Coord = specCoord,
            Parents = new List<Coordinate>(),
            EmittedAt = DateTime.UtcNow,
            ContentHash = Scroll.ComputeHash(seed.Purpose),
            SeedId = seed.SeedId,
            Version = seed.Version,
            AuthorName = seed.AuthorName,
            AuthorType = seed.AuthorType,
            Purpose = seed.Purpose,
            SuccessCriteria = seed.SuccessCriteria,
            NonGoals = seed.NonGoals,
            Generators = seed.Generators
        };
        _lineage.Emit(specScroll);
        scrolls.Add(specScroll);
        Log($"  → {specCoord}");

        Coordinate parentCoord = specCoord;

        // Process each generator
        foreach (var gen in seed.Generators)
        {
            var phase = Enum.Parse<ScrollPhase>(gen.Phase);
            
            switch (phase)
            {
                case ScrollPhase.Generate:
                    Log("");
                    Log("═══ GENERATE ═══");
                    var genCoord = _lineage.Allocate(library, shelf, ScrollPhase.Generate);
                    var genScroll = new GenerateScroll
                    {
                        ScrollId = $"gen-{Guid.NewGuid().ToString("N")[..8]}",
                        Coord = genCoord,
                        Parents = new List<Coordinate> { parentCoord },
                        EmittedAt = DateTime.UtcNow,
                        ContentHash = Scroll.ComputeHash($"{gen.ToolName}:{sw.ElapsedMilliseconds}"),
                        ToolName = gen.ToolName,
                        ToolVersion = gen.ToolVersion,
                        InputHashes = new Dictionary<string, string> { ["seed.yaml"] = Scroll.ComputeHash(seedYaml) },
                        Artifacts = gen.ExpectedOutputs.Select(o => (o, o, Scroll.ComputeHash(o))).ToList(),
                        Duration = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds)
                    };
                    _lineage.Emit(genScroll);
                    scrolls.Add(genScroll);
                    parentCoord = genCoord;
                    Log($"  → {genCoord}");
                    break;

                case ScrollPhase.Build:
                    Log("");
                    Log("═══ BUILD ═══");
                    var buildCoord = _lineage.Allocate(library, shelf, ScrollPhase.Build);
                    var buildScroll = new BuildScroll
                    {
                        ScrollId = $"build-{Guid.NewGuid().ToString("N")[..8]}",
                        Coord = buildCoord,
                        Parents = new List<Coordinate> { parentCoord },
                        EmittedAt = DateTime.UtcNow,
                        ContentHash = Scroll.ComputeHash($"build:{sw.ElapsedMilliseconds}"),
                        Os = Environment.OSVersion.ToString(),
                        Arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                        Toolchains = new Dictionary<string, string> { ["dotnet"] = "8.0" },
                        Outputs = new List<(string, string, long)> { ("bin/output.dll", Scroll.ComputeHash("dll"), 4096) },
                        Warnings = new List<string>(),
                        ExitCode = 0,
                        Duration = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds)
                    };
                    _lineage.Emit(buildScroll);
                    scrolls.Add(buildScroll);
                    parentCoord = buildCoord;
                    Log($"  → {buildCoord}");
                    break;

                case ScrollPhase.Test:
                    Log("");
                    Log("═══ TEST ═══");
                    var testCoord = _lineage.Allocate(library, shelf, ScrollPhase.Test);
                    var testScroll = new TestScroll
                    {
                        ScrollId = $"test-{Guid.NewGuid().ToString("N")[..8]}",
                        Coord = testCoord,
                        Parents = new List<Coordinate> { parentCoord },
                        EmittedAt = DateTime.UtcNow,
                        ContentHash = Scroll.ComputeHash($"test:{sw.ElapsedMilliseconds}"),
                        Framework = gen.ToolName,
                        FrameworkVersion = gen.ToolVersion,
                        Results = new List<(string, string, string?)>
                        {
                            ("Seed_ShouldParse", "PASS", null),
                            ("Lineage_ShouldEmit", "PASS", null)
                        },
                        Total = 2,
                        Passed = 2,
                        Failed = 0,
                        Skipped = 0,
                        Duration = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds)
                    };
                    _lineage.Emit(testScroll);
                    scrolls.Add(testScroll);
                    parentCoord = testCoord;
                    Log($"  → {testCoord}");
                    break;
            }
        }

        // EXPLAIN PHASE
        Log("");
        Log("═══ EXPLAIN ═══");
        var explainCoord = _lineage.Allocate(library, shelf, ScrollPhase.Explain);
        var markdown = GenerateExplanation(seed, scrolls);
        var explainScroll = new ExplainScroll
        {
            ScrollId = $"explain-{Guid.NewGuid().ToString("N")[..8]}",
            Coord = explainCoord,
            Parents = scrolls.Select(s => s.Coord).ToList(),
            EmittedAt = DateTime.UtcNow,
            ContentHash = Scroll.ComputeHash(markdown),
            Markdown = markdown
        };
        _lineage.Emit(explainScroll);
        scrolls.Add(explainScroll);
        Log($"  → {explainCoord}");

        // RESEED PHASE
        Log("");
        Log("═══ RESEED ═══");
        var reseedCoord = _lineage.Allocate(library, shelf, ScrollPhase.Reseed);
        var newVersion = IncrementVersion(seed.Version);
        var reseedScroll = new ReseedScroll
        {
            ScrollId = $"reseed-{Guid.NewGuid().ToString("N")[..8]}",
            Coord = reseedCoord,
            Parents = new List<Coordinate> { parentCoord },
            EmittedAt = DateTime.UtcNow,
            ContentHash = Scroll.ComputeHash(newVersion),
            Trigger = "lifecycle-complete",
            Candidates = new List<(string, string, string, List<string>)>
            {
                (seed.SeedId, newVersion, $"Generated from {seed.SeedId} v{seed.Version}", 
                 new List<string> { $"version: {seed.Version} → {newVersion}" })
            }
        };
        _lineage.Emit(reseedScroll);
        scrolls.Add(reseedScroll);
        Log($"  → {reseedCoord}");

        Log("");
        Log($"Complete. {scrolls.Count} scrolls emitted in {sw.ElapsedMilliseconds}ms");

        return scrolls;
    }

    private string GenerateExplanation(ParsedSeed seed, List<Scroll> scrolls)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {seed.SeedId} v{seed.Version}");
        sb.AppendLine();
        sb.AppendLine("## Purpose");
        sb.AppendLine(seed.Purpose);
        sb.AppendLine();
        sb.AppendLine("## Why These Scrolls Exist");
        sb.AppendLine();
        foreach (var c in seed.SuccessCriteria)
            sb.AppendLine($"- {c}");
        sb.AppendLine();
        sb.AppendLine("## Lineage");
        sb.AppendLine();
        foreach (var s in scrolls.Where(s => s.Phase != ScrollPhase.Explain))
            sb.AppendLine($"- **{s.Phase}** at `{s.Coord}` [{s.ContentHash}]");
        sb.AppendLine();
        if (seed.NonGoals.Count > 0)
        {
            sb.AppendLine("## Non-Goals");
            foreach (var ng in seed.NonGoals)
                sb.AppendLine($"- {ng}");
            sb.AppendLine();
        }
        sb.AppendLine("---");
        sb.AppendLine("*Links backward only. This is the trust bridge.*");
        return sb.ToString();
    }

    private static string IncrementVersion(string version)
    {
        var parts = version.Split('.').Select(int.Parse).ToArray();
        parts[2]++;
        return string.Join(".", parts);
    }

    private void Log(string msg) => _log?.Invoke(msg);

    // Simple YAML parser - handles simplified seed format
    private static ParsedSeed ParseSeed(string yaml)
    {
        var lines = yaml.Split('\n');
        var seed = new ParsedSeed();
        string? section = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            var trimmed = line.Trim();

            if (trimmed == "success_criteria:") { section = "success"; continue; }
            if (trimmed == "non_goals:") { section = "nongoals"; continue; }
            if (trimmed == "generators:") { section = "generators"; continue; }

            if (section == "success" && trimmed.StartsWith("- "))
                seed.SuccessCriteria.Add(trimmed[2..]);
            else if (section == "nongoals" && trimmed.StartsWith("- "))
                seed.NonGoals.Add(trimmed[2..]);
            else if (section == "generators" && trimmed.StartsWith("- phase:"))
            {
                // Format: - phase: Generate | tool: name version | outputs: x,y | on_failure: Halt
                var gen = new GeneratorSpec();
                var parts = trimmed[2..].Split('|').Select(p => p.Trim()).ToArray();
                foreach (var part in parts)
                {
                    if (part.StartsWith("phase:")) gen.Phase = part[6..].Trim();
                    else if (part.StartsWith("tool:"))
                    {
                        var toolParts = part[5..].Trim().Split(' ', 2);
                        gen.ToolName = toolParts[0];
                        gen.ToolVersion = toolParts.Length > 1 ? toolParts[1] : "1.0.0";
                    }
                    else if (part.StartsWith("outputs:"))
                        gen.ExpectedOutputs = part[8..].Split(',').Select(o => o.Trim()).ToList();
                    else if (part.StartsWith("on_failure:"))
                        gen.OnFailure = part[11..].Trim();
                }
                seed.Generators.Add(gen);
            }
            else if (trimmed.StartsWith("seed_id:")) seed.SeedId = trimmed[8..].Trim();
            else if (trimmed.StartsWith("version:")) seed.Version = trimmed[8..].Trim();
            else if (trimmed.StartsWith("author_name:")) seed.AuthorName = trimmed[12..].Trim();
            else if (trimmed.StartsWith("author_type:")) seed.AuthorType = trimmed[12..].Trim();
            else if (trimmed.StartsWith("purpose:")) seed.Purpose = trimmed[8..].Trim();
        }

        return seed;
    }
}

public class ParsedSeed
{
    public string SeedId { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string AuthorName { get; set; } = "";
    public string AuthorType { get; set; } = "Human";
    public string Purpose { get; set; } = "";
    public List<string> SuccessCriteria { get; set; } = new();
    public List<string> NonGoals { get; set; } = new();
    public List<GeneratorSpec> Generators { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════════
// DEMO
// ═══════════════════════════════════════════════════════════════════════════════

public class Program
{
    public static void Main()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Seed Spec - Layer 2 Phext Protocol                      ║");
        Console.WriteLine("║   Sprint 5 v2 - Phext-Native Implementation               ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Seed uses simplified YAML - each generator is one line for clarity
        var seedYaml = """
            seed_id: hello-world
            version: 1.0.0
            author_name: Claude
            author_type: Agent
            purpose: Demonstrate phext-native seed lifecycle with coordinate-addressed scrolls.
            success_criteria:
              - All scrolls live in ONE phext file
              - Each scroll addressable by coordinate
              - Plain text serialization
              - Lineage emerges from coordinate topology
            non_goals:
              - Multiple output files
              - JSON blobs
            generators:
              - phase: Generate | tool: seed-processor 5.0.0 | outputs: source_code | on_failure: Halt
              - phase: Build | tool: dotnet 8.0.0 | outputs: assembly | on_failure: Annotate
              - phase: Test | tool: xunit 2.6.0 | outputs: test_results | on_failure: Continue
            """;

        var processor = new SeedProcessor(Console.WriteLine);
        var scrolls = processor.Process(seedYaml);

        Console.WriteLine();
        Console.WriteLine("═══ PHEXT TEXTMAP ═══");
        Console.WriteLine(processor.Lineage.Textmap());

        Console.WriteLine();
        Console.WriteLine("═══ SCROLL CONTENTS ═══");
        foreach (var ps in processor.Lineage.GetAllScrolls())
        {
            Console.WriteLine($"┌─ {ps.Coord} ─────────────────────────────────────");
            foreach (var line in ps.Scroll.Split('\n').Take(12))
                Console.WriteLine($"│ {line}");
            if (ps.Scroll.Split('\n').Length > 12)
                Console.WriteLine("│ ...");
            Console.WriteLine("└────────────────────────────────────────────────────────");
            Console.WriteLine();
        }

        // Save to single phext file
        var outputPath = "lineage.phext";
        processor.Lineage.Save(outputPath);
        Console.WriteLine($"═══ OUTPUT ═══");
        Console.WriteLine($"Single file: {outputPath} ({new FileInfo(outputPath).Length} bytes)");
        Console.WriteLine();
        Console.WriteLine("Core invariants:");
        Console.WriteLine("  • ONE phext file contains all lineage");
        Console.WriteLine("  • Each scroll addressable by coordinate");
        Console.WriteLine("  • Plain text serialization");
        Console.WriteLine("  • Coordinate topology encodes the DAG");
    }
}
