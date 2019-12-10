using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.IO;

namespace Jock.Net.TcpBson
{
    class BsonConvert
    {
        public static byte[] SerializeObject(object obj, Type type = null)
        {
            var ms = new MemoryStream();
            var writer = new BsonDataWriter(ms);
            var js = JsonSerializer.CreateDefault();
            js.Serialize(writer, obj, type);
            return ms.ToArray();
        }
        public static object DeserializeObject(byte[] bytes, Type type)
        {
            var ms = new MemoryStream(bytes);
            var reader = new BsonDataReader(ms);
            var js = JsonSerializer.CreateDefault();
            return js.Deserialize(reader, type);
        }

        public static T DeserializeObject<T>(byte[] bytes)
        {
            return (T)DeserializeObject(bytes, typeof(T));
        }
    }
}
