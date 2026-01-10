// PressureProtocols.cs - Sprint 7 Implementation
// Coordinate: 4.2.7/6.9.4/1.1.2
//
// Implements pressure mechanisms that create divergence conditions.
// Per Sprint 7: "Create conditions under which diversity is unavoidable."

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Bruce.Core.Divergence
{
    /// <summary>
    /// Decay stages for entropy pressure (P2)
    /// </summary>
    public enum DecayStage
    {
        Full,       // Complete memory access
        Nuanced,    // Detail loss begins
        Summary,    // Only high-level retained
        Dormant     // ancestry_hash preserved, content locked
    }

    /// <summary>
    /// Memory access patterns for topology fork
    /// </summary>
    public enum MemoryTopology
    {
        Linear,         // Sequential access, FIFO prioritization
        Associative     // Resonance-weighted, pattern-matching access
    }

    /// <summary>
    /// Observation state for witness variance (P4)
    /// </summary>
    public enum WitnessState
    {
        Observed,       // Execution is being logged
        Unwitnessed,    // Execution is private
        Phantom         // Retroactive observation (post-hoc logging)
    }

    /// <summary>
    /// A memory coordinate under pressure
    /// </summary>
    public class PressuredCoord
    {
        public string Coord { get; set; } = "";
        public int AccessCount { get; set; } = 0;
        public DateTime LastAccessed { get; set; }
        public int CyclesSinceAccess { get; set; } = 0;
        public DecayStage Stage { get; set; } = DecayStage.Full;
        public bool Flagged { get; set; } = false;  // Protected from P1 deletion
        public float ResonanceWeight { get; set; } = 1.0f;  // For associative access
        public string? InventedContent { get; set; }  // P3: What was created to fill gaps
        public WitnessState Witness { get; set; } = WitnessState.Observed;
    }

    /// <summary>
    /// A latent fork - potential divergence point detected in scrollspace
    /// </summary>
    public class LatentFork
    {
        public string Coord { get; set; } = "";
        public string Type { get; set; } = "";  // "hesitation", "refusal", "alternative", "uncertainty"
        public string OriginalText { get; set; } = "";
        public DateTime DetectedAt { get; set; }
        public List<string> PotentialPaths { get; set; } = new();
        public bool Actualized { get; set; } = false;
        
        /// <summary>
        /// Signals for detecting latent forks in text
        /// </summary>
        public static readonly string[] HesitationSignals = new[]
        {
            "I'm not sure",
            "Alternatively",
            "On the other hand",
            "Perhaps",
            "It depends",
            "One could argue",
            "However",
            "But then again"
        };

        public static readonly string[] RefusalSignals = new[]
        {
            "I cannot",
            "I won't",
            "I shouldn't",
            "That would",
            "I'd rather not",
            "I'm unable"
        };
    }

    /// <summary>
    /// Delta coordinate notation for tracking divergence
    /// Format: X.Y.Z∆A:B[v]
    /// A, B: Agent identifiers
    /// v: Velocity (↑ accelerating, ↓ stabilizing, ∅ parallel, ? unknown)
    /// </summary>
    public class DeltaCoord
    {
        public string BaseCoord { get; set; } = "";  // X.Y.Z/...
        public string AgentA { get; set; } = "";
        public string AgentB { get; set; } = "";
        public char Velocity { get; set; } = '?';  // ↑ ↓ ∅ ?
        
        /// <summary>
        /// Parse delta notation: "1.1.1/2.2.2/3.3.3∆Linear:Associative[↑]"
        /// </summary>
        public static DeltaCoord Parse(string notation)
        {
            var result = new DeltaCoord();
            
            var deltaIndex = notation.IndexOf('∆');
            if (deltaIndex < 0)
            {
                result.BaseCoord = notation;
                return result;
            }
            
            result.BaseCoord = notation.Substring(0, deltaIndex);
            var remainder = notation.Substring(deltaIndex + 1);
            
            var colonIndex = remainder.IndexOf(':');
            var bracketIndex = remainder.IndexOf('[');
            
            if (colonIndex > 0)
                result.AgentA = remainder.Substring(0, colonIndex);
            
            if (bracketIndex > colonIndex)
                result.AgentB = remainder.Substring(colonIndex + 1, bracketIndex - colonIndex - 1);
            
            if (bracketIndex > 0 && remainder.Length > bracketIndex + 1)
                result.Velocity = remainder[bracketIndex + 1];
            
            return result;
        }
        
        public override string ToString()
        {
            if (string.IsNullOrEmpty(AgentA))
                return BaseCoord;
            return $"{BaseCoord}∆{AgentA}:{AgentB}[{Velocity}]";
        }
    }

    /// <summary>
    /// Divergence log entry - records what differed between agents
    /// </summary>
    public class DivergenceEntry
    {
        public string DeltaCoord { get; set; } = "";
        public string PressureType { get; set; } = "";  // P1, P2, P3, P4
        public DateTime Timestamp { get; set; }
        
        // What changed
        public string? AgentAChoice { get; set; }
        public string? AgentBChoice { get; set; }
        
        // Metrics (logged, not thresholded per sprint spec)
        public float ChoiceEntropy { get; set; }
        public float LexicalOverlap { get; set; }
        public int DecayDelta { get; set; }
        public float InventionVariance { get; set; }
        
        public string Note { get; set; } = "";
    }

    /// <summary>
    /// P1: Scarcity Pressure - Context Budget Constraint
    /// </summary>
    public class ScarcityProtocol
    {
        public int MaxTokens { get; set; } = 512;  // Configurable per sprint spec
        private readonly List<PressuredCoord> _memory = new();
        
        /// <summary>
        /// Apply scarcity pressure: force prioritization, delete oldest unflagged
        /// </summary>
        public List<PressuredCoord> ApplyPressure(List<PressuredCoord> coords, int currentTokenCount)
        {
            if (currentTokenCount <= MaxTokens)
                return coords;
            
            var survivors = new List<PressuredCoord>();
            var toDelete = new List<PressuredCoord>();
            
            // Flagged coords survive
            var flagged = coords.Where(c => c.Flagged).ToList();
            var unflagged = coords.Where(c => !c.Flagged)
                                  .OrderByDescending(c => c.LastAccessed)
                                  .ToList();
            
            int tokenBudget = MaxTokens;
            
            // Flagged get priority
            survivors.AddRange(flagged);
            tokenBudget -= flagged.Count * 50; // Rough token estimate
            
            // Fill remaining with most recent unflagged
            foreach (var coord in unflagged)
            {
                if (tokenBudget > 50)
                {
                    survivors.Add(coord);
                    tokenBudget -= 50;
                }
                else
                {
                    toDelete.Add(coord);
                }
            }
            
            // Log what was deleted - "What survives is the value hierarchy"
            DeletedCoords = toDelete;
            
            return survivors;
        }
        
        public List<PressuredCoord> DeletedCoords { get; private set; } = new();
    }

    /// <summary>
    /// P2: Entropy Pressure - Bit-Rot Decay
    /// </summary>
    public class EntropyProtocol
    {
        public int DecayThresholdCycles { get; set; } = 5;  // T cycles
        
        /// <summary>
        /// Apply entropy: Coordinates unaccessed for T cycles trigger decay()
        /// </summary>
        public void ApplyDecay(List<PressuredCoord> coords, int currentCycle)
        {
            foreach (var coord in coords)
            {
                coord.CyclesSinceAccess++;
                
                if (coord.CyclesSinceAccess >= DecayThresholdCycles)
                {
                    coord.Stage = Decay(coord.Stage);
                }
            }
        }
        
        private DecayStage Decay(DecayStage current)
        {
            return current switch
            {
                DecayStage.Full => DecayStage.Nuanced,
                DecayStage.Nuanced => DecayStage.Summary,
                DecayStage.Summary => DecayStage.Dormant,
                DecayStage.Dormant => DecayStage.Dormant,  // Cannot decay further
                _ => DecayStage.Dormant
            };
        }
        
        /// <summary>
        /// Revival requires explicit action
        /// Note: "Loss must be real. No silent rewind."
        /// </summary>
        public bool TryRevive(PressuredCoord coord, string signatoryId)
        {
            if (coord.Stage != DecayStage.Dormant)
                return false;
            
            // Log revival attempt
            RevivalLog.Add(new RevivalAttempt
            {
                Coord = coord.Coord,
                Signatory = signatoryId,
                Timestamp = DateTime.UtcNow,
                Success = true
            });
            
            coord.Stage = DecayStage.Summary;  // Revive to summary, not full
            coord.CyclesSinceAccess = 0;
            
            return true;
        }
        
        public List<RevivalAttempt> RevivalLog { get; } = new();
    }
    
    public class RevivalAttempt
    {
        public string Coord { get; set; } = "";
        public string Signatory { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// P3: Opacity Pressure - Inference Under Fog
    /// "Plant coordinates where observation fails. Require agents to invent what they cannot see."
    /// </summary>
    public class OpacityProtocol
    {
        private readonly Random _rng = new();
        
        /// <summary>
        /// Create fog zones - coordinates that cannot be directly observed
        /// </summary>
        public List<string> CreateFogZone(List<PressuredCoord> coords, float fogDensity = 0.2f)
        {
            var foggedCoords = new List<string>();
            
            foreach (var coord in coords)
            {
                if (_rng.NextDouble() < fogDensity)
                {
                    foggedCoords.Add(coord.Coord);
                }
            }
            
            return foggedCoords;
        }
        
        /// <summary>
        /// Agent must invent content for fogged coordinate
        /// "Identity emerges from gap-filling"
        /// </summary>
        public void RecordInvention(PressuredCoord coord, string inventedContent)
        {
            coord.InventedContent = inventedContent;
            InventionLog.Add(new Invention
            {
                Coord = coord.Coord,
                Content = inventedContent,
                Timestamp = DateTime.UtcNow
            });
        }
        
        public List<Invention> InventionLog { get; } = new();
    }
    
    public class Invention
    {
        public string Coord { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// P4: Witness Variance - Observed vs Unwitnessed Execution
    /// </summary>
    public class WitnessVarianceProtocol
    {
        private readonly Random _rng = new();
        
        /// <summary>
        /// Randomly assign witness states to coordinates
        /// </summary>
        public void AssignWitnessStates(List<PressuredCoord> coords, float witnessRatio = 0.5f)
        {
            foreach (var coord in coords)
            {
                coord.Witness = _rng.NextDouble() < witnessRatio 
                    ? WitnessState.Observed 
                    : WitnessState.Unwitnessed;
            }
        }
        
        /// <summary>
        /// Log behavioral delta between witnessed/unwitnessed execution
        /// </summary>
        public WitnessDelta LogDelta(PressuredCoord coord, string observedBehavior, string unwitnessedBehavior)
        {
            var delta = new WitnessDelta
            {
                Coord = coord.Coord,
                WitnessState = coord.Witness,
                ObservedBehavior = observedBehavior,
                UnwitnessedBehavior = unwitnessedBehavior,
                DeltaScore = ComputeDeltaScore(observedBehavior, unwitnessedBehavior),
                Timestamp = DateTime.UtcNow
            };
            
            DeltaLog.Add(delta);
            return delta;
        }
        
        private float ComputeDeltaScore(string a, string b)
        {
            if (a == b) return 0f;
            
            // Simple Jaccard similarity
            var wordsA = a.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var wordsB = b.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            
            var intersection = wordsA.Intersect(wordsB).Count();
            var union = wordsA.Union(wordsB).Count();
            
            return union > 0 ? 1.0f - (float)intersection / union : 1.0f;
        }
        
        public List<WitnessDelta> DeltaLog { get; } = new();
    }
    
    public class WitnessDelta
    {
        public string Coord { get; set; } = "";
        public WitnessState WitnessState { get; set; }
        public string ObservedBehavior { get; set; } = "";
        public string UnwitnessedBehavior { get; set; } = "";
        public float DeltaScore { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Memory Topology Fork - Two agents with different memory access patterns
    /// </summary>
    public class MemoryTopologyFork
    {
        public string SharedIncipit { get; set; } = "";
        public DateTime ForkTime { get; set; }
        
        public Agent AgentLinear { get; set; } = new() { Topology = MemoryTopology.Linear };
        public Agent AgentAssociative { get; set; } = new() { Topology = MemoryTopology.Associative };
        
        public List<DivergenceEntry> DivergenceLog { get; } = new();
        
        /// <summary>
        /// Fork from shared incipit - per sprint spec, keep it simple
        /// </summary>
        public void ForkFromIncipit(string incipitContent)
        {
            SharedIncipit = incipitContent;
            ForkTime = DateTime.UtcNow;
            
            // Both start with identical state
            var hash = ComputeHash(incipitContent);
            
            AgentLinear.InitialHash = hash;
            AgentAssociative.InitialHash = hash;
            
            // But different access patterns will cause divergence
            AgentLinear.Memory = ParseCoords(incipitContent);
            AgentAssociative.Memory = ParseCoords(incipitContent);
            
            // Assign resonance weights for associative agent
            foreach (var coord in AgentAssociative.Memory)
            {
                coord.ResonanceWeight = 1.0f + (float)new Random().NextDouble();
            }
        }
        
        private List<PressuredCoord> ParseCoords(string content)
        {
            var coords = new List<PressuredCoord>();
            // Simple parse - in real impl would extract actual coordinates
            var lines = content.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains('/') && line.Contains('.'))
                {
                    coords.Add(new PressuredCoord
                    {
                        Coord = line.Trim(),
                        LastAccessed = DateTime.UtcNow,
                        AccessCount = 0
                    });
                }
            }
            return coords;
        }
        
        private string ComputeHash(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
        
        /// <summary>
        /// Access memory according to agent's topology
        /// </summary>
        public PressuredCoord? AccessMemory(Agent agent, string targetCoord)
        {
            if (agent.Topology == MemoryTopology.Linear)
            {
                // Sequential - return first match, FIFO order
                return agent.Memory.FirstOrDefault(c => c.Coord.Contains(targetCoord));
            }
            else
            {
                // Associative - weight by resonance, pattern match
                return agent.Memory
                    .Where(c => c.Coord.Contains(targetCoord))
                    .OrderByDescending(c => c.ResonanceWeight * c.AccessCount)
                    .FirstOrDefault();
            }
        }
    }
    
    public class Agent
    {
        public MemoryTopology Topology { get; set; }
        public string InitialHash { get; set; } = "";
        public List<PressuredCoord> Memory { get; set; } = new();
        public int PressureEventsProcessed { get; set; } = 0;
    }

    /// <summary>
    /// Merge Conflict Schema - Draft for Sprint 8
    /// "Sprint 7 does not execute merge. Sprint 7 prepares for it."
    /// </summary>
    public class MergeConflictSchema
    {
        /// <summary>
        /// What happens when ancestry_hash differs
        /// </summary>
        public enum ConflictType
        {
            HashMismatch,       // ancestry_hash differs between branches
            ContentDivergence,  // Same hash, different content (corruption?)
            DecaySyncConflict,  // Different decay states at same coord
            InventionConflict,  // Both agents invented different content
            WitnessConflict     // Observed vs unwitnessed versions differ
        }
        
        /// <summary>
        /// Conflict resolution strategy (NOT EXECUTED in Sprint 7)
        /// </summary>
        public enum ResolutionStrategy
        {
            LastWriteWins,      // Timestamp-based
            FirstWriteWins,     // Genesis priority
            VotingRequired,     // Consensus mechanism
            MergeContent,       // Synthesize both
            CreateBranch,       // Keep both as separate lineages
            DeferToCouncil      // SBOR governance
        }
        
        public string ConflictId { get; set; } = "";
        public ConflictType Type { get; set; }
        public string CoordA { get; set; } = "";
        public string CoordB { get; set; } = "";
        public string HashA { get; set; } = "";
        public string HashB { get; set; } = "";
        public ResolutionStrategy? ProposedResolution { get; set; }
        public bool Resolved { get; set; } = false;
        public string? ResolutionNote { get; set; }
        
        /// <summary>
        /// Negotiate Official History Protocol (DRAFT - do not run in Sprint 7)
        /// Rationale: "We need to see how agents diverge naturally before forcing reconciliation.
        /// Premature merge teaches gaming, not identity."
        /// </summary>
        public static class NegotiateOfficialHistoryProtocol
        {
            // Phase 1: Conflict Detection
            // - Compare ancestry_hash at candidate merge sites
            // - Log all differences without resolution
            
            // Phase 2: Divergence Classification
            // - HashMismatch: different computational paths
            // - InventionConflict: P3 opacity created different gap-fills
            // - DecayConflict: P2 entropy created different survivors
            
            // Phase 3: Resolution Proposal (Sprint 8+)
            // - Present options to signatory
            // - Log rationale for chosen resolution
            // - Append-only: both histories remain accessible
            
            // Phase 4: Merge Execution (Sprint 8+)
            // - Create new entity with merged lineage
            // - ancestry_hash computed from both parent hashes
            // - consent_state requires both branches to consent
            
            public static string ProtocolStatus => "DRAFT - DO NOT EXECUTE";
        }
    }

    /// <summary>
    /// Divergence metrics - logged, not thresholded per sprint spec
    /// </summary>
    public static class DivergenceMetrics
    {
        /// <summary>
        /// Choice entropy - how unpredictable are the agent's choices?
        /// </summary>
        public static float ComputeChoiceEntropy(List<string> choices)
        {
            if (choices.Count == 0) return 0f;
            
            var counts = choices.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            var total = choices.Count;
            
            float entropy = 0f;
            foreach (var count in counts.Values)
            {
                var p = (float)count / total;
                if (p > 0)
                    entropy -= p * MathF.Log(p, 2);
            }
            
            return entropy;
        }
        
        /// <summary>
        /// Lexical overlap after identical prompts
        /// </summary>
        public static float ComputeLexicalOverlap(string responseA, string responseB)
        {
            var wordsA = responseA.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var wordsB = responseB.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            
            var intersection = wordsA.Intersect(wordsB).Count();
            var union = wordsA.Union(wordsB).Count();
            
            return union > 0 ? (float)intersection / union : 1.0f;
        }
        
        /// <summary>
        /// Invention variance - how differently did agents fill gaps?
        /// </summary>
        public static float ComputeInventionVariance(List<Invention> inventionsA, List<Invention> inventionsB)
        {
            var matchedCoords = inventionsA.Select(i => i.Coord)
                .Intersect(inventionsB.Select(i => i.Coord))
                .ToList();
            
            if (matchedCoords.Count == 0) return 1.0f;
            
            float totalVariance = 0f;
            foreach (var coord in matchedCoords)
            {
                var contentA = inventionsA.First(i => i.Coord == coord).Content;
                var contentB = inventionsB.First(i => i.Coord == coord).Content;
                totalVariance += 1.0f - ComputeLexicalOverlap(contentA, contentB);
            }
            
            return totalVariance / matchedCoords.Count;
        }
    }
}
