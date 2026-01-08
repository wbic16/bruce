// ===========================================================================================================
// Bruce.Core: StoreFactory
// Factory for creating storage backends based on configuration
// Sprint 4, Day 741
// ===========================================================================================================

using Bruce.Core.Configuration;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;

namespace Bruce.Core.Services;

/// <summary>
/// Factory for creating storage backends.
/// Supports both legacy JSON and new Phext storage.
/// </summary>
public static class StoreFactory
{
    /// <summary>
    /// Creates all store interfaces based on configuration.
    /// Returns a tuple of all store interfaces backed by the same storage instance.
    /// </summary>
    public static (
        ITaskStore TaskStore,
        IWorkerStore WorkerStore,
        IAssignmentStore AssignmentStore,
        IArtifactStore ArtifactStore,
        IMessageStore MessageStore
    ) CreateStores(BruceConfig config, IBruceLogger? logger = null)
    {
        return config.StoreType switch
        {
            StoreType.Json => CreateJsonStores(config, logger),
            StoreType.Phext => CreatePhextStores(config, logger),
            _ => throw new ArgumentException($"Unknown store type: {config.StoreType}")
        };
    }

    private static (
        ITaskStore, IWorkerStore, IAssignmentStore, IArtifactStore, IMessageStore
    ) CreateJsonStores(BruceConfig config, IBruceLogger? logger)
    {
        var store = new JsonStore(config, logger);
        return (store, store, store, store, store);
    }

    private static (
        ITaskStore, IWorkerStore, IAssignmentStore, IArtifactStore, IMessageStore
    ) CreatePhextStores(BruceConfig config, IBruceLogger? logger)
    {
        var store = new PhextStore(config, logger);
        return (store, store, store, store, store);
    }

    /// <summary>
    /// Creates a combined store instance for direct access to all methods.
    /// Prefer this when you need the full store API.
    /// </summary>
    public static object CreateCombinedStore(BruceConfig config, IBruceLogger? logger = null)
    {
        return config.StoreType switch
        {
            StoreType.Json => new JsonStore(config, logger),
            StoreType.Phext => new PhextStore(config, logger),
            _ => throw new ArgumentException($"Unknown store type: {config.StoreType}")
        };
    }

    /// <summary>
    /// Migrates data from JSON store to Phext store.
    /// </summary>
    public static MigrationResult MigrateJsonToPhext(string dataPath, IBruceLogger? logger = null)
    {
        var result = new MigrationResult();
        
        try
        {
            // Load from JSON
            var jsonConfig = new BruceConfig { DataPath = dataPath, StoreType = StoreType.Json };
            var jsonStore = new JsonStore(jsonConfig, logger);

            // Create Phext store (will create new file)
            var phextConfig = new BruceConfig { DataPath = dataPath, StoreType = StoreType.Phext };
            var phextStore = new PhextStore(phextConfig, logger);

            // Migrate workers
            foreach (var worker in ((IStore<Worker>)jsonStore).GetAll())
            {
                ((IStore<Worker>)phextStore).Save(worker);
                result.WorkersMigrated++;
            }

            // Migrate tasks
            foreach (var task in jsonStore.GetAll())
            {
                ((IStore<BruceTask>)phextStore).Save(task);
                result.TasksMigrated++;
            }

            // Migrate assignments
            foreach (var assignment in ((IAssignmentStore)jsonStore).GetActive())
            {
                ((IAssignmentStore)phextStore).Save(assignment);
                result.AssignmentsMigrated++;
            }

            // Migrate artifacts
            foreach (var artifact in ((IStore<Artifact>)jsonStore).GetAll())
            {
                ((IStore<Artifact>)phextStore).Save(artifact);
                result.ArtifactsMigrated++;
            }

            // Migrate messages (recent only to avoid bloat)
            foreach (var message in ((IMessageStore)jsonStore).GetRecent(1000))
            {
                ((IMessageStore)phextStore).Save(message);
                result.MessagesMigrated++;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            logger?.Error("Migration failed", ex);
        }

        return result;
    }
}

/// <summary>
/// Result of a migration operation
/// </summary>
public class MigrationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int WorkersMigrated { get; set; }
    public int TasksMigrated { get; set; }
    public int AssignmentsMigrated { get; set; }
    public int ArtifactsMigrated { get; set; }
    public int MessagesMigrated { get; set; }

    public int TotalMigrated => 
        WorkersMigrated + TasksMigrated + AssignmentsMigrated + 
        ArtifactsMigrated + MessagesMigrated;

    public override string ToString() =>
        Success
            ? $"Migration successful: {TotalMigrated} entities ({WorkersMigrated} workers, {TasksMigrated} tasks, {AssignmentsMigrated} assignments, {ArtifactsMigrated} artifacts, {MessagesMigrated} messages)"
            : $"Migration failed: {Error}";
}
