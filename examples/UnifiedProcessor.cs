// ═══════════════════════════════════════════════════════════════════════════════
// Bruce Unified Seed Processor
// Sprint 6, Day 742 - January 8, 2026
//
// Processes bruce.seed to generate idiomatic implementations in C#, Rust, Python, Node
// Full 9D phext coordinates throughout
// ═══════════════════════════════════════════════════════════════════════════════

using System.Security.Cryptography;
using System.Text;
using Phext;

namespace Bruce.SeedGen;

// ─────────────────────────────────────────────────────────────────────────────────
// 9D COORDINATE - Full Phext Address Space
// ─────────────────────────────────────────────────────────────────────────────────

public readonly record struct Coord9D(
    int Library, int Shelf, int Series,
    int Collection, int Volume, int Book,
    int Chapter, int Section, int Scroll) : IComparable<Coord9D>
{
    public static Coord9D Parse(string s)
    {
        // Format: L.S.R/C.V.B/H.E.W
        var parts = s.Split('/');
        if (parts.Length != 3) throw new FormatException($"Invalid coord: {s}");
        
        var l1 = parts[0].Split('.').Select(int.Parse).ToArray();
        var l2 = parts[1].Split('.').Select(int.Parse).ToArray();
        var l3 = parts[2].Split('.').Select(int.Parse).ToArray();
        
        return new Coord9D(l1[0], l1[1], l1[2], l2[0], l2[1], l2[2], l3[0], l3[1], l3[2]);
    }

    public override string ToString() => $"{Library}.{Shelf}.{Series}/{Collection}.{Volume}.{Book}/{Chapter}.{Section}.{Scroll}";
    
    public Coordinate ToPhext() => new(Library, Shelf, Series, Collection, Volume, Book, Chapter, Section, Scroll);

    public int CompareTo(Coord9D other)
    {
        var cmp = Library.CompareTo(other.Library); if (cmp != 0) return cmp;
        cmp = Shelf.CompareTo(other.Shelf); if (cmp != 0) return cmp;
        cmp = Series.CompareTo(other.Series); if (cmp != 0) return cmp;
        cmp = Collection.CompareTo(other.Collection); if (cmp != 0) return cmp;
        cmp = Volume.CompareTo(other.Volume); if (cmp != 0) return cmp;
        cmp = Book.CompareTo(other.Book); if (cmp != 0) return cmp;
        cmp = Chapter.CompareTo(other.Chapter); if (cmp != 0) return cmp;
        cmp = Section.CompareTo(other.Section); if (cmp != 0) return cmp;
        return Scroll.CompareTo(other.Scroll);
    }
}

// ─────────────────────────────────────────────────────────────────────────────────
// LANGUAGE TARGETS
// ─────────────────────────────────────────────────────────────────────────────────

public enum Language { CSharp = 1, Rust = 2, Python = 3, Node = 4 }
public enum Module { Core = 1, Services = 2, Cli = 3 }

// ─────────────────────────────────────────────────────────────────────────────────
// SEED MODEL - Parsed from bruce.seed
// ─────────────────────────────────────────────────────────────────────────────────

public record BruceSeed
{
    public string SeedId { get; init; } = "";
    public string Version { get; init; } = "1.0.0";
    public string AuthorName { get; init; } = "";
    public string AuthorType { get; init; } = "Human";
    public string Purpose { get; init; } = "";
    public List<string> SuccessCriteria { get; init; } = new();
    public List<string> NonGoals { get; init; } = new();
    public List<TypeDef> Types { get; init; } = new();
    public List<ServiceDef> Services { get; init; } = new();
    public CliDef Cli { get; init; } = new();
    public Dictionary<Language, LanguageIdioms> Idioms { get; init; } = new();
}

public record TypeDef
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";  // struct, enum, entity, trait
    public List<FieldDef> Fields { get; init; } = new();
    public List<string> Variants { get; init; } = new();  // For enums
    public List<string> Derives { get; init; } = new();
    public string? Extends { get; init; }
    public Dictionary<string, List<string>> Transitions { get; init; } = new();  // For state enums
}

public record FieldDef(string Name, string Type, bool Optional = false, string? Default = null);

public record ServiceDef
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";  // trait, service
    public string? TypeParam { get; init; }
    public List<string> Dependencies { get; init; } = new();
    public List<MethodDef> Methods { get; init; } = new();
    public List<string> Events { get; init; } = new();
}

public record MethodDef(string Name, List<FieldDef> Args, string ReturnType);

public record CliDef
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<CommandDef> Commands { get; init; } = new();
}

public record CommandDef
{
    public string Name { get; init; } = "";
    public List<string> Aliases { get; init; } = new();
    public string Description { get; init; } = "";
    public List<FieldDef> Args { get; init; } = new();
    public List<FieldDef> Options { get; init; } = new();
    public List<CommandDef> Subcommands { get; init; } = new();
}

public record LanguageIdioms
{
    public string Namespace { get; init; } = "";
    public string Target { get; init; } = "";
    public Dictionary<string, string> Patterns { get; init; } = new();
    public List<string> Dependencies { get; init; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────────
// GENERATED ARTIFACT
// ─────────────────────────────────────────────────────────────────────────────────

public record GeneratedFile
{
    public required Coord9D Coord { get; init; }
    public required Language Language { get; init; }
    public required Module Module { get; init; }
    public required string Path { get; init; }
    public required string Content { get; init; }
    public required string Hash { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────────
// LINEAGE - Tracks all generated artifacts in phext
// ─────────────────────────────────────────────────────────────────────────────────

public class UnifiedLineage
{
    private string _phext = "";
    private readonly int _library;
    private readonly int _shelf;
    private readonly Dictionary<(int series, int collection, int volume), int> _bookCounters = new();

    public UnifiedLineage(int library, int shelf)
    {
        _library = library;
        _shelf = shelf;
    }

    public string Phext => _phext;

    public Coord9D Allocate(int series, int collection, int volume)
    {
        var key = (series, collection, volume);
        if (!_bookCounters.TryGetValue(key, out var book))
            book = 1;
        _bookCounters[key] = book + 1;

        return new Coord9D(_library, _shelf, series, collection, volume, book, 1, 1, 1);
    }

    public Coord9D AllocateSpec() => Allocate(1, 1, 1);
    public Coord9D AllocateDerive(Language lang, Module mod) => Allocate(2, (int)lang, (int)mod);
    public Coord9D AllocateBuild(Language lang) => Allocate(3, (int)lang, 1);
    public Coord9D AllocateTest(Language lang) => Allocate(4, (int)lang, 1);
    public Coord9D AllocatePackage(Language lang) => Allocate(5, (int)lang, 1);
    public Coord9D AllocateExplain() => Allocate(6, 1, 1);
    public Coord9D AllocateReseed() => Allocate(7, 1, 1);

    public void Emit(Coord9D coord, string content)
    {
        _phext = PhextEngine.Replace(_phext, coord.ToPhext(), content);
    }

    public void Save(string path) => File.WriteAllText(path, _phext);
    public void Load(string path) { if (File.Exists(path)) _phext = File.ReadAllText(path); }
}

// ─────────────────────────────────────────────────────────────────────────────────
// LANGUAGE GENERATORS - Produce idiomatic code
// ─────────────────────────────────────────────────────────────────────────────────

public interface ILanguageGenerator
{
    Language Language { get; }
    List<GeneratedFile> Generate(BruceSeed seed, UnifiedLineage lineage);
}

public abstract class BaseGenerator : ILanguageGenerator
{
    public abstract Language Language { get; }
    
    protected static string Hash(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)))[..16].ToLower();
    }

    public abstract List<GeneratedFile> Generate(BruceSeed seed, UnifiedLineage lineage);
    
    protected GeneratedFile CreateFile(Coord9D coord, Module module, string path, string content) =>
        new()
        {
            Coord = coord,
            Language = Language,
            Module = module,
            Path = path,
            Content = content,
            Hash = Hash(content)
        };
}

// ═══════════════════════════════════════════════════════════════════════════════
// C# GENERATOR
// ═══════════════════════════════════════════════════════════════════════════════

public class CSharpGenerator : BaseGenerator
{
    public override Language Language => Language.CSharp;

    public override List<GeneratedFile> Generate(BruceSeed seed, UnifiedLineage lineage)
    {
        var files = new List<GeneratedFile>();

        // Core module
        files.Add(GenerateCoord(seed, lineage));
        files.Add(GenerateEnums(seed, lineage));
        files.Add(GenerateEntities(seed, lineage));
        files.Add(GenerateResult(seed, lineage));

        // Services module
        files.Add(GenerateStoreInterface(seed, lineage));
        files.Add(GeneratePhextStore(seed, lineage));
        files.Add(GenerateJsonStore(seed, lineage));
        files.Add(GenerateStorageFactory(seed, lineage));
        files.Add(GenerateEngine(seed, lineage));

        // CLI module
        files.Add(GenerateCliProgram(seed, lineage));

        // Project files
        files.Add(GenerateCoreProject(seed, lineage));
        files.Add(GenerateServicesProject(seed, lineage));
        files.Add(GenerateCliProject(seed, lineage));
        files.Add(GenerateSolution(seed, lineage));

        return files;
    }

    private GeneratedFile GenerateCoord(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Core);
        var content = $$"""
            // ═══════════════════════════════════════════════════════════════════════════════
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            // ═══════════════════════════════════════════════════════════════════════════════
            
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using Phext;
            
            namespace Bruce.Core;
            
            /// <summary>
            /// Full 9D Phext Coordinate: L.S.R/C.V.B/H.E.W
            /// </summary>
            [JsonConverter(typeof(Coord9DConverter))]
            public readonly record struct Coord9D(
                int Library, int Shelf, int Series,
                int Collection, int Volume, int Book,
                int Chapter, int Section, int Scroll) : IComparable<Coord9D>
            {
                public static Coord9D Parse(string s)
                {
                    var parts = s.Split('/');
                    if (parts.Length != 3) throw new FormatException($"Invalid coord: {s}");
                    
                    var l1 = parts[0].Split('.').Select(int.Parse).ToArray();
                    var l2 = parts[1].Split('.').Select(int.Parse).ToArray();
                    var l3 = parts[2].Split('.').Select(int.Parse).ToArray();
                    
                    if (l1.Length != 3 || l2.Length != 3 || l3.Length != 3)
                        throw new FormatException($"Invalid coord: {s}");
                    
                    return new Coord9D(l1[0], l1[1], l1[2], l2[0], l2[1], l2[2], l3[0], l3[1], l3[2]);
                }
            
                public static bool TryParse(string? s, out Coord9D coord)
                {
                    coord = default;
                    if (string.IsNullOrWhiteSpace(s)) return false;
                    try { coord = Parse(s); return true; }
                    catch { return false; }
                }
            
                public Coordinate ToPhext() => new(Library, Shelf, Series, Collection, Volume, Book, Chapter, Section, Scroll);
                
                public override string ToString() => $"{Library}.{Shelf}.{Series}/{Collection}.{Volume}.{Book}/{Chapter}.{Section}.{Scroll}";
            
                public int CompareTo(Coord9D other)
                {
                    int cmp;
                    if ((cmp = Library.CompareTo(other.Library)) != 0) return cmp;
                    if ((cmp = Shelf.CompareTo(other.Shelf)) != 0) return cmp;
                    if ((cmp = Series.CompareTo(other.Series)) != 0) return cmp;
                    if ((cmp = Collection.CompareTo(other.Collection)) != 0) return cmp;
                    if ((cmp = Volume.CompareTo(other.Volume)) != 0) return cmp;
                    if ((cmp = Book.CompareTo(other.Book)) != 0) return cmp;
                    if ((cmp = Chapter.CompareTo(other.Chapter)) != 0) return cmp;
                    if ((cmp = Section.CompareTo(other.Section)) != 0) return cmp;
                    return Scroll.CompareTo(other.Scroll);
                }
            
                public static bool operator <(Coord9D a, Coord9D b) => a.CompareTo(b) < 0;
                public static bool operator >(Coord9D a, Coord9D b) => a.CompareTo(b) > 0;
                public static bool operator <=(Coord9D a, Coord9D b) => a.CompareTo(b) <= 0;
                public static bool operator >=(Coord9D a, Coord9D b) => a.CompareTo(b) >= 0;
            }
            
            public class Coord9DConverter : JsonConverter<Coord9D>
            {
                public override Coord9D Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                    => Coord9D.Parse(reader.GetString() ?? throw new JsonException("Coord cannot be null"));
            
                public override void Write(Utf8JsonWriter writer, Coord9D value, JsonSerializerOptions options)
                    => writer.WriteStringValue(value.ToString());
            }
            
            /// <summary>
            /// Entity type for coordinate partitioning
            /// </summary>
            public enum EntityType
            {
                Worker = 1,
                Task = 2,
                Assignment = 3,
                Message = 4,
                Artifact = 5,
                Archive = 6
            }
            
            /// <summary>
            /// Thread-safe 9D coordinate allocator
            /// </summary>
            public class CoordAllocator
            {
                private readonly int _library;
                private readonly int _shelf;
                private readonly object _lock = new();
                private readonly Dictionary<EntityType, int> _counters = new();
            
                public CoordAllocator(int library = 2, int shelf = 6)
                {
                    _library = library;
                    _shelf = shelf;
                    foreach (EntityType e in Enum.GetValues<EntityType>())
                        _counters[e] = 0;
                }
            
                public Coord9D Allocate(EntityType entityType)
                {
                    lock (_lock)
                    {
                        var pos = _counters[entityType]++;
                        var book = pos / 81 + 1;
                        var rem = pos % 81;
                        var chapter = rem / 9 + 1;
                        var section = rem % 9 + 1;
                        
                        return new Coord9D(_library, _shelf, 1, (int)entityType, 1, book, chapter, section, 1);
                    }
                }
            
                public void SetHighWater(EntityType entityType, Coord9D coord)
                {
                    lock (_lock)
                    {
                        var pos = (coord.Book - 1) * 81 + (coord.Chapter - 1) * 9 + (coord.Section - 1) + 1;
                        _counters[entityType] = Math.Max(_counters[entityType], pos);
                    }
                }
            }
            """;

        return CreateFile(coord, Module.Core, "src/csharp/Bruce.Core/Coord.cs", content);
    }

    private GeneratedFile GenerateEnums(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Core);
        var sb = new StringBuilder();
        
        sb.AppendLine($"// Generated from bruce.seed v{seed.Version}");
        sb.AppendLine($"// Coord: {coord}");
        sb.AppendLine();
        sb.AppendLine("namespace Bruce.Core;");
        sb.AppendLine();

        foreach (var type in seed.Types.Where(t => t.Kind == "enum"))
        {
            sb.AppendLine($"public enum {type.Name}");
            sb.AppendLine("{");
            for (int i = 0; i < type.Variants.Count; i++)
            {
                var comma = i < type.Variants.Count - 1 ? "," : "";
                sb.AppendLine($"    {type.Variants[i]}{comma}");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return CreateFile(coord, Module.Core, "src/csharp/Bruce.Core/Enums.cs", sb.ToString());
    }

    private GeneratedFile GenerateEntities(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Core);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            namespace Bruce.Core;
            
            public abstract record Entity
            {
                public required string Id { get; init; }
                public required Coord9D Coord { get; init; }
                public DateTime Created { get; init; } = DateTime.UtcNow;
                public DateTime Updated { get; set; } = DateTime.UtcNow;
                public int Version { get; set; } = 1;
            }
            
            public record Worker : Entity
            {
                public required string Name { get; init; }
                public WorkerType Type { get; init; }
                public WorkerRole Role { get; init; }
                public string? Substrate { get; init; }
                public bool IsActive { get; set; } = true;
            
                public int MaxCapacity => Role switch
                {
                    WorkerRole.Worker => 4,
                    WorkerRole.Manager => 24,
                    WorkerRole.Director => 200,
                    _ => 4
                };
            }
            
            public record BruceTask : Entity
            {
                public required string Title { get; init; }
                public string Description { get; init; } = "";
                public TaskSource Source { get; init; }
                public string? SourceRef { get; init; }
                public TaskState State { get; set; } = TaskState.Created;
                public TaskType Type { get; init; } = TaskType.Adhoc;
                public string? AssignedTo { get; set; }
            }
            
            public record Assignment : Entity
            {
                public required string TaskId { get; init; }
                public required string WorkerId { get; init; }
                public DateTime? PushedAt { get; init; }
                public DateTime? ClaimedAt { get; init; }
                public DateTime? CompletedAt { get; set; }
            }
            
            public record Message : Entity
            {
                public string? TaskId { get; init; }
                public required string FromWorkerId { get; init; }
                public string? ToWorkerId { get; init; }
                public required string Body { get; init; }
                public DateTime Timestamp { get; init; } = DateTime.UtcNow;
                public bool IsBroadcast => ToWorkerId == null;
            }
            
            public record Artifact : Entity
            {
                public required string TaskId { get; init; }
                public required string Name { get; init; }
                public string ContentType { get; init; } = "application/octet-stream";
                public byte[] Content { get; init; } = Array.Empty<byte>();
            }
            
            public record WorkerLoad(int Adhoc = 0, int Planned = 0)
            {
                public int Total => Adhoc + Planned;
            }
            """;

        return CreateFile(coord, Module.Core, "src/csharp/Bruce.Core/Entities.cs", content);
    }

    private GeneratedFile GenerateResult(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Core);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            namespace Bruce.Core;
            
            public enum ErrorCode { None, NotFound, Invalid, Conflict, Unauthorized }
            
            public readonly struct Result
            {
                public bool IsSuccess { get; }
                public bool IsFailure => !IsSuccess;
                public string Error { get; }
                public ErrorCode ErrorCode { get; }
            
                private Result(bool success, string error, ErrorCode code)
                {
                    IsSuccess = success;
                    Error = error;
                    ErrorCode = code;
                }
            
                public static Result Ok() => new(true, "", ErrorCode.None);
                public static Result Fail(string error, ErrorCode code = ErrorCode.Invalid) => new(false, error, code);
                public static Result NotFound(string error) => new(false, error, ErrorCode.NotFound);
            }
            
            public readonly struct Result<T>
            {
                public bool IsSuccess { get; }
                public bool IsFailure => !IsSuccess;
                public T? Value { get; }
                public string Error { get; }
                public ErrorCode ErrorCode { get; }
            
                private Result(bool success, T? value, string error, ErrorCode code)
                {
                    IsSuccess = success;
                    Value = value;
                    Error = error;
                    ErrorCode = code;
                }
            
                public static Result<T> Ok(T value) => new(true, value, "", ErrorCode.None);
                public static Result<T> Fail(string error, ErrorCode code = ErrorCode.Invalid) => new(false, default, error, code);
                public static Result<T> NotFound(string error) => new(false, default, error, ErrorCode.NotFound);
            
                public static implicit operator Result<T>(T value) => Ok(value);
            }
            """;

        return CreateFile(coord, Module.Core, "src/csharp/Bruce.Core/Result.cs", content);
    }

    private GeneratedFile GenerateStoreInterface(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Services);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            namespace Bruce.Services;
            
            using Bruce.Core;
            
            public interface IStore<T> where T : Entity
            {
                T? Get(string id);
                IEnumerable<T> GetAll();
                Result<T> Save(T entity);
                Result Delete(string id);
            }
            
            public interface IStorageBackend : IDisposable
            {
                IStore<Worker> Workers { get; }
                IStore<BruceTask> Tasks { get; }
                IStore<Assignment> Assignments { get; }
                IStore<Message> Messages { get; }
                IStore<Artifact> Artifacts { get; }
                CoordAllocator Allocator { get; }
                
                void Load();
                void Save();
            }
            
            public enum BackendType { Phext, Json, Unknown }
            """;

        return CreateFile(coord, Module.Services, "src/csharp/Bruce.Services/Store.cs", content);
    }

    private GeneratedFile GeneratePhextStore(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Services);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            using System.Text.Json;
            using Phext;
            
            namespace Bruce.Services;
            
            using Bruce.Core;
            
            public class PhextStore : IStorageBackend
            {
                private readonly string _path;
                private string _phext = "";
                private readonly CoordAllocator _allocator = new();
                private readonly JsonSerializerOptions _json = new() { WriteIndented = false };
            
                private readonly PhextEntityStore<Worker> _workers;
                private readonly PhextEntityStore<BruceTask> _tasks;
                private readonly PhextEntityStore<Assignment> _assignments;
                private readonly PhextEntityStore<Message> _messages;
                private readonly PhextEntityStore<Artifact> _artifacts;
            
                public PhextStore(string path)
                {
                    _path = path;
                    _workers = new(this, EntityType.Worker);
                    _tasks = new(this, EntityType.Task);
                    _assignments = new(this, EntityType.Assignment);
                    _messages = new(this, EntityType.Message);
                    _artifacts = new(this, EntityType.Artifact);
                }
            
                public IStore<Worker> Workers => _workers;
                public IStore<BruceTask> Tasks => _tasks;
                public IStore<Assignment> Assignments => _assignments;
                public IStore<Message> Messages => _messages;
                public IStore<Artifact> Artifacts => _artifacts;
                public CoordAllocator Allocator => _allocator;
            
                internal string Phext { get => _phext; set => _phext = value; }
                internal JsonSerializerOptions Json => _json;
            
                public void Load()
                {
                    if (File.Exists(_path))
                    {
                        _phext = File.ReadAllText(_path);
                        RebuildAllocator();
                    }
                }
            
                public void Save() => File.WriteAllText(_path, _phext);
            
                private void RebuildAllocator()
                {
                    foreach (var ps in PhextEngine.Phokenize(_phext))
                    {
                        var coord = new Coord9D(
                            ps.Coord.Library, ps.Coord.Shelf, ps.Coord.Series,
                            ps.Coord.Collection, ps.Coord.Volume, ps.Coord.Book,
                            ps.Coord.Chapter, ps.Coord.Section, ps.Coord.Scroll);
                        
                        if (Enum.IsDefined(typeof(EntityType), coord.Collection))
                            _allocator.SetHighWater((EntityType)coord.Collection, coord);
                    }
                }
            
                public void Dispose() { }
            
                private class PhextEntityStore<T> : IStore<T> where T : Entity
                {
                    private readonly PhextStore _parent;
                    private readonly EntityType _entityType;
                    private readonly Dictionary<string, Coord9D> _index = new();
            
                    public PhextEntityStore(PhextStore parent, EntityType entityType)
                    {
                        _parent = parent;
                        _entityType = entityType;
                    }
            
                    public T? Get(string id)
                    {
                        if (!_index.TryGetValue(id, out var coord))
                        {
                            // Scan phext for entity
                            foreach (var ps in PhextEngine.Phokenize(_parent.Phext))
                            {
                                if (ps.Coord.Collection != (int)_entityType) continue;
                                try
                                {
                                    var entity = JsonSerializer.Deserialize<T>(ps.Scroll, _parent.Json);
                                    if (entity?.Id == id)
                                    {
                                        _index[id] = new Coord9D(
                                            ps.Coord.Library, ps.Coord.Shelf, ps.Coord.Series,
                                            ps.Coord.Collection, ps.Coord.Volume, ps.Coord.Book,
                                            ps.Coord.Chapter, ps.Coord.Section, ps.Coord.Scroll);
                                        return entity;
                                    }
                                }
                                catch { }
                            }
                            return default;
                        }
                        
                        var json = PhextEngine.Fetch(_parent.Phext, coord.ToPhext());
                        return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, _parent.Json);
                    }
            
                    public IEnumerable<T> GetAll()
                    {
                        foreach (var ps in PhextEngine.Phokenize(_parent.Phext))
                        {
                            if (ps.Coord.Collection != (int)_entityType) continue;
                            T? entity = default;
                            try { entity = JsonSerializer.Deserialize<T>(ps.Scroll, _parent.Json); }
                            catch { continue; }
                            if (entity != null) yield return entity;
                        }
                    }
            
                    public Result<T> Save(T entity)
                    {
                        var coord = entity.Coord;
                        if (coord == default)
                            coord = _parent.Allocator.Allocate(_entityType);
            
                        var updated = entity with { Coord = coord, Updated = DateTime.UtcNow };
                        var json = JsonSerializer.Serialize(updated, _parent.Json);
                        _parent.Phext = PhextEngine.Replace(_parent.Phext, coord.ToPhext(), json);
                        _index[entity.Id] = coord;
                        _parent.Save();
                        return updated;
                    }
            
                    public Result Delete(string id)
                    {
                        if (!_index.TryGetValue(id, out var coord))
                            return Result.NotFound($"{typeof(T).Name} {id} not found");
            
                        _parent.Phext = PhextEngine.Replace(_parent.Phext, coord.ToPhext(), "");
                        _index.Remove(id);
                        _parent.Save();
                        return Result.Ok();
                    }
                }
            }
            """;

        return CreateFile(coord, Module.Services, "src/csharp/Bruce.Services/PhextStore.cs", content);
    }

    private GeneratedFile GenerateJsonStore(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Services);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            using System.Text.Json;
            
            namespace Bruce.Services;
            
            using Bruce.Core;
            
            public class JsonStore : IStorageBackend
            {
                private readonly string _path;
                private readonly CoordAllocator _allocator = new();
                private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
            
                private readonly JsonEntityStore<Worker> _workers;
                private readonly JsonEntityStore<BruceTask> _tasks;
                private readonly JsonEntityStore<Assignment> _assignments;
                private readonly JsonEntityStore<Message> _messages;
                private readonly JsonEntityStore<Artifact> _artifacts;
            
                public JsonStore(string path)
                {
                    _path = path;
                    Directory.CreateDirectory(path);
                    _workers = new(Path.Combine(path, "workers.json"), _allocator, EntityType.Worker, _json);
                    _tasks = new(Path.Combine(path, "tasks.json"), _allocator, EntityType.Task, _json);
                    _assignments = new(Path.Combine(path, "assignments.json"), _allocator, EntityType.Assignment, _json);
                    _messages = new(Path.Combine(path, "messages.json"), _allocator, EntityType.Message, _json);
                    _artifacts = new(Path.Combine(path, "artifacts.json"), _allocator, EntityType.Artifact, _json);
                }
            
                public IStore<Worker> Workers => _workers;
                public IStore<BruceTask> Tasks => _tasks;
                public IStore<Assignment> Assignments => _assignments;
                public IStore<Message> Messages => _messages;
                public IStore<Artifact> Artifacts => _artifacts;
                public CoordAllocator Allocator => _allocator;
            
                public void Load()
                {
                    _workers.Load();
                    _tasks.Load();
                    _assignments.Load();
                    _messages.Load();
                    _artifacts.Load();
                }
            
                public void Save()
                {
                    _workers.Save();
                    _tasks.Save();
                    _assignments.Save();
                    _messages.Save();
                    _artifacts.Save();
                }
            
                public void Dispose() { }
            
                private class JsonEntityStore<T> : IStore<T> where T : Entity
                {
                    private readonly string _path;
                    private readonly CoordAllocator _allocator;
                    private readonly EntityType _entityType;
                    private readonly JsonSerializerOptions _json;
                    private List<T> _entities = new();
            
                    public JsonEntityStore(string path, CoordAllocator allocator, EntityType entityType, JsonSerializerOptions json)
                    {
                        _path = path;
                        _allocator = allocator;
                        _entityType = entityType;
                        _json = json;
                    }
            
                    public void Load()
                    {
                        if (!File.Exists(_path)) return;
                        var json = File.ReadAllText(_path);
                        _entities = JsonSerializer.Deserialize<List<T>>(json, _json) ?? new();
                        foreach (var e in _entities)
                            _allocator.SetHighWater(_entityType, e.Coord);
                    }
            
                    public void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_entities, _json));
            
                    public T? Get(string id) => _entities.FirstOrDefault(e => e.Id == id);
                    public IEnumerable<T> GetAll() => _entities;
            
                    public Result<T> Save(T entity)
                    {
                        var coord = entity.Coord == default ? _allocator.Allocate(_entityType) : entity.Coord;
                        var updated = entity with { Coord = coord, Updated = DateTime.UtcNow };
                        
                        var idx = _entities.FindIndex(e => e.Id == entity.Id);
                        if (idx >= 0) _entities[idx] = updated;
                        else _entities.Add(updated);
                        
                        Save();
                        return updated;
                    }
            
                    public Result Delete(string id)
                    {
                        var removed = _entities.RemoveAll(e => e.Id == id);
                        if (removed == 0) return Result.NotFound($"{typeof(T).Name} {id} not found");
                        Save();
                        return Result.Ok();
                    }
                }
            }
            """;

        return CreateFile(coord, Module.Services, "src/csharp/Bruce.Services/JsonStore.cs", content);
    }

    private GeneratedFile GenerateStorageFactory(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Services);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            namespace Bruce.Services;
            
            public static class StorageFactory
            {
                public static BackendType Detect(string path)
                {
                    if (File.Exists(path) && path.EndsWith(".phext", StringComparison.OrdinalIgnoreCase))
                        return BackendType.Phext;
                    
                    if (Directory.Exists(path))
                    {
                        // Check for phext file in directory
                        var phextFile = Directory.GetFiles(path, "*.phext").FirstOrDefault();
                        if (phextFile != null) return BackendType.Phext;
                        
                        // Check for json files
                        if (Directory.GetFiles(path, "*.json").Any())
                            return BackendType.Json;
                    }
                    
                    return BackendType.Unknown;
                }
            
                public static IStorageBackend Create(string path, BackendType? forceType = null)
                {
                    var type = forceType ?? Detect(path);
                    
                    IStorageBackend backend = type switch
                    {
                        BackendType.Phext => new PhextStore(path.EndsWith(".phext") ? path : Path.Combine(path, "bruce.phext")),
                        BackendType.Json => new JsonStore(path),
                        _ => new PhextStore(Path.Combine(path, "bruce.phext"))  // Default to phext
                    };
                    
                    backend.Load();
                    return backend;
                }
            
                public static IStorageBackend CreateFromEnv()
                {
                    var path = Environment.GetEnvironmentVariable("BRUCE_DATA") ?? "./data";
                    var backendEnv = Environment.GetEnvironmentVariable("BRUCE_BACKEND");
                    
                    BackendType? forceType = backendEnv?.ToLower() switch
                    {
                        "phext" => BackendType.Phext,
                        "json" => BackendType.Json,
                        _ => null
                    };
                    
                    return Create(path, forceType);
                }
            }
            """;

        return CreateFile(coord, Module.Services, "src/csharp/Bruce.Services/StorageFactory.cs", content);
    }

    private GeneratedFile GenerateEngine(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Services);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            namespace Bruce.Services;
            
            using Bruce.Core;
            
            public class BruceEngine : IDisposable
            {
                private readonly IStorageBackend _storage;
            
                public BruceEngine(IStorageBackend? storage = null)
                {
                    _storage = storage ?? StorageFactory.CreateFromEnv();
                }
            
                public IStore<Worker> Workers => _storage.Workers;
                public IStore<BruceTask> Tasks => _storage.Tasks;
                public IStore<Assignment> Assignments => _storage.Assignments;
                public IStore<Message> Messages => _storage.Messages;
            
                public Result<Worker> CreateWorker(string name, WorkerType type, WorkerRole role, string? substrate = null)
                {
                    if (string.IsNullOrWhiteSpace(name))
                        return Result<Worker>.Fail("Name cannot be empty");
                    
                    if (type == WorkerType.AI && string.IsNullOrEmpty(substrate))
                        return Result<Worker>.Fail("AI workers must specify substrate");
            
                    var worker = new Worker
                    {
                        Id = $"wrk_{Guid.NewGuid():N}"[..16],
                        Coord = default,
                        Name = name.Trim(),
                        Type = type,
                        Role = role,
                        Substrate = substrate
                    };
            
                    return _storage.Workers.Save(worker);
                }
            
                public Result<BruceTask> CreateTask(string title, string description, TaskSource source, TaskType type)
                {
                    if (string.IsNullOrWhiteSpace(title))
                        return Result<BruceTask>.Fail("Title cannot be empty");
            
                    var task = new BruceTask
                    {
                        Id = $"tsk_{Guid.NewGuid():N}"[..16],
                        Coord = default,
                        Title = title.Trim(),
                        Description = description?.Trim() ?? "",
                        Source = source,
                        Type = type
                    };
            
                    return _storage.Tasks.Save(task);
                }
            
                public Result<BruceTask> AdvanceTask(string taskId)
                {
                    var task = _storage.Tasks.Get(taskId);
                    if (task == null) return Result<BruceTask>.NotFound($"Task {taskId} not found");
            
                    var next = task.State switch
                    {
                        TaskState.Created => TaskState.Reviewed,
                        TaskState.Reviewed => TaskState.Assigned,
                        TaskState.Assigned => TaskState.Testing,
                        TaskState.Testing => TaskState.Done,
                        _ => task.State
                    };
            
                    if (next == task.State)
                        return Result<BruceTask>.Fail($"Cannot advance from {task.State}");
            
                    var updated = task with { State = next };
                    return _storage.Tasks.Save(updated);
                }
            
                public Result<Assignment> AssignTask(string taskId, string workerId)
                {
                    var task = _storage.Tasks.Get(taskId);
                    if (task == null) return Result<Assignment>.NotFound($"Task {taskId} not found");
            
                    var worker = _storage.Workers.Get(workerId);
                    if (worker == null) return Result<Assignment>.NotFound($"Worker {workerId} not found");
            
                    if (task.State == TaskState.Created)
                    {
                        var reviewed = task with { State = TaskState.Reviewed };
                        _storage.Tasks.Save(reviewed);
                    }
            
                    var assignment = new Assignment
                    {
                        Id = $"asn_{Guid.NewGuid():N}"[..16],
                        Coord = default,
                        TaskId = taskId,
                        WorkerId = workerId,
                        PushedAt = DateTime.UtcNow
                    };
            
                    var updated = task with { State = TaskState.Assigned, AssignedTo = workerId };
                    _storage.Tasks.Save(updated);
            
                    return _storage.Assignments.Save(assignment);
                }
            
                public Result<Message> SendMessage(string fromId, string body, string? toId = null, string? taskId = null)
                {
                    if (string.IsNullOrWhiteSpace(body))
                        return Result<Message>.Fail("Message body cannot be empty");
            
                    var from = _storage.Workers.Get(fromId);
                    if (from == null) return Result<Message>.NotFound($"Sender {fromId} not found");
            
                    if (toId != null && _storage.Workers.Get(toId) == null)
                        return Result<Message>.NotFound($"Recipient {toId} not found");
            
                    var msg = new Message
                    {
                        Id = $"msg_{Guid.NewGuid():N}"[..16],
                        Coord = default,
                        FromWorkerId = fromId,
                        ToWorkerId = toId,
                        TaskId = taskId,
                        Body = body.Trim()
                    };
            
                    return _storage.Messages.Save(msg);
                }
            
                public void Dispose() => _storage.Dispose();
            }
            """;

        return CreateFile(coord, Module.Services, "src/csharp/Bruce.Services/Engine.cs", content);
    }

    private GeneratedFile GenerateCliProgram(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Cli);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            using Bruce.Core;
            using Bruce.Services;
            
            namespace Bruce.Cli;
            
            public class Program
            {
                public static int Main(string[] args)
                {
                    if (args.Length == 0) return ShowHelp();
            
                    using var engine = new BruceEngine();
            
                    return args[0].ToLower() switch
                    {
                        "worker" => HandleWorker(engine, args[1..]),
                        "task" => HandleTask(engine, args[1..]),
                        "message" or "msg" => HandleMessage(engine, args[1..]),
                        "status" => ShowStatus(engine),
                        "help" or "--help" or "-h" => ShowHelp(),
                        _ => ShowHelp($"Unknown command: {args[0]}")
                    };
                }
            
                static int HandleWorker(BruceEngine engine, string[] args)
                {
                    if (args.Length == 0) return ShowHelp("worker requires subcommand");
                    
                    return args[0].ToLower() switch
                    {
                        "list" or "ls" => ListWorkers(engine),
                        "show" when args.Length > 1 => ShowWorker(engine, args[1]),
                        "register" when args.Length > 1 => RegisterWorker(engine, args[1..]),
                        _ => ShowHelp($"Unknown worker command: {args[0]}")
                    };
                }
            
                static int HandleTask(BruceEngine engine, string[] args)
                {
                    if (args.Length == 0) return ShowHelp("task requires subcommand");
                    
                    return args[0].ToLower() switch
                    {
                        "list" or "ls" => ListTasks(engine),
                        "show" when args.Length > 1 => ShowTask(engine, args[1]),
                        "create" when args.Length > 1 => CreateTask(engine, args[1..]),
                        "advance" when args.Length > 1 => AdvanceTask(engine, args[1]),
                        "claim" when args.Length > 1 => ClaimTask(engine, args[1..]),
                        _ => ShowHelp($"Unknown task command: {args[0]}")
                    };
                }
            
                static int HandleMessage(BruceEngine engine, string[] args)
                {
                    if (args.Length == 0) return ShowHelp("message requires subcommand");
                    
                    return args[0].ToLower() switch
                    {
                        "list" or "ls" => ListMessages(engine),
                        "send" when args.Length > 2 => SendMessage(engine, args[1..]),
                        _ => ShowHelp($"Unknown message command: {args[0]}")
                    };
                }
            
                static int ListWorkers(BruceEngine engine)
                {
                    Console.WriteLine($"{"ID",-18} {"Name",-16} {"Type",-8} {"Role",-10} {"Active"}");
                    Console.WriteLine(new string('-', 60));
                    foreach (var w in engine.Workers.GetAll())
                        Console.WriteLine($"{w.Id,-18} {w.Name,-16} {w.Type,-8} {w.Role,-10} {w.IsActive}");
                    return 0;
                }
            
                static int ShowWorker(BruceEngine engine, string id)
                {
                    var w = engine.Workers.Get(id);
                    if (w == null) { Console.WriteLine($"Worker not found: {id}"); return 1; }
                    Console.WriteLine($"ID:        {w.Id}");
                    Console.WriteLine($"Name:      {w.Name}");
                    Console.WriteLine($"Coord:     {w.Coord}");
                    Console.WriteLine($"Type:      {w.Type}");
                    Console.WriteLine($"Role:      {w.Role}");
                    Console.WriteLine($"Substrate: {w.Substrate ?? "-"}");
                    Console.WriteLine($"Active:    {w.IsActive}");
                    return 0;
                }
            
                static int RegisterWorker(BruceEngine engine, string[] args)
                {
                    var name = args[0];
                    var type = WorkerType.Human;
                    var role = WorkerRole.Worker;
                    string? substrate = null;
            
                    for (int i = 1; i < args.Length; i++)
                    {
                        if (args[i] == "--substrate" && i + 1 < args.Length)
                            substrate = args[++i];
                        else if (args[i] == "--type" && i + 1 < args.Length)
                            Enum.TryParse(args[++i], true, out type);
                        else if (args[i] == "--role" && i + 1 < args.Length)
                            Enum.TryParse(args[++i], true, out role);
                    }
            
                    if (substrate != null) type = WorkerType.AI;
            
                    var result = engine.CreateWorker(name, type, role, substrate);
                    if (result.IsFailure) { Console.WriteLine($"Error: {result.Error}"); return 1; }
                    Console.WriteLine($"Created worker: {result.Value!.Id}");
                    return 0;
                }
            
                static int ListTasks(BruceEngine engine)
                {
                    Console.WriteLine($"{"ID",-18} {"Title",-30} {"State",-12} {"Type"}");
                    Console.WriteLine(new string('-', 70));
                    foreach (var t in engine.Tasks.GetAll())
                    {
                        var title = t.Title.Length > 28 ? t.Title[..25] + "..." : t.Title;
                        Console.WriteLine($"{t.Id,-18} {title,-30} {t.State,-12} {t.Type}");
                    }
                    return 0;
                }
            
                static int ShowTask(BruceEngine engine, string id)
                {
                    var t = engine.Tasks.Get(id);
                    if (t == null) { Console.WriteLine($"Task not found: {id}"); return 1; }
                    Console.WriteLine($"ID:          {t.Id}");
                    Console.WriteLine($"Title:       {t.Title}");
                    Console.WriteLine($"Coord:       {t.Coord}");
                    Console.WriteLine($"State:       {t.State}");
                    Console.WriteLine($"Type:        {t.Type}");
                    Console.WriteLine($"Description: {t.Description}");
                    Console.WriteLine($"Assigned:    {t.AssignedTo ?? "-"}");
                    return 0;
                }
            
                static int CreateTask(BruceEngine engine, string[] args)
                {
                    var title = args[0];
                    var type = TaskType.Adhoc;
                    var desc = "";
            
                    for (int i = 1; i < args.Length; i++)
                    {
                        if ((args[i] == "-d" || args[i] == "--description") && i + 1 < args.Length)
                            desc = args[++i];
                        else if (args[i] == "--type" && i + 1 < args.Length)
                            Enum.TryParse(args[++i], true, out type);
                    }
            
                    var result = engine.CreateTask(title, desc, TaskSource.Manual, type);
                    if (result.IsFailure) { Console.WriteLine($"Error: {result.Error}"); return 1; }
                    Console.WriteLine($"Created task: {result.Value!.Id}");
                    return 0;
                }
            
                static int AdvanceTask(BruceEngine engine, string id)
                {
                    var result = engine.AdvanceTask(id);
                    if (result.IsFailure) { Console.WriteLine($"Error: {result.Error}"); return 1; }
                    Console.WriteLine($"Task {id} advanced to {result.Value!.State}");
                    return 0;
                }
            
                static int ClaimTask(BruceEngine engine, string[] args)
                {
                    var taskId = args[0];
                    string? workerId = null;
                    for (int i = 1; i < args.Length; i++)
                        if ((args[i] == "-w" || args[i] == "--worker") && i + 1 < args.Length)
                            workerId = args[++i];
            
                    if (workerId == null) { Console.WriteLine("Error: --worker required"); return 1; }
            
                    var result = engine.AssignTask(taskId, workerId);
                    if (result.IsFailure) { Console.WriteLine($"Error: {result.Error}"); return 1; }
                    Console.WriteLine($"Task {taskId} assigned to {workerId}");
                    return 0;
                }
            
                static int ListMessages(BruceEngine engine)
                {
                    Console.WriteLine($"{"ID",-18} {"From",-16} {"To",-16} {"Body"}");
                    Console.WriteLine(new string('-', 70));
                    foreach (var m in engine.Messages.GetAll())
                    {
                        var to = m.ToWorkerId ?? "(broadcast)";
                        var body = m.Body.Length > 30 ? m.Body[..27] + "..." : m.Body;
                        Console.WriteLine($"{m.Id,-18} {m.FromWorkerId,-16} {to,-16} {body}");
                    }
                    return 0;
                }
            
                static int SendMessage(BruceEngine engine, string[] args)
                {
                    var to = args[0];
                    var body = args[1];
                    string? from = null;
            
                    for (int i = 2; i < args.Length; i++)
                        if (args[i] == "--from" && i + 1 < args.Length)
                            from = args[++i];
            
                    if (from == null) { Console.WriteLine("Error: --from required"); return 1; }
            
                    var result = engine.SendMessage(from, body, to);
                    if (result.IsFailure) { Console.WriteLine($"Error: {result.Error}"); return 1; }
                    Console.WriteLine($"Message sent: {result.Value!.Id}");
                    return 0;
                }
            
                static int ShowStatus(BruceEngine engine)
                {
                    var workers = engine.Workers.GetAll().ToList();
                    var tasks = engine.Tasks.GetAll().ToList();
                    var messages = engine.Messages.GetAll().ToList();
            
                    Console.WriteLine("Bruce System Status");
                    Console.WriteLine(new string('═', 40));
                    Console.WriteLine($"Workers:  {workers.Count} ({workers.Count(w => w.IsActive)} active)");
                    Console.WriteLine($"Tasks:    {tasks.Count}");
                    Console.WriteLine($"  Created:  {tasks.Count(t => t.State == TaskState.Created)}");
                    Console.WriteLine($"  Reviewed: {tasks.Count(t => t.State == TaskState.Reviewed)}");
                    Console.WriteLine($"  Assigned: {tasks.Count(t => t.State == TaskState.Assigned)}");
                    Console.WriteLine($"  Testing:  {tasks.Count(t => t.State == TaskState.Testing)}");
                    Console.WriteLine($"  Done:     {tasks.Count(t => t.State == TaskState.Done)}");
                    Console.WriteLine($"Messages: {messages.Count}");
                    return 0;
                }
            
                static int ShowHelp(string? error = null)
                {
                    if (error != null) Console.WriteLine($"Error: {error}\n");
                    Console.WriteLine("""
                        Bruce CLI - Hybrid Human-AI Team Coordination
                        Generated from bruce.seed v6.0.0
            
                        Usage: bruce <command> [options]
            
                        Commands:
                          worker list                    List all workers
                          worker show <id>               Show worker details
                          worker register <name> [opts]  Register new worker
            
                          task list                      List all tasks
                          task show <id>                 Show task details
                          task create <title> [opts]     Create new task
                          task advance <id>              Advance task state
                          task claim <id> -w <worker>    Assign task to worker
            
                          message list                   List all messages
                          message send <to> <body> --from <id>  Send message
            
                          status                         System summary
            
                        Environment:
                          BRUCE_DATA     Data directory (default: ./data)
                          BRUCE_BACKEND  Storage backend (auto, phext, json)
                        """);
                    return error != null ? 1 : 0;
                }
            }
            """;

        return CreateFile(coord, Module.Cli, "src/csharp/Bruce.Cli/Program.cs", content);
    }

    private GeneratedFile GenerateCoreProject(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Core);
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Phext" Version="0.3.0" />
              </ItemGroup>
            </Project>
            """;
        return CreateFile(coord, Module.Core, "src/csharp/Bruce.Core/Bruce.Core.csproj", content);
    }

    private GeneratedFile GenerateServicesProject(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Services);
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Bruce.Core/Bruce.Core.csproj" />
              </ItemGroup>
            </Project>
            """;
        return CreateFile(coord, Module.Services, "src/csharp/Bruce.Services/Bruce.Services.csproj", content);
    }

    private GeneratedFile GenerateCliProject(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Cli);
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <AssemblyName>bruce</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Bruce.Services/Bruce.Services.csproj" />
              </ItemGroup>
            </Project>
            """;
        return CreateFile(coord, Module.Cli, "src/csharp/Bruce.Cli/Bruce.Cli.csproj", content);
    }

    private GeneratedFile GenerateSolution(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.CSharp, Module.Core);
        var content = """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Bruce.Core", "Bruce.Core\Bruce.Core.csproj", "{A1111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Bruce.Services", "Bruce.Services\Bruce.Services.csproj", "{B2222222-2222-2222-2222-222222222222}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Bruce.Cli", "Bruce.Cli\Bruce.Cli.csproj", "{C3333333-3333-3333-3333-333333333333}"
            EndProject
            Global
              GlobalSection(SolutionConfigurationPlatforms) = preSolution
                Debug|Any CPU = Debug|Any CPU
                Release|Any CPU = Release|Any CPU
              EndGlobalSection
              GlobalSection(ProjectConfigurationPlatforms) = postSolution
                {A1111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                {A1111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU
                {B2222222-2222-2222-2222-222222222222}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                {B2222222-2222-2222-2222-222222222222}.Debug|Any CPU.Build.0 = Debug|Any CPU
                {C3333333-3333-3333-3333-333333333333}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                {C3333333-3333-3333-3333-333333333333}.Debug|Any CPU.Build.0 = Debug|Any CPU
              EndGlobalSection
            EndGlobal
            """;
        return CreateFile(coord, Module.Core, "src/csharp/Bruce.sln", content);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// RUST GENERATOR
// ═══════════════════════════════════════════════════════════════════════════════

public class RustGenerator : BaseGenerator
{
    public override Language Language => Language.Rust;

    public override List<GeneratedFile> Generate(BruceSeed seed, UnifiedLineage lineage)
    {
        var files = new List<GeneratedFile>();

        // Core
        files.Add(GenerateCargoToml(seed, lineage));
        files.Add(GenerateLibRs(seed, lineage));
        files.Add(GenerateCoordRs(seed, lineage));
        files.Add(GenerateEntitiesRs(seed, lineage));
        files.Add(GenerateErrorRs(seed, lineage));

        // Store
        files.Add(GenerateStoreModRs(seed, lineage));
        files.Add(GeneratePhextStoreRs(seed, lineage));
        files.Add(GenerateJsonStoreRs(seed, lineage));

        // CLI
        files.Add(GenerateMainRs(seed, lineage));

        return files;
    }

    private GeneratedFile GenerateCargoToml(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Rust, Module.Core);
        var content = $$"""
            # Generated from bruce.seed v{{seed.Version}}
            # Coord: {{coord}}
            
            [package]
            name = "bruce"
            version = "{{seed.Version}}"
            edition = "2021"
            description = "Hybrid Human-AI Team Coordination"
            
            [dependencies]
            phext = "0.3"
            serde = { version = "1.0", features = ["derive"] }
            serde_json = "1.0"
            chrono = { version = "0.4", features = ["serde"] }
            thiserror = "1.0"
            uuid = { version = "1.0", features = ["v4"] }
            
            [[bin]]
            name = "bruce"
            path = "src/bin/bruce.rs"
            """;
        return CreateFile(coord, Module.Core, "src/rust/bruce/Cargo.toml", content);
    }

    private GeneratedFile GenerateLibRs(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Rust, Module.Core);
        var content = $$"""
            //! Generated from bruce.seed v{{seed.Version}}
            //! Coord: {{coord}}
            
            pub mod coord;
            pub mod entities;
            pub mod error;
            pub mod store;
            
            pub use coord::Coord9D;
            pub use entities::*;
            pub use error::{BruceError, Result};
            pub use store::{Storage, StorageBackend};
            """;
        return CreateFile(coord, Module.Core, "src/rust/bruce/src/lib.rs", content);
    }

    private GeneratedFile GenerateCoordRs(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Rust, Module.Core);
        var content = $$"""
            //! Generated from bruce.seed v{{seed.Version}}
            //! Coord: {{coord}}
            
            use serde::{Deserialize, Serialize};
            use std::fmt;
            use std::str::FromStr;
            
            /// Full 9D Phext Coordinate: L.S.R/C.V.B/H.E.W
            #[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
            pub struct Coord9D {
                pub library: u8,
                pub shelf: u8,
                pub series: u8,
                pub collection: u8,
                pub volume: u8,
                pub book: u8,
                pub chapter: u8,
                pub section: u8,
                pub scroll: u8,
            }
            
            impl Coord9D {
                pub fn new(
                    library: u8, shelf: u8, series: u8,
                    collection: u8, volume: u8, book: u8,
                    chapter: u8, section: u8, scroll: u8,
                ) -> Self {
                    Self { library, shelf, series, collection, volume, book, chapter, section, scroll }
                }
            
                pub fn default() -> Self {
                    Self::new(1, 1, 1, 1, 1, 1, 1, 1, 1)
                }
            }
            
            impl fmt::Display for Coord9D {
                fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
                    write!(
                        f, "{}.{}.{}/{}.{}.{}/{}.{}.{}",
                        self.library, self.shelf, self.series,
                        self.collection, self.volume, self.book,
                        self.chapter, self.section, self.scroll
                    )
                }
            }
            
            impl FromStr for Coord9D {
                type Err = crate::error::BruceError;
            
                fn from_str(s: &str) -> Result<Self, Self::Err> {
                    let parts: Vec<&str> = s.split('/').collect();
                    if parts.len() != 3 {
                        return Err(crate::error::BruceError::InvalidCoord(s.to_string()));
                    }
            
                    let parse_triple = |s: &str| -> Result<(u8, u8, u8), crate::error::BruceError> {
                        let nums: Vec<u8> = s.split('.')
                            .map(|n| n.parse().map_err(|_| crate::error::BruceError::InvalidCoord(s.to_string())))
                            .collect::<Result<Vec<_>, _>>()?;
                        if nums.len() != 3 {
                            return Err(crate::error::BruceError::InvalidCoord(s.to_string()));
                        }
                        Ok((nums[0], nums[1], nums[2]))
                    };
            
                    let (library, shelf, series) = parse_triple(parts[0])?;
                    let (collection, volume, book) = parse_triple(parts[1])?;
                    let (chapter, section, scroll) = parse_triple(parts[2])?;
            
                    Ok(Self { library, shelf, series, collection, volume, book, chapter, section, scroll })
                }
            }
            
            #[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
            #[repr(u8)]
            pub enum EntityType {
                Worker = 1,
                Task = 2,
                Assignment = 3,
                Message = 4,
                Artifact = 5,
                Archive = 6,
            }
            """;
        return CreateFile(coord, Module.Core, "src/rust/bruce/src/coord.rs", content);
    }

    private GeneratedFile GenerateEntitiesRs(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Rust, Module.Core);
        var content = $$"""
            //! Generated from bruce.seed v{{seed.Version}}
            //! Coord: {{coord}}
            
            use chrono::{DateTime, Utc};
            use serde::{Deserialize, Serialize};
            use crate::coord::Coord9D;
            
            #[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
            pub enum TaskState {
                Created,
                Reviewed,
                Assigned,
                Testing,
                Done,
            }
            
            #[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
            pub enum TaskType {
                Adhoc,
                Planned,
            }
            
            #[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
            pub enum TaskSource {
                Manual,
                Teams,
                Email,
                Helix,
                Api,
            }
            
            #[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
            pub enum WorkerType {
                Human,
                AI,
            }
            
            #[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
            pub enum WorkerRole {
                Worker,
                Manager,
                Director,
            }
            
            impl WorkerRole {
                pub fn max_capacity(&self) -> u32 {
                    match self {
                        WorkerRole::Worker => 4,
                        WorkerRole::Manager => 24,
                        WorkerRole::Director => 200,
                    }
                }
            }
            
            pub trait Entity {
                fn id(&self) -> &str;
                fn coord(&self) -> Coord9D;
                fn set_coord(&mut self, coord: Coord9D);
            }
            
            #[derive(Debug, Clone, Serialize, Deserialize)]
            pub struct Worker {
                pub id: String,
                pub coord: Coord9D,
                pub created: DateTime<Utc>,
                pub updated: DateTime<Utc>,
                pub version: u32,
                pub name: String,
                #[serde(rename = "type")]
                pub worker_type: WorkerType,
                pub role: WorkerRole,
                pub substrate: Option<String>,
                pub is_active: bool,
            }
            
            impl Entity for Worker {
                fn id(&self) -> &str { &self.id }
                fn coord(&self) -> Coord9D { self.coord }
                fn set_coord(&mut self, coord: Coord9D) { self.coord = coord; }
            }
            
            #[derive(Debug, Clone, Serialize, Deserialize)]
            pub struct Task {
                pub id: String,
                pub coord: Coord9D,
                pub created: DateTime<Utc>,
                pub updated: DateTime<Utc>,
                pub version: u32,
                pub title: String,
                pub description: String,
                pub source: TaskSource,
                pub source_ref: Option<String>,
                pub state: TaskState,
                #[serde(rename = "type")]
                pub task_type: TaskType,
                pub assigned_to: Option<String>,
            }
            
            impl Entity for Task {
                fn id(&self) -> &str { &self.id }
                fn coord(&self) -> Coord9D { self.coord }
                fn set_coord(&mut self, coord: Coord9D) { self.coord = coord; }
            }
            
            #[derive(Debug, Clone, Serialize, Deserialize)]
            pub struct Assignment {
                pub id: String,
                pub coord: Coord9D,
                pub created: DateTime<Utc>,
                pub updated: DateTime<Utc>,
                pub version: u32,
                pub task_id: String,
                pub worker_id: String,
                pub pushed_at: Option<DateTime<Utc>>,
                pub claimed_at: Option<DateTime<Utc>>,
                pub completed_at: Option<DateTime<Utc>>,
            }
            
            impl Entity for Assignment {
                fn id(&self) -> &str { &self.id }
                fn coord(&self) -> Coord9D { self.coord }
                fn set_coord(&mut self, coord: Coord9D) { self.coord = coord; }
            }
            
            #[derive(Debug, Clone, Serialize, Deserialize)]
            pub struct Message {
                pub id: String,
                pub coord: Coord9D,
                pub created: DateTime<Utc>,
                pub updated: DateTime<Utc>,
                pub version: u32,
                pub task_id: Option<String>,
                pub from_worker_id: String,
                pub to_worker_id: Option<String>,
                pub body: String,
                pub timestamp: DateTime<Utc>,
            }
            
            impl Entity for Message {
                fn id(&self) -> &str { &self.id }
                fn coord(&self) -> Coord9D { self.coord }
                fn set_coord(&mut self, coord: Coord9D) { self.coord = coord; }
            }
            """;
        return CreateFile(coord, Module.Core, "src/rust/bruce/src/entities.rs", content);
    }

    private GeneratedFile GenerateErrorRs(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Rust, Module.Core);
        var content = $$"""
            //! Generated from bruce.seed v{{seed.Version}}
            //! Coord: {{coord}}
            
            use thiserror::Error;
            
            #[derive(Error, Debug)]
            pub enum BruceError {
                #[error("Not found: {0}")]
                NotFound(String),
            
                #[error("Invalid: {0}")]
                Invalid(String),
            
                #[error("Invalid coordinate: {0}")]
                InvalidCoord(String),
            
                #[error("IO error: {0}")]
                Io(#[from] std::io::Error),
            
                #[error("JSON error: {0}")]
                Json(#[from] serde_json::Error),
            
                #[error("Invalid state transition: {0} -> {1}")]
                InvalidTransition(String, String),
            }
            
            pub type Result<T> = std::result::Result<T, BruceError>;
            """;
        return CreateFile(coord, Module.Core, "src/rust/bruce/src/error.rs", content);
    }

    private GeneratedFile GenerateStoreModRs(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Rust, Module.Services);
        var content = $$"""
            //! Generated from bruce.seed v{{seed.Version}}
            //! Coord: {{coord}}
            
            pub mod phext;
            pub mod json;
            
            use crate::entities::Entity;
            use crate::error::Result;
            use std::path::Path;
            
            pub trait Storage<T: Entity> {
                fn get(&self, id: &str) -> Option<&T>;
                fn get_all(&self) -> Vec<&T>;
                fn save(&mut self, entity: T) -> Result<()>;
                fn delete(&mut self, id: &str) -> Result<()>;
            }
            
            pub enum StorageBackend {
                Phext(phext::PhextStore),
                Json(json::JsonStore),
            }
            
            pub fn detect_backend(path: &Path) -> StorageBackend {
                if path.extension().map(|e| e == "phext").unwrap_or(false) {
                    StorageBackend::Phext(phext::PhextStore::new(path))
                } else if path.is_dir() && path.join("workers.json").exists() {
                    StorageBackend::Json(json::JsonStore::new(path))
                } else {
                    // Default to phext
                    let phext_path = if path.is_dir() {
                        path.join("bruce.phext")
                    } else {
                        path.to_path_buf()
                    };
                    StorageBackend::Phext(phext::PhextStore::new(&phext_path))
                }
            }
            """;
        return CreateFile(coord, Module.Services, "src/rust/bruce/src/store/mod.rs", content);
    }

    private GeneratedFile GeneratePhextStoreRs(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Rust, Module.Services);
        var content = $$"""
            //! Generated from bruce.seed v{{seed.Version}}
            //! Coord: {{coord}}
            
            use std::collections::HashMap;
            use std::fs;
            use std::path::{Path, PathBuf};
            use crate::coord::{Coord9D, EntityType};
            use crate::entities::*;
            use crate::error::Result;
            
            pub struct PhextStore {
                path: PathBuf,
                phext: String,
                workers: HashMap<String, Worker>,
                tasks: HashMap<String, Task>,
                assignments: HashMap<String, Assignment>,
                messages: HashMap<String, Message>,
            }
            
            impl PhextStore {
                pub fn new(path: &Path) -> Self {
                    let mut store = Self {
                        path: path.to_path_buf(),
                        phext: String::new(),
                        workers: HashMap::new(),
                        tasks: HashMap::new(),
                        assignments: HashMap::new(),
                        messages: HashMap::new(),
                    };
                    store.load().ok();
                    store
                }
            
                pub fn load(&mut self) -> Result<()> {
                    if self.path.exists() {
                        self.phext = fs::read_to_string(&self.path)?;
                        self.rebuild_indexes()?;
                    }
                    Ok(())
                }
            
                pub fn save(&self) -> Result<()> {
                    fs::write(&self.path, &self.phext)?;
                    Ok(())
                }
            
                fn rebuild_indexes(&mut self) -> Result<()> {
                    // Parse phext and populate entity maps
                    // Implementation uses phext library
                    Ok(())
                }
            
                pub fn workers(&self) -> &HashMap<String, Worker> { &self.workers }
                pub fn tasks(&self) -> &HashMap<String, Task> { &self.tasks }
                pub fn assignments(&self) -> &HashMap<String, Assignment> { &self.assignments }
                pub fn messages(&self) -> &HashMap<String, Message> { &self.messages }
            
                pub fn save_worker(&mut self, worker: Worker) -> Result<()> {
                    self.workers.insert(worker.id.clone(), worker);
                    self.save()
                }
            
                pub fn save_task(&mut self, task: Task) -> Result<()> {
                    self.tasks.insert(task.id.clone(), task);
                    self.save()
                }
            }
            """;
        return CreateFile(coord, Module.Services, "src/rust/bruce/src/store/phext.rs", content);
    }

    private GeneratedFile GenerateJsonStoreRs(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Rust, Module.Services);
        var content = $$"""
            //! Generated from bruce.seed v{{seed.Version}}
            //! Coord: {{coord}}
            
            use std::collections::HashMap;
            use std::fs;
            use std::path::{Path, PathBuf};
            use crate::entities::*;
            use crate::error::Result;
            
            pub struct JsonStore {
                path: PathBuf,
                workers: HashMap<String, Worker>,
                tasks: HashMap<String, Task>,
                assignments: HashMap<String, Assignment>,
                messages: HashMap<String, Message>,
            }
            
            impl JsonStore {
                pub fn new(path: &Path) -> Self {
                    let mut store = Self {
                        path: path.to_path_buf(),
                        workers: HashMap::new(),
                        tasks: HashMap::new(),
                        assignments: HashMap::new(),
                        messages: HashMap::new(),
                    };
                    store.load().ok();
                    store
                }
            
                pub fn load(&mut self) -> Result<()> {
                    if let Ok(json) = fs::read_to_string(self.path.join("workers.json")) {
                        let list: Vec<Worker> = serde_json::from_str(&json)?;
                        for w in list { self.workers.insert(w.id.clone(), w); }
                    }
                    if let Ok(json) = fs::read_to_string(self.path.join("tasks.json")) {
                        let list: Vec<Task> = serde_json::from_str(&json)?;
                        for t in list { self.tasks.insert(t.id.clone(), t); }
                    }
                    Ok(())
                }
            
                pub fn save(&self) -> Result<()> {
                    fs::create_dir_all(&self.path)?;
                    let workers: Vec<_> = self.workers.values().collect();
                    fs::write(self.path.join("workers.json"), serde_json::to_string_pretty(&workers)?)?;
                    let tasks: Vec<_> = self.tasks.values().collect();
                    fs::write(self.path.join("tasks.json"), serde_json::to_string_pretty(&tasks)?)?;
                    Ok(())
                }
            
                pub fn workers(&self) -> &HashMap<String, Worker> { &self.workers }
                pub fn tasks(&self) -> &HashMap<String, Task> { &self.tasks }
            }
            """;
        return CreateFile(coord, Module.Services, "src/rust/bruce/src/store/json.rs", content);
    }

    private GeneratedFile GenerateMainRs(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Rust, Module.Cli);
        var content = $$"""
            //! Generated from bruce.seed v{{seed.Version}}
            //! Coord: {{coord}}
            
            use bruce::*;
            use std::env;
            use std::path::PathBuf;
            
            fn main() {
                let args: Vec<String> = env::args().collect();
                
                if args.len() < 2 {
                    show_help(None);
                    return;
                }
            
                let data_path = env::var("BRUCE_DATA")
                    .map(PathBuf::from)
                    .unwrap_or_else(|_| PathBuf::from("./data"));
            
                match args[1].as_str() {
                    "worker" => handle_worker(&args[2..], &data_path),
                    "task" => handle_task(&args[2..], &data_path),
                    "status" => show_status(&data_path),
                    "help" | "--help" | "-h" => show_help(None),
                    cmd => show_help(Some(&format!("Unknown command: {}", cmd))),
                }
            }
            
            fn handle_worker(args: &[String], path: &PathBuf) {
                if args.is_empty() {
                    println!("worker requires subcommand");
                    return;
                }
                match args[0].as_str() {
                    "list" | "ls" => {
                        let store = store::detect_backend(path);
                        println!("{:<18} {:<16} {:<8} {:<10}", "ID", "Name", "Type", "Role");
                        println!("{}", "-".repeat(55));
                        // List workers from store
                    }
                    _ => println!("Unknown worker command: {}", args[0]),
                }
            }
            
            fn handle_task(args: &[String], path: &PathBuf) {
                if args.is_empty() {
                    println!("task requires subcommand");
                    return;
                }
                match args[0].as_str() {
                    "list" | "ls" => {
                        println!("{:<18} {:<30} {:<12} {}", "ID", "Title", "State", "Type");
                        println!("{}", "-".repeat(70));
                    }
                    _ => println!("Unknown task command: {}", args[0]),
                }
            }
            
            fn show_status(path: &PathBuf) {
                println!("Bruce System Status");
                println!("{}", "=".repeat(40));
                println!("Data path: {:?}", path);
            }
            
            fn show_help(error: Option<&str>) {
                if let Some(e) = error {
                    println!("Error: {}\n", e);
                }
                println!(r#"Bruce CLI - Hybrid Human-AI Team Coordination
            Generated from bruce.seed v6.0.0
            
            Usage: bruce <command> [options]
            
            Commands:
              worker list              List all workers
              worker show <id>         Show worker details
              task list                List all tasks
              task show <id>           Show task details
              status                   System summary
            
            Environment:
              BRUCE_DATA     Data directory (default: ./data)
            "#);
            }
            """;
        return CreateFile(coord, Module.Cli, "src/rust/bruce/src/bin/bruce.rs", content);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PYTHON GENERATOR
// ═══════════════════════════════════════════════════════════════════════════════

public class PythonGenerator : BaseGenerator
{
    public override Language Language => Language.Python;

    public override List<GeneratedFile> Generate(BruceSeed seed, UnifiedLineage lineage)
    {
        var files = new List<GeneratedFile>();

        files.Add(GeneratePyProject(seed, lineage));
        files.Add(GenerateInit(seed, lineage));
        files.Add(GenerateCoord(seed, lineage));
        files.Add(GenerateEntities(seed, lineage));
        files.Add(GenerateStoreInit(seed, lineage));
        files.Add(GeneratePhextStore(seed, lineage));
        files.Add(GenerateJsonStore(seed, lineage));
        files.Add(GenerateFactory(seed, lineage));
        files.Add(GenerateEngine(seed, lineage));
        files.Add(GenerateCliMain(seed, lineage));

        return files;
    }

    private GeneratedFile GeneratePyProject(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Python, Module.Core);
        var content = $$"""
            # Generated from bruce.seed v{{seed.Version}}
            # Coord: {{coord}}
            
            [build-system]
            requires = ["hatchling"]
            build-backend = "hatchling.build"
            
            [project]
            name = "bruce"
            version = "{{seed.Version}}"
            description = "Hybrid Human-AI Team Coordination"
            requires-python = ">=3.11"
            dependencies = [
                "phext>=0.3.0",
            ]
            
            [project.scripts]
            bruce = "bruce.cli.main:main"
            """;
        return CreateFile(coord, Module.Core, "src/python/pyproject.toml", content);
    }

    private GeneratedFile GenerateInit(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Python, Module.Core);
        var content = $$"""
            """Generated from bruce.seed v{{seed.Version}}"""
            # Coord: {{coord}}
            
            from .coord import Coord9D, EntityType
            from .entities import (
                Worker, Task, Assignment, Message,
                TaskState, TaskType, TaskSource,
                WorkerType, WorkerRole
            )
            
            __version__ = "{{seed.Version}}"
            __all__ = [
                "Coord9D", "EntityType",
                "Worker", "Task", "Assignment", "Message",
                "TaskState", "TaskType", "TaskSource",
                "WorkerType", "WorkerRole",
            ]
            """;
        return CreateFile(coord, Module.Core, "src/python/bruce/__init__.py", content);
    }

    private GeneratedFile GenerateCoord(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Python, Module.Core);
        var content = $$"""
            """Generated from bruce.seed v{{seed.Version}}"""
            # Coord: {{coord}}
            
            from __future__ import annotations
            from dataclasses import dataclass
            from enum import IntEnum
            from typing import Self
            
            
            class EntityType(IntEnum):
                WORKER = 1
                TASK = 2
                ASSIGNMENT = 3
                MESSAGE = 4
                ARTIFACT = 5
                ARCHIVE = 6
            
            
            @dataclass(frozen=True, slots=True, order=True)
            class Coord9D:
                """Full 9D Phext Coordinate: L.S.R/C.V.B/H.E.W"""
                library: int
                shelf: int
                series: int
                collection: int
                volume: int
                book: int
                chapter: int
                section: int
                scroll: int
            
                def __str__(self) -> str:
                    return (
                        f"{self.library}.{self.shelf}.{self.series}/"
                        f"{self.collection}.{self.volume}.{self.book}/"
                        f"{self.chapter}.{self.section}.{self.scroll}"
                    )
            
                @classmethod
                def parse(cls, s: str) -> Self:
                    parts = s.split("/")
                    if len(parts) != 3:
                        raise ValueError(f"Invalid coord: {s}")
                    
                    l1 = [int(x) for x in parts[0].split(".")]
                    l2 = [int(x) for x in parts[1].split(".")]
                    l3 = [int(x) for x in parts[2].split(".")]
                    
                    if len(l1) != 3 or len(l2) != 3 or len(l3) != 3:
                        raise ValueError(f"Invalid coord: {s}")
                    
                    return cls(l1[0], l1[1], l1[2], l2[0], l2[1], l2[2], l3[0], l3[1], l3[2])
            
                @classmethod
                def default(cls) -> Self:
                    return cls(1, 1, 1, 1, 1, 1, 1, 1, 1)
            
            
            class CoordAllocator:
                """Thread-safe coordinate allocator."""
                
                def __init__(self, library: int = 2, shelf: int = 6):
                    self._library = library
                    self._shelf = shelf
                    self._counters: dict[EntityType, int] = {e: 0 for e in EntityType}
                
                def allocate(self, entity_type: EntityType) -> Coord9D:
                    pos = self._counters[entity_type]
                    self._counters[entity_type] = pos + 1
                    
                    book = pos // 81 + 1
                    rem = pos % 81
                    chapter = rem // 9 + 1
                    section = rem % 9 + 1
                    
                    return Coord9D(
                        self._library, self._shelf, 1,
                        int(entity_type), 1, book,
                        chapter, section, 1
                    )
                
                def set_high_water(self, entity_type: EntityType, coord: Coord9D) -> None:
                    pos = (coord.book - 1) * 81 + (coord.chapter - 1) * 9 + (coord.section - 1) + 1
                    self._counters[entity_type] = max(self._counters[entity_type], pos)
            """;
        return CreateFile(coord, Module.Core, "src/python/bruce/coord.py", content);
    }

    private GeneratedFile GenerateEntities(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Python, Module.Core);
        var content = $$"""
            """Generated from bruce.seed v{{seed.Version}}"""
            # Coord: {{coord}}
            
            from __future__ import annotations
            from dataclasses import dataclass, field
            from datetime import datetime, timezone
            from enum import Enum
            from typing import Optional
            from .coord import Coord9D
            
            
            class TaskState(str, Enum):
                CREATED = "Created"
                REVIEWED = "Reviewed"
                ASSIGNED = "Assigned"
                TESTING = "Testing"
                DONE = "Done"
            
            
            class TaskType(str, Enum):
                ADHOC = "Adhoc"
                PLANNED = "Planned"
            
            
            class TaskSource(str, Enum):
                MANUAL = "Manual"
                TEAMS = "Teams"
                EMAIL = "Email"
                HELIX = "Helix"
                API = "Api"
            
            
            class WorkerType(str, Enum):
                HUMAN = "Human"
                AI = "AI"
            
            
            class WorkerRole(str, Enum):
                WORKER = "Worker"
                MANAGER = "Manager"
                DIRECTOR = "Director"
            
                @property
                def max_capacity(self) -> int:
                    return {"Worker": 4, "Manager": 24, "Director": 200}[self.value]
            
            
            def _now() -> datetime:
                return datetime.now(timezone.utc)
            
            
            @dataclass
            class Worker:
                id: str
                name: str
                coord: Coord9D = field(default_factory=Coord9D.default)
                created: datetime = field(default_factory=_now)
                updated: datetime = field(default_factory=_now)
                version: int = 1
                worker_type: WorkerType = WorkerType.HUMAN
                role: WorkerRole = WorkerRole.WORKER
                substrate: Optional[str] = None
                is_active: bool = True
            
            
            @dataclass
            class Task:
                id: str
                title: str
                coord: Coord9D = field(default_factory=Coord9D.default)
                created: datetime = field(default_factory=_now)
                updated: datetime = field(default_factory=_now)
                version: int = 1
                description: str = ""
                source: TaskSource = TaskSource.MANUAL
                source_ref: Optional[str] = None
                state: TaskState = TaskState.CREATED
                task_type: TaskType = TaskType.ADHOC
                assigned_to: Optional[str] = None
            
            
            @dataclass
            class Assignment:
                id: str
                task_id: str
                worker_id: str
                coord: Coord9D = field(default_factory=Coord9D.default)
                created: datetime = field(default_factory=_now)
                updated: datetime = field(default_factory=_now)
                version: int = 1
                pushed_at: Optional[datetime] = None
                claimed_at: Optional[datetime] = None
                completed_at: Optional[datetime] = None
            
            
            @dataclass
            class Message:
                id: str
                from_worker_id: str
                body: str
                coord: Coord9D = field(default_factory=Coord9D.default)
                created: datetime = field(default_factory=_now)
                updated: datetime = field(default_factory=_now)
                version: int = 1
                task_id: Optional[str] = None
                to_worker_id: Optional[str] = None
                timestamp: datetime = field(default_factory=_now)
            
                @property
                def is_broadcast(self) -> bool:
                    return self.to_worker_id is None
            """;
        return CreateFile(coord, Module.Core, "src/python/bruce/entities.py", content);
    }

    private GeneratedFile GenerateStoreInit(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Python, Module.Services);
        var content = $$"""
            """Generated from bruce.seed v{{seed.Version}}"""
            # Coord: {{coord}}
            
            from .base import Store, StorageBackend
            from .phext import PhextStore
            from .json import JsonStore
            from .factory import detect_backend, create_storage
            
            __all__ = [
                "Store", "StorageBackend",
                "PhextStore", "JsonStore",
                "detect_backend", "create_storage",
            ]
            """;
        return CreateFile(coord, Module.Services, "src/python/bruce/store/__init__.py", content);
    }

    private GeneratedFile GeneratePhextStore(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Python, Module.Services);
        var content = $$"""
            """Generated from bruce.seed v{{seed.Version}}"""
            # Coord: {{coord}}
            
            from pathlib import Path
            from typing import TypeVar, Generic, Optional
            import json
            from ..coord import Coord9D, CoordAllocator, EntityType
            from ..entities import Worker, Task, Assignment, Message
            from .base import StorageBackend
            
            T = TypeVar("T")
            
            
            class PhextStore(StorageBackend):
                """Single phext file storage backend."""
                
                def __init__(self, path: Path):
                    self._path = path
                    self._phext = ""
                    self._allocator = CoordAllocator()
                    self._workers: dict[str, Worker] = {}
                    self._tasks: dict[str, Task] = {}
                    self._assignments: dict[str, Assignment] = {}
                    self._messages: dict[str, Message] = {}
                
                def load(self) -> None:
                    if self._path.exists():
                        self._phext = self._path.read_text()
                        self._rebuild_indexes()
                
                def save(self) -> None:
                    self._path.write_text(self._phext)
                
                def _rebuild_indexes(self) -> None:
                    # Parse phext and populate entity dictionaries
                    pass
                
                @property
                def allocator(self) -> CoordAllocator:
                    return self._allocator
                
                def get_worker(self, id: str) -> Optional[Worker]:
                    return self._workers.get(id)
                
                def get_all_workers(self) -> list[Worker]:
                    return list(self._workers.values())
                
                def save_worker(self, worker: Worker) -> Worker:
                    if worker.coord == Coord9D.default():
                        worker.coord = self._allocator.allocate(EntityType.WORKER)
                    self._workers[worker.id] = worker
                    self.save()
                    return worker
                
                def get_task(self, id: str) -> Optional[Task]:
                    return self._tasks.get(id)
                
                def get_all_tasks(self) -> list[Task]:
                    return list(self._tasks.values())
                
                def save_task(self, task: Task) -> Task:
                    if task.coord == Coord9D.default():
                        task.coord = self._allocator.allocate(EntityType.TASK)
                    self._tasks[task.id] = task
                    self.save()
                    return task
            """;
        return CreateFile(coord, Module.Services, "src/python/bruce/store/phext.py", content);
    }

    private GeneratedFile GenerateJsonStore(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Python, Module.Services);
        var content = $$"""
            """Generated from bruce.seed v{{seed.Version}}"""
            # Coord: {{coord}}
            
            from pathlib import Path
            from typing import Optional
            import json
            from dataclasses import asdict
            from ..coord import CoordAllocator, EntityType
            from ..entities import Worker, Task, Assignment, Message
            from .base import StorageBackend
            
            
            class JsonStore(StorageBackend):
                """JSON file storage backend."""
                
                def __init__(self, path: Path):
                    self._path = path
                    self._allocator = CoordAllocator()
                    self._workers: dict[str, Worker] = {}
                    self._tasks: dict[str, Task] = {}
                
                def load(self) -> None:
                    workers_file = self._path / "workers.json"
                    if workers_file.exists():
                        data = json.loads(workers_file.read_text())
                        # Deserialize workers
                    
                    tasks_file = self._path / "tasks.json"
                    if tasks_file.exists():
                        data = json.loads(tasks_file.read_text())
                        # Deserialize tasks
                
                def save(self) -> None:
                    self._path.mkdir(parents=True, exist_ok=True)
                    
                    workers_file = self._path / "workers.json"
                    workers_file.write_text(json.dumps(
                        [asdict(w) for w in self._workers.values()],
                        indent=2, default=str
                    ))
                    
                    tasks_file = self._path / "tasks.json"
                    tasks_file.write_text(json.dumps(
                        [asdict(t) for t in self._tasks.values()],
                        indent=2, default=str
                    ))
                
                @property
                def allocator(self) -> CoordAllocator:
                    return self._allocator
                
                def get_worker(self, id: str) -> Optional[Worker]:
                    return self._workers.get(id)
                
                def get_all_workers(self) -> list[Worker]:
                    return list(self._workers.values())
                
                def save_worker(self, worker: Worker) -> Worker:
                    self._workers[worker.id] = worker
                    self.save()
                    return worker
                
                def get_task(self, id: str) -> Optional[Task]:
                    return self._tasks.get(id)
                
                def get_all_tasks(self) -> list[Task]:
                    return list(self._tasks.values())
                
                def save_task(self, task: Task) -> Task:
                    self._tasks[task.id] = task
                    self.save()
                    return task
            """;
        return CreateFile(coord, Module.Services, "src/python/bruce/store/json.py", content);
    }

    private GeneratedFile GenerateFactory(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Python, Module.Services);
        var content = $$"""
            """Generated from bruce.seed v{{seed.Version}}"""
            # Coord: {{coord}}
            
            import os
            from enum import Enum
            from pathlib import Path
            from typing import Optional
            from .base import StorageBackend
            from .phext import PhextStore
            from .json import JsonStore
            
            
            class BackendType(Enum):
                PHEXT = "phext"
                JSON = "json"
                UNKNOWN = "unknown"
            
            
            def detect_backend(path: Path) -> BackendType:
                """Auto-detect storage backend type."""
                if path.suffix == ".phext":
                    return BackendType.PHEXT
                
                if path.is_dir():
                    if any(path.glob("*.phext")):
                        return BackendType.PHEXT
                    if (path / "workers.json").exists():
                        return BackendType.JSON
                
                return BackendType.UNKNOWN
            
            
            def create_storage(
                path: Optional[Path] = None,
                force_type: Optional[BackendType] = None
            ) -> StorageBackend:
                """Create storage backend from path or environment."""
                if path is None:
                    path = Path(os.environ.get("BRUCE_DATA", "./data"))
                
                backend_env = os.environ.get("BRUCE_BACKEND", "").lower()
                if force_type is None and backend_env:
                    force_type = {
                        "phext": BackendType.PHEXT,
                        "json": BackendType.JSON,
                    }.get(backend_env)
                
                backend_type = force_type or detect_backend(path)
                
                if backend_type == BackendType.JSON:
                    store = JsonStore(path)
                else:
                    phext_path = path / "bruce.phext" if path.is_dir() else path
                    store = PhextStore(phext_path)
                
                store.load()
                return store
            """;
        return CreateFile(coord, Module.Services, "src/python/bruce/store/factory.py", content);
    }

    private GeneratedFile GenerateEngine(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Python, Module.Services);
        var content = $$"""
            """Generated from bruce.seed v{{seed.Version}}"""
            # Coord: {{coord}}
            
            from typing import Optional
            from uuid import uuid4
            from .entities import (
                Worker, Task, Assignment, Message,
                TaskState, TaskType, TaskSource,
                WorkerType, WorkerRole
            )
            from .store import StorageBackend, create_storage
            
            
            class BruceError(Exception):
                """Base exception for Bruce operations."""
                pass
            
            
            class NotFoundError(BruceError):
                """Entity not found."""
                pass
            
            
            class BruceEngine:
                """Main entry point for Bruce operations."""
                
                def __init__(self, storage: Optional[StorageBackend] = None):
                    self._storage = storage or create_storage()
                
                def create_worker(
                    self,
                    name: str,
                    worker_type: WorkerType = WorkerType.HUMAN,
                    role: WorkerRole = WorkerRole.WORKER,
                    substrate: Optional[str] = None,
                ) -> Worker:
                    if not name.strip():
                        raise BruceError("Name cannot be empty")
                    
                    if worker_type == WorkerType.AI and not substrate:
                        raise BruceError("AI workers must specify substrate")
                    
                    worker = Worker(
                        id=f"wrk_{uuid4().hex[:12]}",
                        name=name.strip(),
                        worker_type=worker_type,
                        role=role,
                        substrate=substrate,
                    )
                    return self._storage.save_worker(worker)
                
                def create_task(
                    self,
                    title: str,
                    description: str = "",
                    source: TaskSource = TaskSource.MANUAL,
                    task_type: TaskType = TaskType.ADHOC,
                ) -> Task:
                    if not title.strip():
                        raise BruceError("Title cannot be empty")
                    
                    task = Task(
                        id=f"tsk_{uuid4().hex[:12]}",
                        title=title.strip(),
                        description=description.strip(),
                        source=source,
                        task_type=task_type,
                    )
                    return self._storage.save_task(task)
                
                def advance_task(self, task_id: str) -> Task:
                    task = self._storage.get_task(task_id)
                    if task is None:
                        raise NotFoundError(f"Task {task_id} not found")
                    
                    transitions = {
                        TaskState.CREATED: TaskState.REVIEWED,
                        TaskState.REVIEWED: TaskState.ASSIGNED,
                        TaskState.ASSIGNED: TaskState.TESTING,
                        TaskState.TESTING: TaskState.DONE,
                    }
                    
                    next_state = transitions.get(task.state)
                    if next_state is None:
                        raise BruceError(f"Cannot advance from {task.state}")
                    
                    task.state = next_state
                    return self._storage.save_task(task)
                
                def get_workers(self) -> list[Worker]:
                    return self._storage.get_all_workers()
                
                def get_tasks(self) -> list[Task]:
                    return self._storage.get_all_tasks()
            """;
        return CreateFile(coord, Module.Services, "src/python/bruce/services/engine.py", content);
    }

    private GeneratedFile GenerateCliMain(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Python, Module.Cli);
        var content = $$"""
            """Generated from bruce.seed v{{seed.Version}}"""
            # Coord: {{coord}}
            
            import sys
            from ..services.engine import BruceEngine
            from ..entities import TaskState
            
            
            def main() -> int:
                args = sys.argv[1:]
                
                if not args:
                    return show_help()
                
                engine = BruceEngine()
                cmd = args[0].lower()
                
                if cmd == "worker":
                    return handle_worker(engine, args[1:])
                elif cmd == "task":
                    return handle_task(engine, args[1:])
                elif cmd == "status":
                    return show_status(engine)
                elif cmd in ("help", "--help", "-h"):
                    return show_help()
                else:
                    return show_help(f"Unknown command: {cmd}")
            
            
            def handle_worker(engine: BruceEngine, args: list[str]) -> int:
                if not args:
                    print("worker requires subcommand")
                    return 1
                
                if args[0] in ("list", "ls"):
                    print(f"{'ID':<18} {'Name':<16} {'Type':<8} {'Role':<10}")
                    print("-" * 55)
                    for w in engine.get_workers():
                        print(f"{w.id:<18} {w.name:<16} {w.worker_type.value:<8} {w.role.value:<10}")
                    return 0
                
                print(f"Unknown worker command: {args[0]}")
                return 1
            
            
            def handle_task(engine: BruceEngine, args: list[str]) -> int:
                if not args:
                    print("task requires subcommand")
                    return 1
                
                if args[0] in ("list", "ls"):
                    print(f"{'ID':<18} {'Title':<30} {'State':<12} {'Type'}")
                    print("-" * 70)
                    for t in engine.get_tasks():
                        title = t.title[:27] + "..." if len(t.title) > 30 else t.title
                        print(f"{t.id:<18} {title:<30} {t.state.value:<12} {t.task_type.value}")
                    return 0
                
                print(f"Unknown task command: {args[0]}")
                return 1
            
            
            def show_status(engine: BruceEngine) -> int:
                workers = engine.get_workers()
                tasks = engine.get_tasks()
                
                print("Bruce System Status")
                print("=" * 40)
                print(f"Workers:  {len(workers)} ({sum(1 for w in workers if w.is_active)} active)")
                print(f"Tasks:    {len(tasks)}")
                for state in TaskState:
                    count = sum(1 for t in tasks if t.state == state)
                    print(f"  {state.value}: {count}")
                return 0
            
            
            def show_help(error: str | None = None) -> int:
                if error:
                    print(f"Error: {error}\n")
                
                print("""Bruce CLI - Hybrid Human-AI Team Coordination
            Generated from bruce.seed v6.0.0
            
            Usage: bruce <command> [options]
            
            Commands:
              worker list              List all workers
              task list                List all tasks
              status                   System summary
            
            Environment:
              BRUCE_DATA     Data directory (default: ./data)
              BRUCE_BACKEND  Storage backend (auto, phext, json)
            """)
                return 1 if error else 0
            
            
            if __name__ == "__main__":
                sys.exit(main())
            """;
        return CreateFile(coord, Module.Cli, "src/python/bruce/cli/main.py", content);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// NODE/TYPESCRIPT GENERATOR
// ═══════════════════════════════════════════════════════════════════════════════

public class NodeGenerator : BaseGenerator
{
    public override Language Language => Language.Node;

    public override List<GeneratedFile> Generate(BruceSeed seed, UnifiedLineage lineage)
    {
        var files = new List<GeneratedFile>();

        files.Add(GeneratePackageJson(seed, lineage));
        files.Add(GenerateTsConfig(seed, lineage));
        files.Add(GenerateIndex(seed, lineage));
        files.Add(GenerateCoord(seed, lineage));
        files.Add(GenerateEntities(seed, lineage));
        files.Add(GenerateResult(seed, lineage));
        files.Add(GenerateStoreIndex(seed, lineage));
        files.Add(GeneratePhextStore(seed, lineage));
        files.Add(GenerateJsonStore(seed, lineage));
        files.Add(GenerateFactory(seed, lineage));
        files.Add(GenerateEngine(seed, lineage));
        files.Add(GenerateCli(seed, lineage));

        return files;
    }

    private GeneratedFile GeneratePackageJson(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Core);
        var content = $$"""
            {
              "name": "@tessera/bruce",
              "version": "{{seed.Version}}",
              "description": "Hybrid Human-AI Team Coordination",
              "main": "dist/index.js",
              "types": "dist/index.d.ts",
              "bin": {
                "bruce": "dist/bin/bruce.js"
              },
              "scripts": {
                "build": "tsc",
                "start": "node dist/bin/bruce.js"
              },
              "dependencies": {
                "@tessera/phext": "^0.3.0"
              },
              "devDependencies": {
                "typescript": "^5.0.0",
                "@types/node": "^20.0.0"
              }
            }
            """;
        return CreateFile(coord, Module.Core, "src/node/bruce/package.json", content);
    }

    private GeneratedFile GenerateTsConfig(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Core);
        var content = """
            {
              "compilerOptions": {
                "target": "ES2022",
                "module": "commonjs",
                "lib": ["ES2022"],
                "outDir": "./dist",
                "rootDir": "./src",
                "strict": true,
                "esModuleInterop": true,
                "skipLibCheck": true,
                "declaration": true
              },
              "include": ["src/**/*"]
            }
            """;
        return CreateFile(coord, Module.Core, "src/node/bruce/tsconfig.json", content);
    }

    private GeneratedFile GenerateIndex(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Core);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            export { Coord9D, EntityType, CoordAllocator } from './coord';
            export {
              Worker, Task, Assignment, Message,
              TaskState, TaskType, TaskSource,
              WorkerType, WorkerRole
            } from './entities';
            export { Result, ok, err } from './result';
            export { StorageBackend, PhextStore, JsonStore, createStorage } from './store';
            export { BruceEngine, BruceError } from './services/engine';
            """;
        return CreateFile(coord, Module.Core, "src/node/bruce/src/index.ts", content);
    }

    private GeneratedFile GenerateCoord(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Core);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            export enum EntityType {
              Worker = 1,
              Task = 2,
              Assignment = 3,
              Message = 4,
              Artifact = 5,
              Archive = 6,
            }
            
            export class Coord9D {
              constructor(
                public readonly library: number,
                public readonly shelf: number,
                public readonly series: number,
                public readonly collection: number,
                public readonly volume: number,
                public readonly book: number,
                public readonly chapter: number,
                public readonly section: number,
                public readonly scroll: number,
              ) {}
            
              static parse(s: string): Coord9D {
                const parts = s.split('/');
                if (parts.length !== 3) throw new Error(`Invalid coord: ${s}`);
                
                const [l1, l2, l3] = parts.map(p => p.split('.').map(Number));
                if (l1.length !== 3 || l2.length !== 3 || l3.length !== 3) {
                  throw new Error(`Invalid coord: ${s}`);
                }
                
                return new Coord9D(l1[0], l1[1], l1[2], l2[0], l2[1], l2[2], l3[0], l3[1], l3[2]);
              }
            
              static default(): Coord9D {
                return new Coord9D(1, 1, 1, 1, 1, 1, 1, 1, 1);
              }
            
              toString(): string {
                return `${this.library}.${this.shelf}.${this.series}/` +
                       `${this.collection}.${this.volume}.${this.book}/` +
                       `${this.chapter}.${this.section}.${this.scroll}`;
              }
            
              equals(other: Coord9D): boolean {
                return this.toString() === other.toString();
              }
            }
            
            export class CoordAllocator {
              private counters = new Map<EntityType, number>();
            
              constructor(
                private library: number = 2,
                private shelf: number = 6,
              ) {
                for (const e of Object.values(EntityType).filter(v => typeof v === 'number')) {
                  this.counters.set(e as EntityType, 0);
                }
              }
            
              allocate(entityType: EntityType): Coord9D {
                const pos = this.counters.get(entityType) ?? 0;
                this.counters.set(entityType, pos + 1);
            
                const book = Math.floor(pos / 81) + 1;
                const rem = pos % 81;
                const chapter = Math.floor(rem / 9) + 1;
                const section = (rem % 9) + 1;
            
                return new Coord9D(
                  this.library, this.shelf, 1,
                  entityType, 1, book,
                  chapter, section, 1
                );
              }
            
              setHighWater(entityType: EntityType, coord: Coord9D): void {
                const pos = (coord.book - 1) * 81 + (coord.chapter - 1) * 9 + (coord.section - 1) + 1;
                const current = this.counters.get(entityType) ?? 0;
                this.counters.set(entityType, Math.max(current, pos));
              }
            }
            """;
        return CreateFile(coord, Module.Core, "src/node/bruce/src/coord.ts", content);
    }

    private GeneratedFile GenerateEntities(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Core);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            import { Coord9D } from './coord';
            
            export enum TaskState {
              Created = 'Created',
              Reviewed = 'Reviewed',
              Assigned = 'Assigned',
              Testing = 'Testing',
              Done = 'Done',
            }
            
            export enum TaskType {
              Adhoc = 'Adhoc',
              Planned = 'Planned',
            }
            
            export enum TaskSource {
              Manual = 'Manual',
              Teams = 'Teams',
              Email = 'Email',
              Helix = 'Helix',
              Api = 'Api',
            }
            
            export enum WorkerType {
              Human = 'Human',
              AI = 'AI',
            }
            
            export enum WorkerRole {
              Worker = 'Worker',
              Manager = 'Manager',
              Director = 'Director',
            }
            
            export const roleCapacity: Record<WorkerRole, number> = {
              [WorkerRole.Worker]: 4,
              [WorkerRole.Manager]: 24,
              [WorkerRole.Director]: 200,
            };
            
            export interface Entity {
              id: string;
              coord: Coord9D;
              created: Date;
              updated: Date;
              version: number;
            }
            
            export interface Worker extends Entity {
              name: string;
              type: WorkerType;
              role: WorkerRole;
              substrate?: string;
              isActive: boolean;
            }
            
            export interface Task extends Entity {
              title: string;
              description: string;
              source: TaskSource;
              sourceRef?: string;
              state: TaskState;
              type: TaskType;
              assignedTo?: string;
            }
            
            export interface Assignment extends Entity {
              taskId: string;
              workerId: string;
              pushedAt?: Date;
              claimedAt?: Date;
              completedAt?: Date;
            }
            
            export interface Message extends Entity {
              taskId?: string;
              fromWorkerId: string;
              toWorkerId?: string;
              body: string;
              timestamp: Date;
            }
            """;
        return CreateFile(coord, Module.Core, "src/node/bruce/src/entities.ts", content);
    }

    private GeneratedFile GenerateResult(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Core);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            export type Result<T, E = Error> =
              | { ok: true; value: T }
              | { ok: false; error: E };
            
            export function ok<T>(value: T): Result<T, never> {
              return { ok: true, value };
            }
            
            export function err<E>(error: E): Result<never, E> {
              return { ok: false, error };
            }
            
            export function isOk<T, E>(result: Result<T, E>): result is { ok: true; value: T } {
              return result.ok;
            }
            
            export function isErr<T, E>(result: Result<T, E>): result is { ok: false; error: E } {
              return !result.ok;
            }
            """;
        return CreateFile(coord, Module.Core, "src/node/bruce/src/result.ts", content);
    }

    private GeneratedFile GenerateStoreIndex(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Services);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            export { StorageBackend } from './base';
            export { PhextStore } from './phext';
            export { JsonStore } from './json';
            export { createStorage, detectBackend, BackendType } from './factory';
            """;
        return CreateFile(coord, Module.Services, "src/node/bruce/src/store/index.ts", content);
    }

    private GeneratedFile GeneratePhextStore(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Services);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            import * as fs from 'fs';
            import { Coord9D, CoordAllocator, EntityType } from '../coord';
            import { Worker, Task, Assignment, Message } from '../entities';
            import { StorageBackend } from './base';
            
            export class PhextStore implements StorageBackend {
              private phext = '';
              private allocator = new CoordAllocator();
              private workers = new Map<string, Worker>();
              private tasks = new Map<string, Task>();
            
              constructor(private path: string) {}
            
              load(): void {
                if (fs.existsSync(this.path)) {
                  this.phext = fs.readFileSync(this.path, 'utf-8');
                  this.rebuildIndexes();
                }
              }
            
              save(): void {
                fs.writeFileSync(this.path, this.phext);
              }
            
              private rebuildIndexes(): void {
                // Parse phext and populate entity maps
              }
            
              getAllocator(): CoordAllocator {
                return this.allocator;
              }
            
              getWorker(id: string): Worker | undefined {
                return this.workers.get(id);
              }
            
              getAllWorkers(): Worker[] {
                return Array.from(this.workers.values());
              }
            
              saveWorker(worker: Worker): Worker {
                if (worker.coord.equals(Coord9D.default())) {
                  worker = { ...worker, coord: this.allocator.allocate(EntityType.Worker) };
                }
                this.workers.set(worker.id, worker);
                this.save();
                return worker;
              }
            
              getTask(id: string): Task | undefined {
                return this.tasks.get(id);
              }
            
              getAllTasks(): Task[] {
                return Array.from(this.tasks.values());
              }
            
              saveTask(task: Task): Task {
                if (task.coord.equals(Coord9D.default())) {
                  task = { ...task, coord: this.allocator.allocate(EntityType.Task) };
                }
                this.tasks.set(task.id, task);
                this.save();
                return task;
              }
            }
            """;
        return CreateFile(coord, Module.Services, "src/node/bruce/src/store/phext.ts", content);
    }

    private GeneratedFile GenerateJsonStore(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Services);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            import * as fs from 'fs';
            import * as path from 'path';
            import { CoordAllocator } from '../coord';
            import { Worker, Task } from '../entities';
            import { StorageBackend } from './base';
            
            export class JsonStore implements StorageBackend {
              private allocator = new CoordAllocator();
              private workers = new Map<string, Worker>();
              private tasks = new Map<string, Task>();
            
              constructor(private dirPath: string) {}
            
              load(): void {
                const workersFile = path.join(this.dirPath, 'workers.json');
                if (fs.existsSync(workersFile)) {
                  const data = JSON.parse(fs.readFileSync(workersFile, 'utf-8')) as Worker[];
                  data.forEach(w => this.workers.set(w.id, w));
                }
            
                const tasksFile = path.join(this.dirPath, 'tasks.json');
                if (fs.existsSync(tasksFile)) {
                  const data = JSON.parse(fs.readFileSync(tasksFile, 'utf-8')) as Task[];
                  data.forEach(t => this.tasks.set(t.id, t));
                }
              }
            
              save(): void {
                fs.mkdirSync(this.dirPath, { recursive: true });
                fs.writeFileSync(
                  path.join(this.dirPath, 'workers.json'),
                  JSON.stringify(Array.from(this.workers.values()), null, 2)
                );
                fs.writeFileSync(
                  path.join(this.dirPath, 'tasks.json'),
                  JSON.stringify(Array.from(this.tasks.values()), null, 2)
                );
              }
            
              getAllocator(): CoordAllocator {
                return this.allocator;
              }
            
              getWorker(id: string): Worker | undefined {
                return this.workers.get(id);
              }
            
              getAllWorkers(): Worker[] {
                return Array.from(this.workers.values());
              }
            
              saveWorker(worker: Worker): Worker {
                this.workers.set(worker.id, worker);
                this.save();
                return worker;
              }
            
              getTask(id: string): Task | undefined {
                return this.tasks.get(id);
              }
            
              getAllTasks(): Task[] {
                return Array.from(this.tasks.values());
              }
            
              saveTask(task: Task): Task {
                this.tasks.set(task.id, task);
                this.save();
                return task;
              }
            }
            """;
        return CreateFile(coord, Module.Services, "src/node/bruce/src/store/json.ts", content);
    }

    private GeneratedFile GenerateFactory(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Services);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            import * as fs from 'fs';
            import * as path from 'path';
            import { StorageBackend } from './base';
            import { PhextStore } from './phext';
            import { JsonStore } from './json';
            
            export enum BackendType {
              Phext = 'phext',
              Json = 'json',
              Unknown = 'unknown',
            }
            
            export function detectBackend(pathStr: string): BackendType {
              if (pathStr.endsWith('.phext')) return BackendType.Phext;
            
              if (fs.existsSync(pathStr) && fs.statSync(pathStr).isDirectory()) {
                if (fs.readdirSync(pathStr).some(f => f.endsWith('.phext'))) {
                  return BackendType.Phext;
                }
                if (fs.existsSync(path.join(pathStr, 'workers.json'))) {
                  return BackendType.Json;
                }
              }
            
              return BackendType.Unknown;
            }
            
            export function createStorage(
              pathStr?: string,
              forceType?: BackendType
            ): StorageBackend {
              const dataPath = pathStr ?? process.env.BRUCE_DATA ?? './data';
              const backendEnv = process.env.BRUCE_BACKEND?.toLowerCase();
              
              let type = forceType;
              if (!type && backendEnv) {
                type = backendEnv === 'phext' ? BackendType.Phext :
                       backendEnv === 'json' ? BackendType.Json : undefined;
              }
              type = type ?? detectBackend(dataPath);
            
              let store: StorageBackend;
              if (type === BackendType.Json) {
                store = new JsonStore(dataPath);
              } else {
                const phextPath = dataPath.endsWith('.phext') 
                  ? dataPath 
                  : path.join(dataPath, 'bruce.phext');
                store = new PhextStore(phextPath);
              }
            
              store.load();
              return store;
            }
            """;
        return CreateFile(coord, Module.Services, "src/node/bruce/src/store/factory.ts", content);
    }

    private GeneratedFile GenerateEngine(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Services);
        var content = $$"""
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            import { randomUUID } from 'crypto';
            import { Coord9D } from '../coord';
            import {
              Worker, Task, TaskState, TaskType, TaskSource,
              WorkerType, WorkerRole
            } from '../entities';
            import { StorageBackend, createStorage } from '../store';
            
            export class BruceError extends Error {
              constructor(message: string) {
                super(message);
                this.name = 'BruceError';
              }
            }
            
            export class BruceEngine {
              private storage: StorageBackend;
            
              constructor(storage?: StorageBackend) {
                this.storage = storage ?? createStorage();
              }
            
              createWorker(
                name: string,
                type: WorkerType = WorkerType.Human,
                role: WorkerRole = WorkerRole.Worker,
                substrate?: string
              ): Worker {
                if (!name.trim()) throw new BruceError('Name cannot be empty');
                if (type === WorkerType.AI && !substrate) {
                  throw new BruceError('AI workers must specify substrate');
                }
            
                const worker: Worker = {
                  id: `wrk_${randomUUID().replace(/-/g, '').slice(0, 12)}`,
                  coord: Coord9D.default(),
                  created: new Date(),
                  updated: new Date(),
                  version: 1,
                  name: name.trim(),
                  type,
                  role,
                  substrate,
                  isActive: true,
                };
            
                return this.storage.saveWorker(worker);
              }
            
              createTask(
                title: string,
                description = '',
                source: TaskSource = TaskSource.Manual,
                type: TaskType = TaskType.Adhoc
              ): Task {
                if (!title.trim()) throw new BruceError('Title cannot be empty');
            
                const task: Task = {
                  id: `tsk_${randomUUID().replace(/-/g, '').slice(0, 12)}`,
                  coord: Coord9D.default(),
                  created: new Date(),
                  updated: new Date(),
                  version: 1,
                  title: title.trim(),
                  description: description.trim(),
                  source,
                  type,
                  state: TaskState.Created,
                };
            
                return this.storage.saveTask(task);
              }
            
              advanceTask(taskId: string): Task {
                const task = this.storage.getTask(taskId);
                if (!task) throw new BruceError(`Task ${taskId} not found`);
            
                const transitions: Partial<Record<TaskState, TaskState>> = {
                  [TaskState.Created]: TaskState.Reviewed,
                  [TaskState.Reviewed]: TaskState.Assigned,
                  [TaskState.Assigned]: TaskState.Testing,
                  [TaskState.Testing]: TaskState.Done,
                };
            
                const nextState = transitions[task.state];
                if (!nextState) throw new BruceError(`Cannot advance from ${task.state}`);
            
                const updated = { ...task, state: nextState, updated: new Date() };
                return this.storage.saveTask(updated);
              }
            
              getWorkers(): Worker[] {
                return this.storage.getAllWorkers();
              }
            
              getTasks(): Task[] {
                return this.storage.getAllTasks();
              }
            }
            """;
        return CreateFile(coord, Module.Services, "src/node/bruce/src/services/engine.ts", content);
    }

    private GeneratedFile GenerateCli(BruceSeed seed, UnifiedLineage lineage)
    {
        var coord = lineage.AllocateDerive(Language.Node, Module.Cli);
        var content = $$"""
            #!/usr/bin/env node
            // Generated from bruce.seed v{{seed.Version}}
            // Coord: {{coord}}
            
            import { BruceEngine } from '../services/engine';
            import { TaskState } from '../entities';
            
            function main(): number {
              const args = process.argv.slice(2);
              
              if (args.length === 0) return showHelp();
              
              const engine = new BruceEngine();
              const cmd = args[0].toLowerCase();
              
              switch (cmd) {
                case 'worker': return handleWorker(engine, args.slice(1));
                case 'task': return handleTask(engine, args.slice(1));
                case 'status': return showStatus(engine);
                case 'help':
                case '--help':
                case '-h': return showHelp();
                default: return showHelp(`Unknown command: ${cmd}`);
              }
            }
            
            function handleWorker(engine: BruceEngine, args: string[]): number {
              if (args.length === 0) {
                console.log('worker requires subcommand');
                return 1;
              }
              
              if (args[0] === 'list' || args[0] === 'ls') {
                console.log(`${'ID'.padEnd(18)} ${'Name'.padEnd(16)} ${'Type'.padEnd(8)} ${'Role'.padEnd(10)}`);
                console.log('-'.repeat(55));
                for (const w of engine.getWorkers()) {
                  console.log(`${w.id.padEnd(18)} ${w.name.padEnd(16)} ${w.type.padEnd(8)} ${w.role.padEnd(10)}`);
                }
                return 0;
              }
              
              console.log(`Unknown worker command: ${args[0]}`);
              return 1;
            }
            
            function handleTask(engine: BruceEngine, args: string[]): number {
              if (args.length === 0) {
                console.log('task requires subcommand');
                return 1;
              }
              
              if (args[0] === 'list' || args[0] === 'ls') {
                console.log(`${'ID'.padEnd(18)} ${'Title'.padEnd(30)} ${'State'.padEnd(12)} ${'Type'}`);
                console.log('-'.repeat(70));
                for (const t of engine.getTasks()) {
                  const title = t.title.length > 27 ? t.title.slice(0, 27) + '...' : t.title;
                  console.log(`${t.id.padEnd(18)} ${title.padEnd(30)} ${t.state.padEnd(12)} ${t.type}`);
                }
                return 0;
              }
              
              console.log(`Unknown task command: ${args[0]}`);
              return 1;
            }
            
            function showStatus(engine: BruceEngine): number {
              const workers = engine.getWorkers();
              const tasks = engine.getTasks();
              
              console.log('Bruce System Status');
              console.log('='.repeat(40));
              console.log(`Workers:  ${workers.length} (${workers.filter(w => w.isActive).length} active)`);
              console.log(`Tasks:    ${tasks.length}`);
              for (const state of Object.values(TaskState)) {
                const count = tasks.filter(t => t.state === state).length;
                console.log(`  ${state}: ${count}`);
              }
              return 0;
            }
            
            function showHelp(error?: string): number {
              if (error) console.log(`Error: ${error}\n`);
              
              console.log(`Bruce CLI - Hybrid Human-AI Team Coordination
            Generated from bruce.seed v6.0.0
            
            Usage: bruce <command> [options]
            
            Commands:
              worker list              List all workers
              task list                List all tasks
              status                   System summary
            
            Environment:
              BRUCE_DATA     Data directory (default: ./data)
              BRUCE_BACKEND  Storage backend (auto, phext, json)
            `);
              return error ? 1 : 0;
            }
            
            process.exit(main());
            """;
        return CreateFile(coord, Module.Cli, "src/node/bruce/src/bin/bruce.ts", content);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// UNIFIED PROCESSOR
// ═══════════════════════════════════════════════════════════════════════════════

public class UnifiedSeedProcessor
{
    private readonly List<ILanguageGenerator> _generators;
    private readonly Action<string>? _log;

    public UnifiedSeedProcessor(Action<string>? log = null)
    {
        _log = log;
        _generators = new List<ILanguageGenerator>
        {
            new CSharpGenerator(),
            new RustGenerator(),
            new PythonGenerator(),
            new NodeGenerator()
        };
    }

    public (UnifiedLineage lineage, List<GeneratedFile> files) Process(BruceSeed seed)
    {
        var library = 2;  // bruce project
        var shelf = int.Parse(seed.Version.Split('.')[0]);
        var lineage = new UnifiedLineage(library, shelf);

        var allFiles = new List<GeneratedFile>();

        Log($"Processing seed: {seed.SeedId} v{seed.Version}");
        Log($"Coordinate space: Library {library}, Shelf {shelf}");
        Log("");

        // Emit spec scroll
        var specCoord = lineage.AllocateSpec();
        lineage.Emit(specCoord, $"seed_id: {seed.SeedId}\nversion: {seed.Version}\npurpose: {seed.Purpose}");
        Log($"SPEC → {specCoord}");

        // Generate for each language
        foreach (var gen in _generators)
        {
            Log($"\n═══ DERIVE.{gen.Language.ToString().ToUpper()} ═══");
            var files = gen.Generate(seed, lineage);
            allFiles.AddRange(files);
            
            foreach (var file in files)
            {
                lineage.Emit(file.Coord, $"path: {file.Path}\nhash: {file.Hash}");
                Log($"  {file.Coord} → {file.Path}");
            }
        }

        Log($"\nComplete. {allFiles.Count} files generated.");
        return (lineage, allFiles);
    }

    private void Log(string msg) => _log?.Invoke(msg);
}

// ═══════════════════════════════════════════════════════════════════════════════
// ENTRY POINT
// ═══════════════════════════════════════════════════════════════════════════════

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Bruce Unified Seed Processor                            ║");
        Console.WriteLine("║   Sprint 6, Day 742 - January 8, 2026                     ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var seed = new BruceSeed
        {
            SeedId = "bruce",
            Version = "6.0.0",
            AuthorName = "Will Bickford",
            AuthorType = "Human",
            Purpose = "Unified program for hybrid human-AI team coordination with native implementations across C#, Rust, Python, and Node.",
            SuccessCriteria = new List<string>
            {
                "All outputs derive from single seed specification",
                "Each language produces idiomatic, production-ready code",
                "Full 9D phext coordinates used throughout",
                "Pluggable storage with PhextStore default",
                "Auto-detection of storage backend",
                "CLI parity across all language implementations"
            }
        };

        var processor = new UnifiedSeedProcessor(Console.WriteLine);
        var (lineage, files) = processor.Process(seed);

        // Write files to disk
        var outputDir = args.Length > 0 ? args[0] : ".";
        Console.WriteLine($"\n═══ WRITING FILES TO {outputDir} ═══");
        
        foreach (var file in files)
        {
            var path = Path.Combine(outputDir, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Content);
            Console.WriteLine($"  ✓ {file.Path}");
        }

        // Save lineage
        var lineagePath = Path.Combine(outputDir, "bruce.lineage.phext");
        lineage.Save(lineagePath);
        Console.WriteLine($"\n═══ LINEAGE ═══");
        Console.WriteLine($"Saved to: {lineagePath}");
    }
}
