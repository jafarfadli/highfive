using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SeoulFirstWeek.Procedure
{
    /// <summary>
    /// Something in the scene that happens on cue before a waypoint can be used: a train pulling in and opening its
    /// doors, an ambulance arriving. The director plays it when the `go` step that targets the waypoint begins.
    /// </summary>
    public abstract class SetPiece : MonoBehaviour
    {
        public abstract Task PlayAsync(CancellationToken ct);

        /// <summary>Jumps to the end state (editor play-tests and a step that has already happened).</summary>
        public virtual void Finish() { }
    }
}
