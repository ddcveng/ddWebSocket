using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace chatApp
{
    public static class JSONFileService
    {
        /// Map types of data to their storage file
        private static Dictionary<Type, string> fileMap = new Dictionary<Type, string>()
        {
            {typeof(User) , Program.UserPath },
            {typeof(ChatRoom) , Program.ChatRoomPath }
        };

        /// Read all data from file to memory
        /**
         * Reads the corresponding json file a deserializes
         * all objects into type T.
         *
         * @tparam T One of the types in fileMap. JSON file is chosen
         * based on this type and the data is deserialized into this type
         */
        public static List<T> GetAll<T>()
        {
            using (var jsonFileReader = File.OpenText(fileMap[typeof(T)]))
            {
                return JsonSerializer.Deserialize<List<T>>(jsonFileReader.ReadToEnd(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
        }

        /// Add a new entry to the storage file
        /**
         * Seriealizes the object and writes it to the file
         * To reset the file delete everything and put just [] in
         * This function assumes the files use UNIX end of lines (just '\n')
         *
         * @param newObj The new object to add
         * @tparam T Type of the object to add, also determines location of the file
         */
        public static void Add<T>(T newObj)
        {
            using (FileStream fs = File.Open(fileMap[typeof(T)], FileMode.Open, FileAccess.ReadWrite))
            {
                using var sw = new StreamWriter(fs);
                fs.Seek(-2, SeekOrigin.End);
                if (fs.Length > 5)// [ ] CR LF EOF
                {
                    sw.Write(',');
                }
                sw.Write(newObj.ToString());
                sw.Write("]\n");
            }
        }

        /// Update an existing entry in the data files
        /**
         * Adds either a new chatroom to an existing user
         * or another new user to an existing chatroom
         *
         * @param objID The Guid of either a user or chatroom
         * @param toAdd The Guid of the object to add
         *
         * @tparam T Type of data to update
         */
        public static void Update<T>(Guid objID, Guid toAdd) where T : IJSONObject
        {
            var current = GetAll<T>();
            T obj = current.First(r => r.ID == objID);
            obj.IDList.Add(toAdd);
            File.WriteAllText(fileMap[typeof(T)], JsonSerializer.Serialize(current, new JsonSerializerOptions()
            {
                WriteIndented = true
            }));
        }

        /// Add a new message to a chatroom
        /**
         * @param objID The Guid of the chatroom
         * @param message The message to add
         */
        public static void Update(Guid objID, Message message)
        {
            var current = GetAll<ChatRoom>();
            ChatRoom obj = current.First(r => r.ID == objID);
            obj.Add(message);
            File.WriteAllText(fileMap[typeof(ChatRoom)], JsonSerializer.Serialize(current, new JsonSerializerOptions()
            {
                WriteIndented = true
            }));
        }
    }
}
