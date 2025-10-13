using CFX;
using CFX.Transport;
using CFX.Utilities;

namespace CFXBridge;

public class CFXBridge
{
    private static readonly AmqpCFXEndpoint endpoint = new();

    // track handlers so we can unsubscribe later
    private static readonly List<CFX.Transport.CFXMessageReceivedHandler> _messageHandlers = new();
    private static readonly List<CFX.Transport.ConnectionEventHandler> _connectionHandlers = new();

    public Task<object?> OpenCFXEndpoint(dynamic input)
    {
        // The DLL is shared across all nodes, so this WILL be called multiple times
        if (endpoint.IsOpen) return Task.FromResult<object?>(null);

        AmqpCFXEndpoint.Codec = CFXCodec.raw;
        endpoint.Open((string)input.handle);

        return Task.FromResult<object?>(null);
    }

    public Task<object?> CloseCFXEndpoint(dynamic input)
    {
        // unsubscribe tracked handlers before closing
        foreach (var h in _messageHandlers) endpoint.OnCFXMessageReceived -= h;
        _messageHandlers.Clear();

        foreach (var h in _connectionHandlers) endpoint.OnConnectionEvent -= h;
        _connectionHandlers.Clear();

        if (endpoint.IsOpen) endpoint.Close();
        return Task.FromResult<object?>(null);
    }

    public Task<object?> AddPublishChannel(dynamic input)
    {
        if (!endpoint.IsOpen) throw new Exception("Endpoint is not open. Please open the endpoint before adding publish channels.");

        var brokerUri = new Uri((string)input.brokerUri);
        var amqpTarget = (string)input.amqpTarget;

        Exception testError;
        bool ok = endpoint.TestPublishChannel(brokerUri, amqpTarget, out testError);

        if (!ok)
        {
            throw new Exception("Publish channel test failed. Double check the broker URI and AMQP target.", testError);
        }

        endpoint.AddPublishChannel(brokerUri, amqpTarget);

        return Task.FromResult<object?>(null);
    }

    public Task<object?> AddSubscribeChannel(dynamic input)
    {
        if (!endpoint.IsOpen) throw new Exception("Endpoint is not open. Please open the endpoint before adding subscribe channels.");

        var brokerUri = new Uri((string)input.brokerUri);
        var sourceQueue = (string)input.sourceQueue;

        Exception testError;
        bool ok = endpoint.TestSubscribeChannel(brokerUri, sourceQueue, out testError);

        if (!ok)
        {
            throw new Exception("Subscribe channel test failed. Double check the broker URI and source queue name.", testError);
        }

        endpoint.AddSubscribeChannel(brokerUri, sourceQueue);

        return Task.FromResult<object?>(null);
    }

    public Task<object?> PublishMessage(dynamic input)
    {
        if (!endpoint.IsOpen) throw new Exception("Endpoint is not open. Please open the endpoint before publishing messages.");

        var payload = (string)input.dataJSON;
        var message = CFXJsonSerializer.DeserializeObject<CFXMessage>(payload);

        endpoint.Publish(new CFXEnvelope(message));

        return Task.FromResult<object?>(null);
    }

    public Task<object?> RegisterListenerCallback(dynamic input)
    {
        if (!endpoint.IsOpen) throw new Exception("Endpoint is not open. Please open the endpoint before registering a subscription callback.");

        var callback = (Func<object, Task<object>>)input.callback;

        CFXMessageReceivedHandler handler = async (AmqpChannelAddress source, CFXEnvelope message) =>
        {
            try
            {
                await callback(message);
            }
            catch
            {
                // ignore callback errors
            }
        };

        endpoint.OnCFXMessageReceived += handler;
        _messageHandlers.Add(handler);

        return Task.FromResult<object?>(null);
    }

    public Task<object?> RegisterConnectionEventCallback(dynamic input)
    {
        var callback = (Func<object, Task<object>>)input.callback;

        ConnectionEventHandler handler = async (ConnectionEvent eventType, Uri uri, int spoolSize, string errorMessage, Exception error) =>
        {
            try
            {
                await callback(eventType.ToString());
            }
            catch
            {
                // swallow callback errors
            }
        };

        endpoint.OnConnectionEvent += handler;
        _connectionHandlers.Add(handler);

        return Task.FromResult<object?>(null);
    }
}