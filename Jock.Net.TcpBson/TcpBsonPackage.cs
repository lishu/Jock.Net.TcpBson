using System;
using System.IO;

namespace Jock.Net.TcpBson
{
    class TcpBsonPackage
    {

        public TcpBsonPackageType Type { get; set; }
        public string DataType { get; set; }
        public Action Callback { get; set; }
        public byte[] DataBytes { get; set; }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(DataType ?? string.Empty);
                    writer.Write(DataBytes?.Length ?? 0);
                    if (DataBytes != null && DataBytes.Length > 0)
                    {
                        writer.Write(DataBytes, 0, DataBytes.Length);
                        writer.Flush();
                    }
                    var buffer = ms.ToArray();
                    var fullBuffer = new byte[buffer.Length + 5];
                    Array.Copy(buffer, 0, fullBuffer, 5, buffer.Length);
                    fullBuffer[0] = (byte)Type;
                    buffer = BitConverter.GetBytes(buffer.Length);
                    Array.Copy(buffer, 0, fullBuffer, 1, 4);
                    return fullBuffer;
                }
            }
        }
    }
}
