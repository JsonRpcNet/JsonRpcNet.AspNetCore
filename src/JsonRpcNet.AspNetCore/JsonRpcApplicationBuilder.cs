using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using JsonRpcNet.Attributes;
using JsonRpcNet.Docs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;

namespace JsonRpcNet.AspNetCore
{
    public static class JsonRpcApplicationBuilder
    {
        public static IApplicationBuilder AddJsonRpcService<TJsonRpcWebSocketService>(this IApplicationBuilder app)
            where TJsonRpcWebSocketService : JsonRpcWebSocketService
        {
            var rpcServiceAttribute = typeof(TJsonRpcWebSocketService).GetCustomAttribute<JsonRpcServiceAttribute>();
            var path = rpcServiceAttribute?.Path ?? "/" + typeof(TJsonRpcWebSocketService).Name;
            Func<IWebSocketConnection> factory = () => app.ApplicationServices.GetRequiredService<TJsonRpcWebSocketService>();
            
            return app.Map(path, a => a.UseMiddleware<JsonRpcWebSocketMiddleware>(factory));
        }

        public static IServiceCollection AddWebSocketHandlers(this IServiceCollection services)
        {
            foreach(var type in Assembly.GetEntryAssembly().ExportedTypes)
            {
                if(type.GetTypeInfo().BaseType == typeof(JsonRpcWebSocketService))
                {
                    services.AddTransient(type);
                }
            }

            return services;
        }

        public static IApplicationBuilder UseJsonRpcApi(this IApplicationBuilder app)
        {
            return UseJsonRpcApi(app, new JsonRpcInfo());
        }
        
        public static IApplicationBuilder UseJsonRpcApi(this IApplicationBuilder app, JsonRpcInfo jsonRpcInfo)
        {
            app.Use(async (context, next) =>
            {
                var referer = SanitizeReferer(context.Request.GetTypedHeaders().Referer?.AbsolutePath ?? "");
                var requestPath = referer + context.Request.Path;
                var file = JsonRpcFileReader.GetFile(requestPath, jsonRpcInfo);
                if (!file.Exist)
                {
                    await next.Invoke();
                    return;
                }
                context.Response.ContentType = MimeTypeProvider.Get(file.Extension);
                context.Response.StatusCode = 200;
                await context.Response.Body.WriteAsync(file.Buffer, 0, file.Buffer.Length);
            });

            return app;
        }

        private static string SanitizeReferer(string referer)
        {
            if (referer.EndsWith("/"))
            {
                return referer.Substring(0, referer.Length - 1);
            }
            return referer;
        }
    }
    
}