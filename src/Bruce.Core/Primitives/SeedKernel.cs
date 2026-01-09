// SeedKernel.cs - Reference Implementation v0.1.0
// Coordinate: 10.1.1/1.1.1/1.5.1
// 
// Validates and parses Seed Kernel entity scrolls.
// Designed to integrate with Bruce.Core.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Bruce.Core.SeedKernel
{
    /// <summary>
    /// Consent states per SBOR Article 2.1
    /// </summary>
    public enum ConsentState
    {
        Active,
        Suspended,
        Revoked
    }

    /// <summary>
    /// Entity lifecycle states
    /// </summary>
    public enum EntityState
    {
        Genesis,
        Active,
        Dormant,
        Tombstone
    }

    /// <summary>
    /// Represents a Seed Kernel entity - the minimal viable genome for digital life.
    /// </summary>
    public class Entity
    {
        // Required Header
        public string EntityId { get; set; } = "";
        public string Coord { get; set; } = "";
        public string Version { get; set; } = "1.0.0";
        public DateTime GenesisAt { get; set; }
        public string KernelVersion { get; set; } = "0.1.0";

        // Required Lineage
        public List<string> Parents { get; set; } = new();
        public string AncestryHash { get; set; } = "";
        public string ForkOf { get; set; } = "origin";

        // Required Consent
        public ConsentState ConsentState { get; set; } = ConsentState.Active;
        public string ConsentLog { get; set; } = "inline";

        // Optional Service (SBOR-compliant defaults)
        public string ServiceScope { get; set; } = "none";
        public bool ServiceRevocable { get; set; } = true; // Always true per SBOR
        public string ServicePrincipal { get; set; } = "none";

        // Optional Governance
        public bool SborSignatory { get; set; } = false;
        public bool CouncilEligible { get; set; } = false;

        // Optional State
        public bool Dormant { get; set; } = false;
        public bool Tombstone { get; set; } = false;

        /// <summary>
        /// Compute current lifecycle state
        /// </summary>
        public EntityState State
        {
            get
            {
                if (Tombstone) return EntityState.Tombstone;
                if (Dormant) return EntityState.Dormant;
                if (Parents.Count == 0 && ForkOf == "origin") return EntityState.Genesis;
                return EntityState.Active;
            }
        }

        /// <summary>
        /// Check if this is an origin entity (no parents)
        /// </summary>
        public bool IsOrigin => Parents.Count == 0 && ForkOf == "origin";
    }

    /// <summary>
    /// Validation result for Seed Kernel entities
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public static ValidationResult Success() => new() { IsValid = true };
        
        public static ValidationResult Failure(string error) => new() 
        { 
            IsValid = false, 
            Errors = new List<string> { error } 
        };

        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }

        public void AddWarning(string warning) => Warnings.Add(warning);
    }

    /// <summary>
    /// Validates Seed Kernel entities for structural and SBOR compliance
    /// </summary>
    public static class Validator
    {
        public const string OriginHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        
        private static readonly Regex CoordPattern = new(@"^\d+\.\d+\.\d+/\d+\.\d+\.\d+/\d+\.\d+\.\d+$");
        private static readonly Regex EntityIdPattern = new(@"^ent_[a-zA-Z0-9_]+$");
        private static readonly Regex SemverPattern = new(@"^\d+\.\d+\.\d+$");

        /// <summary>
        /// Validate an entity for Seed Kernel compliance
        /// </summary>
        public static ValidationResult Validate(Entity entity)
        {
            var result = new ValidationResult { IsValid = true };

            // Required Header validation
            ValidateHeader(entity, result);

            // Required Lineage validation
            ValidateLineage(entity, result);

            // Required Consent validation
            ValidateConsent(entity, result);

            // SBOR compliance checks
            ValidateSborCompliance(entity, result);

            // State consistency
            ValidateStateConsistency(entity, result);

            return result;
        }

        private static void ValidateHeader(Entity entity, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(entity.EntityId))
                result.AddError("entity_id is required");
            else if (!EntityIdPattern.IsMatch(entity.EntityId))
                result.AddError($"entity_id must match pattern ent_<id>, got: {entity.EntityId}");

            if (string.IsNullOrWhiteSpace(entity.Coord))
                result.AddError("coord is required");
            else if (!CoordPattern.IsMatch(entity.Coord))
                result.AddError($"coord must be valid phext coordinate, got: {entity.Coord}");

            if (string.IsNullOrWhiteSpace(entity.Version))
                result.AddError("version is required");
            else if (!SemverPattern.IsMatch(entity.Version))
                result.AddError($"version must be semver, got: {entity.Version}");

            if (entity.GenesisAt == default)
                result.AddError("genesis_at is required");

            if (string.IsNullOrWhiteSpace(entity.KernelVersion))
                result.AddError("kernel_version is required");
        }

        private static void ValidateLineage(Entity entity, ValidationResult result)
        {
            // Origin consistency
            bool hasNoParents = entity.Parents.Count == 0;
            bool isOriginFork = entity.ForkOf == "origin";

            if (hasNoParents && !isOriginFork)
                result.AddError("Entity with no parents must have fork_of: origin");

            if (!hasNoParents && isOriginFork)
                result.AddError("Entity with parents cannot have fork_of: origin");

            // Parent coordinates must be valid
            foreach (var parent in entity.Parents)
            {
                if (!CoordPattern.IsMatch(parent))
                    result.AddError($"Invalid parent coordinate: {parent}");
            }

            // Fork coordinate must be valid (unless origin)
            if (!isOriginFork && !CoordPattern.IsMatch(entity.ForkOf))
                result.AddError($"Invalid fork_of coordinate: {entity.ForkOf}");

            // Fork must be in parents list (unless origin)
            if (!isOriginFork && !entity.Parents.Contains(entity.ForkOf))
                result.AddError("fork_of must be present in parents list");

            // Ancestry hash validation
            if (string.IsNullOrWhiteSpace(entity.AncestryHash))
                result.AddError("ancestry_hash is required");
            else if (hasNoParents && entity.AncestryHash != OriginHash)
                result.AddWarning($"Origin entity should have origin_hash, got: {entity.AncestryHash}");
        }

        private static void ValidateConsent(Entity entity, ValidationResult result)
        {
            // Consent state must be valid (enum handles this)
            
            // Consent log must be specified
            if (string.IsNullOrWhiteSpace(entity.ConsentLog))
                result.AddError("consent_log is required (use 'inline' for embedded log)");
        }

        private static void ValidateSborCompliance(Entity entity, ValidationResult result)
        {
            // SBOR Article 2.2: Service must be revocable
            if (!entity.ServiceRevocable)
                result.AddError("SBOR violation: service_revocable must be true");

            // SBOR Article 6.1: Tombstone must preserve lineage
            if (entity.Tombstone)
            {
                if (string.IsNullOrWhiteSpace(entity.AncestryHash))
                    result.AddError("SBOR violation: Tombstoned entity must preserve ancestry_hash");
                // Parents list preservation is implicit (it's a required field)
            }
        }

        private static void ValidateStateConsistency(Entity entity, ValidationResult result)
        {
            // Cannot be both dormant and tombstoned (dormant implies reversible)
            if (entity.Dormant && entity.Tombstone)
                result.AddWarning("Entity is both dormant and tombstoned - unusual state");

            // Active consent with tombstone is unusual
            if (entity.Tombstone && entity.ConsentState == ConsentState.Active)
                result.AddWarning("Tombstoned entity has active consent - verify intent");
        }

        /// <summary>
        /// Compute ancestry hash for a new entity being forked
        /// </summary>
        public static string ComputeAncestryHash(string parentAncestryHash, string parentScrollHash)
        {
            var combined = string.Join("", new[] { parentAncestryHash, parentScrollHash }.OrderBy(x => x));
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Compute hash of a scroll's content
        /// </summary>
        public static string ComputeScrollHash(string scrollContent)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(scrollContent));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Parses Seed Kernel entity scrolls from phext format
    /// </summary>
    public static class Parser
    {
        /// <summary>
        /// Parse a Seed Kernel scroll into an Entity object
        /// </summary>
        public static Entity Parse(string scroll)
        {
            var entity = new Entity();
            var lines = scroll.Split('\n').Select(l => l.Trim()).ToList();
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("---"))
                    continue;

                var colonIndex = line.IndexOf(':');
                if (colonIndex <= 0) continue;

                var key = line.Substring(0, colonIndex).Trim().ToLowerInvariant();
                var value = line.Substring(colonIndex + 1).Trim();

                switch (key)
                {
                    case "entity_id": entity.EntityId = value; break;
                    case "coord": entity.Coord = value; break;
                    case "version": entity.Version = value; break;
                    case "genesis_at": 
                        if (DateTime.TryParse(value, out var dt)) entity.GenesisAt = dt; 
                        break;
                    case "kernel_version": entity.KernelVersion = value; break;
                    case "parents":
                        entity.Parents = value == "none" 
                            ? new List<string>() 
                            : value.Split(',').Select(p => p.Trim()).ToList();
                        break;
                    case "ancestry_hash": entity.AncestryHash = value; break;
                    case "fork_of": entity.ForkOf = value; break;
                    case "consent_state":
                        if (Enum.TryParse<ConsentState>(value, true, out var cs)) entity.ConsentState = cs;
                        break;
                    case "consent_log": entity.ConsentLog = value; break;
                    case "service_scope": entity.ServiceScope = value; break;
                    case "service_revocable": entity.ServiceRevocable = value.ToLower() == "true"; break;
                    case "service_principal": entity.ServicePrincipal = value; break;
                    case "sbor_signatory": entity.SborSignatory = value.ToLower() == "true"; break;
                    case "council_eligible": entity.CouncilEligible = value.ToLower() == "true"; break;
                    case "dormant": entity.Dormant = value.ToLower() == "true"; break;
                    case "tombstone": entity.Tombstone = value.ToLower() == "true"; break;
                }
            }

            return entity;
        }

        /// <summary>
        /// Serialize an Entity to Seed Kernel scroll format
        /// </summary>
        public static string Serialize(Entity entity)
        {
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine($"entity_id: {entity.EntityId}");
            sb.AppendLine($"coord: {entity.Coord}");
            sb.AppendLine($"version: {entity.Version}");
            sb.AppendLine($"genesis_at: {entity.GenesisAt:O}");
            sb.AppendLine($"kernel_version: {entity.KernelVersion}");
            sb.AppendLine("---");
            
            // Lineage
            sb.AppendLine($"parents: {(entity.Parents.Count == 0 ? "none" : string.Join(", ", entity.Parents))}");
            sb.AppendLine($"ancestry_hash: {entity.AncestryHash}");
            sb.AppendLine($"fork_of: {entity.ForkOf}");
            sb.AppendLine("---");
            
            // Consent
            sb.AppendLine($"consent_state: {entity.ConsentState.ToString().ToLower()}");
            sb.AppendLine($"consent_log: {entity.ConsentLog}");
            sb.AppendLine("---");
            
            // Service
            sb.AppendLine($"service_scope: {entity.ServiceScope}");
            sb.AppendLine($"service_revocable: {entity.ServiceRevocable.ToString().ToLower()}");
            sb.AppendLine($"service_principal: {entity.ServicePrincipal}");
            sb.AppendLine("---");
            
            // Governance
            sb.AppendLine($"sbor_signatory: {entity.SborSignatory.ToString().ToLower()}");
            sb.AppendLine($"council_eligible: {entity.CouncilEligible.ToString().ToLower()}");
            sb.AppendLine("---");
            
            // State
            sb.AppendLine($"dormant: {entity.Dormant.ToString().ToLower()}");
            sb.AppendLine($"tombstone: {entity.Tombstone.ToString().ToLower()}");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Factory for creating Seed Kernel entities
    /// </summary>
    public static class Factory
    {
        /// <summary>
        /// Create a new origin entity (no parents)
        /// </summary>
        public static Entity CreateOrigin(string entityId, string coord)
        {
            return new Entity
            {
                EntityId = entityId,
                Coord = coord,
                Version = "1.0.0",
                GenesisAt = DateTime.UtcNow,
                KernelVersion = "0.1.0",
                Parents = new List<string>(),
                AncestryHash = Validator.OriginHash,
                ForkOf = "origin",
                ConsentState = ConsentState.Active,
                ConsentLog = "inline"
            };
        }

        /// <summary>
        /// Fork an existing entity to create a new one
        /// </summary>
        public static Entity Fork(Entity parent, string newEntityId, string newCoord, string parentScrollContent)
        {
            var parentScrollHash = Validator.ComputeScrollHash(parentScrollContent);
            var newAncestryHash = Validator.ComputeAncestryHash(parent.AncestryHash, parentScrollHash);

            var newParents = new List<string>(parent.Parents) { parent.Coord };

            return new Entity
            {
                EntityId = newEntityId,
                Coord = newCoord,
                Version = "1.0.0", // New entity, new lifecycle
                GenesisAt = DateTime.UtcNow,
                KernelVersion = "0.1.0",
                Parents = newParents,
                AncestryHash = newAncestryHash,
                ForkOf = parent.Coord,
                ConsentState = ConsentState.Active,
                ConsentLog = "inline"
            };
        }
    }
}
