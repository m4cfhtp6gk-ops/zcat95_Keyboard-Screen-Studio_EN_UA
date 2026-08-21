using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace KeyboardScreen.Core;

public sealed class HttpImageDeviceTransport : IDeviceTransport, IDisposable
{
	private readonly HttpClient _client;

	private readonly bool _ownsClient;

	public HttpImageDeviceTransport(HttpClient? client = null)
	{
		_ownsClient = client == null;
		_client = client ?? new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(8.0)
		};
	}

	public async Task<DevicePushResult> PushAsync(Uri endpoint, RenderedFrame frame, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(endpoint, "endpoint");
		ArgumentNullException.ThrowIfNull(frame, "frame");
		Stopwatch timer = Stopwatch.StartNew();
		try
		{
			using ByteArrayContent content = new ByteArrayContent(frame.JpegBytes);
			content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
			using HttpResponseMessage httpResponseMessage = await _client.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			timer.Stop();
			return new DevicePushResult(httpResponseMessage.IsSuccessStatusCode, (int)httpResponseMessage.StatusCode, httpResponseMessage.IsSuccessStatusCode ? Loc.T("DevicePushSucceeded") : Loc.T("DevicePushDeviceReturned", httpResponseMessage.StatusCode, httpResponseMessage.ReasonPhrase), timer.Elapsed);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			timer.Stop();
			return new DevicePushResult(Success: false, null, Loc.T("DeviceConnectTimeout"), timer.Elapsed);
		}
		catch (HttpRequestException ex2)
		{
			timer.Stop();
			return new DevicePushResult(Success: false, null, Loc.T("DeviceConnectFailed", ex2.Message), timer.Elapsed);
		}
	}

	public void Dispose()
	{
		if (_ownsClient)
		{
			_client.Dispose();
		}
	}
}
