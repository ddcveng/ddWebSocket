using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace otavaSocket
{
    public interface IJSONObject
    {
        public Guid ID { get; set; }
        public List<Guid> IDList { get; set; }
    }
    public class User : IJSONObject
    {
        public Guid ID { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string DateCreated { get; set; }
        [JsonPropertyName("ChatRoomIDs")]
        public List<Guid> IDList { get; set; }
        public string Icon { get; set; }

        public User()
        {
            ID = Guid.NewGuid();
            IDList = new List<Guid>();
            Icon = null;
        }

        public void Add(Guid chatRoomID)
        {
            IDList.Add(chatRoomID);
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions()
            {
                WriteIndented = true
            });
        }
    }

    public class JSONPacket
    {
        public string Redirect { get; set; }
        public string Data { get; set; }
        public bool HasIcon { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class Message
    {
        public string Sender { get; set; }
        public string Body { get; set; }
        public string Icon { get; set; }
        public DateTime TimeSent { get; set; }
    }

    public class ChatRoom : IJSONObject
    {
        public Guid ID { get; set; }
        public string Name { get; set; }
        [JsonPropertyName("UserIDs")]
        public List<Guid> IDList { get; set; }
        public List<Message> Messages { get; set; }

        public ChatRoom()
        {
            ID = Guid.NewGuid();
            IDList = new List<Guid>();
            Messages = new List<Message>();
        }

        public void Add(Guid userID)
        {
            IDList.Add(userID);
        }

        public void Add(Message message)
        {
            Messages.Add(message);
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions()
            {
                WriteIndented = true
            });
        }

        public void Minimize()
        {
            Messages = null;
            IDList = null;
        }
    }

    public class ChatRoomPacket
    {
        public ChatRoom Main { get; set; }
        public ChatRoom[] MinimalChatRoomData { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }


}
