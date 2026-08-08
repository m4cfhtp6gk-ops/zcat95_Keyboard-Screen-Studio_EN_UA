using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeyboardScreen.Core;

public interface IDeviceTransport
{
	Task<DevicePushResult> PushAsync(Uri endpoint, RenderedFrame frame, CancellationToken cancellationToken = default(CancellationToken));
}
