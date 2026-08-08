using System.Threading;
using System.Threading.Tasks;

namespace KeyboardScreen.Core;

public interface ISystemSnapshotSource
{
	ValueTask<SystemSnapshot> ReadAsync(CancellationToken cancellationToken = default(CancellationToken));
}
