using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace otavaSocket
{
    public static class JSONFileService
    {
        private static Dictionary<Type, string> fileMap = new Dictionary<Type, string>()
        {
            {typeof(User) , Program.UserPath },
            {typeof(ChatRoom) , Program.ChatRoomPath }
        };

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

        public static async void Add<T>(T newObj)
        {
            using (FileStream fs = File.Open(fileMap[typeof(T)], FileMode.Open, FileAccess.ReadWrite))
            {
                using var sw = new StreamWriter(fs);
                fs.Seek(-1, SeekOrigin.End);
                if (fs.Length > 5)// [ ] CR LF EOF
                {
                    await sw.WriteAsync(',');
                }
                await sw.WriteAsync(newObj.ToString());
                await sw.WriteAsync("]");
            }
        }

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

        public static void Update(Guid objID, string icon)
        {
            var current = GetAll<User>();
            User u = current.First(r => r.ID == objID);
            u.Icon = icon;
            File.WriteAllText(fileMap[typeof(User)], JsonSerializer.Serialize(current, new JsonSerializerOptions()
            {
                WriteIndented = true
            }));
        }

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