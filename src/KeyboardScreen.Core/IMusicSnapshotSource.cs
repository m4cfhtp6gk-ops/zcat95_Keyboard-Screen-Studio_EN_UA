using System.Threading;
using System.Threading.Tasks;

namespace KeyboardScreen.Core;

public interface IMusicSnapshotSource
{
	ValueTask<MusicSnapshot> ReadAsync(CancellationToken cancellationToken = default(CancellationToken));
}
