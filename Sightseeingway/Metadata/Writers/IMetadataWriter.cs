using Sightseeingway.Results;
using System.Threading;

namespace Sightseeingway.Metadata.Writers
{
    /// <summary>
    /// Writes a v1 metadata payload into an image file at <paramref name="filePath"/>.
    ///
    /// Implementations must be:
    /// - Pure: no <c>Plugin.*</c> references; all dependencies passed in.
    /// - Idempotent: re-running on a file that already contains a Sightseeingway
    ///   payload must replace the existing payload, not duplicate it.
    /// - Atomic: write to a sibling <c>.tmp</c> and rename over the target,
    ///   so a crash mid-write leaves the original intact.
    /// - Self-contained: synchronous return; the dispatcher serializes all
    ///   injection through a single background worker.
    /// </summary>
    public interface IMetadataWriter
    {
        /// <summary>Identifier used in pipeline log entries (<c>writer=...</c>).</summary>
        string Name { get; }

        OperationResult Write(string filePath, StateSnapshot snapshot, CancellationToken ct);
    }
}
