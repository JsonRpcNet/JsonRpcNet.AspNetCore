using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace JsonRpcNet.AspNetCore
{
    public class JsonRpcWebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Func<IWebSocketConnection> _connectionFactory;

        public JsonRpcWebSocketMiddleware(RequestDelegate next, Func<IWebSocketConnection> connectionFactory)
        {
            _next = next;
            _connectionFactory = connectionFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                await _next.Invoke(context);
                return;
            }

            //context.Request.Path
            var connectionHandler = _connectionFactory?.Invoke();
            if (connectionHandler == null)
            {
                throw new InvalidOperationException("Could not activate instance of '" + typeof(IWebSocketConnection).Name + "'");
            }
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var netCoreWebsocket = new NetCoreWebSocket(socket, context.RequestAborted);
            await connectionHandler.HandleMessagesAsync(netCoreWebsocket);
        }
    }
}