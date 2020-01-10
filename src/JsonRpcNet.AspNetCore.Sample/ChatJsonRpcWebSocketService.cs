using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JsonRpcNet.Attributes;

namespace JsonRpcNet.AspNetCore.Sample
{
    public class UserAddedEventArgs : EventArgs
    {
        public string UserName { get; set; }
    }
    [JsonRpcService("chat", Description = "Chat hub", Name = "ChatService")]
    public class ChatJsonRpcWebSocketService : JsonRpcWebSocketService
    {
        [JsonRpcNotification("userAdded", Description = "Invoked when user added to chat")]
        private event EventHandler<UserAddedEventArgs> UserAdded;
        
        [JsonRpcMethod("SendMessage", Description = "Sends a message to the chat")]
        public Task SendMessage(string message)
        {
           return BroadcastAsync(message);
        }

        [JsonRpcMethod("SendMessageEcho", Description = "Sends a message to the chat and get and echo back")]
        public async Task<string> EchoMessage(string message)
        {
            await BroadcastAsync(message);
            return message;
        }
        
        [JsonRpcMethod("AddUser", Description = "Add a user to the chat")]
        public void AddUser(AddUserRequest request)
        {
            BroadcastAsync($"User {request.Name} joined").GetAwaiter().GetResult();
            UserAdded?.Invoke(this, new UserAddedEventArgs{UserName = request.Name});
        }

        [JsonRpcMethod("GetUsers", Description = "Gets users in the chat")]
        public Task<List<User>> GetUsers()
        {
            return
                Task.FromResult(
                    new List<User>
                    {
                        new User
                        {
                            Name = "John Wick",
                            Id = "1",
                            UserType = UserType.Admin
                        },
                        new User
                        {
                            Name = "Hella joof",
                            Id = "2",
                            UserType = UserType.NonAdmin
                        }
                    });
        }
        protected override Task OnBinaryMessage(ArraySegment<byte> buffer)
        {
            throw new NotImplementedException();
        }

        protected override Task OnConnected()
        {
            Console.WriteLine($"{GetType().Name} Connect...");
            return base.OnConnected();
        }

        protected override Task OnDisconnected(CloseStatusCode code, string reason)
        {
            Console.WriteLine($"{GetType().Name} Disconnected with code: " + code.ToString());
            return base.OnDisconnected(code, reason);
        }
    }

}
