using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpcNet.AspNetCore
{
    public class NetCoreWebSocket : IWebSocket
    {
        private readonly WebSocket _webSocket;
        private readonly AsyncQueue<(MessageType messageType, ArraySegment<byte> data)> _queue =
            new AsyncQueue<(MessageType messageType, ArraySegment<byte> data)>();

        private readonly CancellationToken _cancellation;
        public NetCoreWebSocket(WebSocket webSocket, CancellationToken cancellation)
        {
            _webSocket = webSocket;
            _cancellation = cancellation;
            Id = Guid.NewGuid().ToString();
            BeginProcessMessages();
        }
        public string Id { get; }
        public IPEndPoint UserEndPoint => null;

        public JsonRpcWebSocketState WebSocketState
        {
            get
            {
                switch (_webSocket.State)
                {
                    case System.Net.WebSockets.WebSocketState.Aborted:
                        return JsonRpcWebSocketState.Closed;
                    case System.Net.WebSockets.WebSocketState.Closed:
                        return JsonRpcWebSocketState.Closed;
                    case System.Net.WebSockets.WebSocketState.CloseReceived:
                        return JsonRpcWebSocketState.Closing;
                    case System.Net.WebSockets.WebSocketState.CloseSent:
                        return JsonRpcWebSocketState.Closing;
                    case System.Net.WebSockets.WebSocketState.Connecting:
                        return JsonRpcWebSocketState.Connecting;
                    case System.Net.WebSockets.WebSocketState.None:
                        return JsonRpcWebSocketState.Closed;
                    case System.Net.WebSockets.WebSocketState.Open:
                        return JsonRpcWebSocketState.Open;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public Task SendAsync(string message)
        {
            return _webSocket.SendAsync(buffer: new ArraySegment<byte>(array: Encoding.UTF8.GetBytes(message),
                    offset: 0, 
                    count: message.Length),
                messageType: WebSocketMessageType.Text,
                endOfMessage: true,
                _cancellation);
        }

        public Task CloseAsync(int code, string reason)
        {
            return _webSocket.CloseAsync((WebSocketCloseStatus)code, reason, _cancellation);
        }

        public Task<(MessageType messageType, ArraySegment<byte> data)> ReceiveAsync()
        {
            return _queue.DequeueAsync(_cancellation);
        }

        
        
        private async void BeginProcessMessages()
        {
            while(_webSocket.State == System.Net.WebSockets.WebSocketState.Open && !_cancellation.IsCancellationRequested)
            {
                var (buffer, type, closeStatusDescription) = await ReceiveMessageAsync().ConfigureAwait(false);

                if (type == WebSocketMessageType.Text)
                {
                    _queue.Enqueue(((MessageType)type, buffer));
                    continue;
                }

                if (type == WebSocketMessageType.Close)
                {
                    _queue.Enqueue(((MessageType)type, new ArraySegment<byte>(Encoding.UTF8.GetBytes(closeStatusDescription))));
                }
            }
        }

        private async
            Task<(ArraySegment<byte> buffer, WebSocketMessageType type, string
                closeStatusDescription)> ReceiveMessageAsync()
        {
            const int bufferSize = 4096;
            var buffer = new byte[bufferSize];
            var offset = 0;
            var free = buffer.Length;
            WebSocketMessageType type = WebSocketMessageType.Close;
            string closeStatusDescription = string.Empty;
            while (!_cancellation.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, free),
                        _cancellation).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", _cancellation);
                    Console.WriteLine(e);
                    return (new ArraySegment<byte>(), WebSocketMessageType.Close, "Socket closed");
                }
               
                 
                offset += result.Count;
                free -= result.Count;
                if (result.EndOfMessage)
                {
                    type = result.MessageType;
                    closeStatusDescription = result.CloseStatusDescription;
                    break;
                }

                if (free == 0)
                {
                    // No free space
                    // Resize the outgoing buffer
                    var newSize = buffer.Length + bufferSize;

                    var newBuffer = new byte[newSize];
                    Array.Copy(buffer, 0, newBuffer, 0, offset);
                    buffer = newBuffer;
                    free = buffer.Length - offset;
                }
            }
            
            return (new ArraySegment<byte>(buffer, 0, offset), type, closeStatusDescription);
        }
    }
}