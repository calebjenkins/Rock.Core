﻿#if NET45
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Rock.Serialization
{
    public class BinaryFormatterSerializer : ISerializer
    {
        public void SerializeToStream(Stream stream, object item, Type type)
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(stream, item);
        }

        public object DeserializeFromStream(Stream stream, Type type)
        {
            var binaryFormatter = new BinaryFormatter();
            return binaryFormatter.Deserialize(stream);
        }

        public string SerializeToString(object item, Type type)
        {
            var data = this.SerializeToByteArray(item, type);
            return Convert.ToBase64String(data);
        }

        public object DeserializeFromString(string data, Type type)
        {
            var binaryData = Convert.FromBase64String(data);
            return this.DeserializeFromByteArray(binaryData, type);
        }
    }
}
#endif
