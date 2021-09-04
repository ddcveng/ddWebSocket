using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace chatApp
{
    /// Common interface for my objects that are serailized into a json file
    /**
     * Shared by the User and Chatroom classes
     */
    public interface IJSONObject
    {
        /// ID of the object
        public Guid ID { get; set; }
        /// A collection of references to other objects
        public List<Guid> IDList { get; set; }
    }

    /// Respresents a chatApp user
    /**
     * Properties of this class are serialized into the sotrage file
     */
    public class User : IJSONObject
    {
        public Guid ID { get; set; }
        /// Username of this user
        public string Username { get; set; }
        /// Encrypted password of this user
        public string Password { get; set; }
        /// Date and time of creation
        /**
         * Stored in format MM/DD/YYYY HH:MM:SS
         */
        public string DateCreated { get; set; }
        [JsonPropertyName("ChatRoomIDs")]
        public List<Guid> IDList { get; set; }

        public User()
        {
            ID = Guid.NewGuid();
            IDList = new List<Guid>();
        }

        /// Add a chatroom reference to this user
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

    /// Represents a chatroom messsage
    public class Message
    {
        /// Username of some ID of the sender
        public string Sender { get; set; }
        /// The message content
        public string Body { get; set; }
        /// Date and time when the message was sent
        public DateTime TimeSent { get; set; }
    }

    /// Represents a chatroom
    public class ChatRoom : IJSONObject
    {
        public Guid ID { get; set; }
        /// User defined name of the chatroom
        public string Name { get; set; }
        [JsonPropertyName("UserIDs")]
        public List<Guid> IDList { get; set; }
        /// A collection of all the messages that were sent in this chatroom
        public List<Message> Messages { get; set; }

        public ChatRoom()
        {
            ID = Guid.NewGuid();
            IDList = new List<Guid>();
            Messages = new List<Message>();
        }

        /// Add a user reference to the chatroom
        public void Add(Guid userID)
        {
            IDList.Add(userID);
        }

        /// Add a new message to the chatroom
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

        /// Get rid of all expensive data
        /**
         * Used when we just need the chatroom id and name and not necessarily
         * its contents
         */
        public void Minimize()
        {
            Messages = null;
            IDList = null;
        }
    }
}
