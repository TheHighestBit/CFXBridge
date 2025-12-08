using CFX;
using CFX.Transport;
using CFX.Utilities;

namespace CFXBridge;

public class CFXBridge
{
    // Maps the handles to endpoints
    private static readonly Dictionary<string, AmqpCFXEndpoint> _endpointMap = new();

    // Maps handle to endpoint message event handler
    private static readonly Dictionary<string, CFXMessageReceivedHandler> _messageHandlers = new();
    
    // Each endpoint can only have a single connection event handler
    private static readonly Dictionary<string, ConnectionEventHandler> _connectionHandlers = new();

    public Task<object?> OpenCFXEndpoint(dynamic input)
    {
        string endpointHandle = (string)input.handle;

        if (_endpointMap.ContainsKey(endpointHandle)) throw new Exception($"An endpoint with the handle '{endpointHandle}' is already open.");
        
        AmqpCFXEndpoint endpoint = new AmqpCFXEndpoint();
        AmqpCFXEndpoint.Codec = CFXCodec.raw; // So the message body is human readable and not compressed binary

        endpoint.Open(endpointHandle);
        _endpointMap[endpointHandle] = endpoint;

        return Task.FromResult<object?>(null);
    }

    public Task<object?> CloseCFXEndpoint(dynamic input)
    {
        string endpointHandle = (string)input.handle;

        if (!_endpointMap.ContainsKey(endpointHandle)) throw new Exception($"No endpoint with the handle '{endpointHandle}' is currently open.");
        AmqpCFXEndpoint endpoint = _endpointMap[endpointHandle];

        // Remove the message event handler if it exists
        if (_messageHandlers.ContainsKey(endpointHandle))
        {
            var msgHandler = _messageHandlers[endpointHandle];
            endpoint.OnCFXMessageReceived -= msgHandler;
            _messageHandlers.Remove(endpointHandle);
        }

        // Unsubscribe connection event handler if exists
        if (_connectionHandlers.ContainsKey(endpointHandle))
        {
            var connHandler = _connectionHandlers[endpointHandle];
            endpoint.OnConnectionEvent -= connHandler;
            _connectionHandlers.Remove(endpointHandle);
        }

        endpoint.Close();
        _endpointMap.Remove(endpointHandle);

        return Task.FromResult<object?>(null);
    }

    public Task<object?> AddPublishChannel(dynamic input)
    {
        string endpointHandle = (string)input.handle;
        AmqpCFXEndpoint? endpoint;
        if (!_endpointMap.TryGetValue(endpointHandle, out endpoint))
        {
            throw new Exception($"No endpoint with the handle '{endpointHandle}' is currently open.");
        }
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
        string endpointHandle = (string)input.handle;
        AmqpCFXEndpoint? endpoint;
        if (!_endpointMap.TryGetValue(endpointHandle, out endpoint))
        {
            throw new Exception($"No endpoint with the handle '{endpointHandle}' is currently open.");
        }
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
        string endpointHandle = (string)input.handle;
        AmqpCFXEndpoint? endpoint;
        if (!_endpointMap.TryGetValue(endpointHandle, out endpoint))
        {
            throw new Exception($"No endpoint with the handle '{endpointHandle}' is currently open.");
        }
        if (!endpoint.IsOpen) throw new Exception("Endpoint is not open. Please open the endpoint before publishing messages.");

        var payload = (string)input.dataJSON;
        var message = CFXJsonSerializer.DeserializeObject<CFXMessage>(payload);

        endpoint.PublishToChannel(new CFXEnvelope(message), new AmqpChannelAddress { Uri = new Uri((string)input.brokerUri), Address = (string)input.amqpTarget });

        return Task.FromResult<object?>(null);
    }

    public Task<object?> RegisterListenerCallback(dynamic input)
    {
        string endpointHandle = (string)input.handle;
        AmqpCFXEndpoint? endpoint;
        if (!_endpointMap.TryGetValue(endpointHandle, out endpoint))
        {
            throw new Exception($"No endpoint with the handle '{endpointHandle}' is currently open.");
        }
        else if (_messageHandlers.ContainsKey(endpointHandle))
        {
            throw new Exception("A message handler is already registered for this endpoint.");
        }
        if (!endpoint.IsOpen) throw new Exception("Endpoint is not open. Please open the endpoint before registering a subscription callback.");

        var callback = (Func<object, Task<object>>)input.callback;

        CFXMessageReceivedHandler handler = async (AmqpChannelAddress source, CFXEnvelope message) =>
        {
            try
            {   
                string jsonMessage = message.ToJson();
                await callback(jsonMessage);
            }
            catch
            {
                // ignore callback errors
            }
        };

        endpoint.OnCFXMessageReceived += handler;
        _messageHandlers[endpointHandle] = handler;

        return Task.FromResult<object?>(null);
    }

    public Task<object?> UnregisterListenerCallback(dynamic input)
    {
        string endpointHandle = (string)input.handle;
        AmqpCFXEndpoint? endpoint;
        if (!_endpointMap.TryGetValue(endpointHandle, out endpoint))
        {
            throw new Exception($"No endpoint with the handle '{endpointHandle}' is currently open.");
        }
        else if (!_messageHandlers.ContainsKey(endpointHandle))
        {
            throw new Exception("No message handler is registered for this endpoint.");
        }

        var handler = _messageHandlers[endpointHandle];
        endpoint.OnCFXMessageReceived -= handler;
        _messageHandlers.Remove(endpointHandle);

        return Task.FromResult<object?>(null);
    }

    public Task<object?> RegisterConnectionEventCallback(dynamic input)
    {   
        string endpointHandle = (string)input.handle;
        AmqpCFXEndpoint? endpoint;
        if (!_endpointMap.TryGetValue(endpointHandle, out endpoint))
        {
            throw new Exception($"No endpoint with the handle '{endpointHandle}' is currently open.");
        }
        else if (_connectionHandlers.ContainsKey(endpointHandle))
        {
            throw new Exception("A connection event handler is already registered for this endpoint.");
        }
        
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
        _connectionHandlers[endpointHandle] = handler;

        return Task.FromResult<object?>(null);
    }
}