﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Wire.ValueSerializers;

namespace Wire
{
    public class Serializer
    {
        private readonly Dictionary<Type, ValueSerializer> _serializers = new Dictionary<Type, ValueSerializer>();

        //private static readonly Dictionary<Type, ValueSerializer> PrimitiveSerializers = new Dictionary
        //    <Type, ValueSerializer>
        //{
        //    [typeof (int)] = Int32Serializer.Instance,
        //    [typeof(long)] = Int64Serializer.Instance,
        //    [typeof(short)] = Int16Serializer.Instance,
        //    [typeof(byte)] = ByteSerializer.Instance,
        //    [typeof(DateTime)] = DateTimeSerializer.Instance,
        //    [typeof(string)] = StringSerializer.Instance,
        //    [typeof(double)] = DoubleSerializer.Instance,
        //    [typeof(float)] = FloatSerializer.Instance,
        //    [typeof(Guid)] = GuidSerializer.Instance,
        //};


        private ValueSerializer GetSerialzerForPoco(Type type)
        {
            ValueSerializer serializer;
            if (!_serializers.TryGetValue(type, out serializer))
            {
                serializer = BuildSerializer(type);
                _serializers.Add(type, serializer);
            }
            return serializer;
        }

        private ValueSerializer BuildSerializer(Type type)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            var fieldWriters = new List<Action<Stream, object, SerializerSession>>();
            var fieldReaders = new List<Action<Stream, object, SerializerSession>>();
            var fieldNames = new List<byte[]>();
            foreach (var field in fields)
            {
                var f = field;
                var s = GetSerializerByType(field.FieldType);

                var getFieldValue = GenerateFieldReader(type, f);

                byte[] fieldName = Encoding.UTF8.GetBytes(f.Name);
                fieldNames.Add(fieldName);
                Action<Stream, object, SerializerSession> fieldWriter = (stream, o, session) =>
                {
                //    
                    var value = getFieldValue(o);
                    s.WriteValue(stream, value, session);
                };
                fieldWriters.Add(fieldWriter);

                Action<Stream, object, SerializerSession> fieldReader = (stream, o, session) =>
                {
                //    ByteArraySerializer.Instance.ReadValue(stream, session);
                    var value = s.ReadValue(stream, session);
                    f.SetValue(o, value);
                };
                fieldReaders.Add(fieldReader);
            }

            Action<Stream,object,SerializerSession> writer = (stream, o, session) =>
            {
                for (var index = 0; index < fieldWriters.Count; index++)
                {
                    ByteArraySerializer.Instance.WriteValue(stream, fieldNames[index], session);
                    var fieldWriter = fieldWriters[index];
                    fieldWriter(stream, o, session);
                }
            };
            Func < Stream, SerializerSession, object> reader = (stream, session) =>
            {
                var instance = Activator.CreateInstance(type);
                for (var index = 0; index < fieldReaders.Count; index++)
                {
                    var fieldName = (byte[])ByteArraySerializer.Instance.ReadValue(stream, session);
                    //TODO: check if correct field
                    var fieldReader = fieldReaders[index];
                    fieldReader(stream, instance, session);
                }
                return instance;
            };
            var serializer = new ObjectSerializer(type,writer,reader);
            return serializer;
        }

        private static Func<object, object> GenerateFieldReader(Type type, FieldInfo f)
        {
            var param = Expression.Parameter(typeof (object));
            Expression castParam = Expression.Convert(param, type);
            Expression x = Expression.Field(castParam, f);
            Expression castRes = Expression.Convert(x, typeof (object));
            var getFieldValue = Expression.Lambda<Func<object, object>>(castRes, param).Compile();
            return getFieldValue;
        }

        public void Serialize(object obj, Stream stream)
        {
            var session = new SerializerSession
            {
                Buffer = new byte[100],
                Serializer = this
            };
            var type = obj.GetType();
            var s = GetSerializerByType(obj.GetType());
            s.WriteManifest(stream, type, session);
            s.WriteValue(stream, obj, session);
        }

        public T Deserialize<T>(Stream stream)
        {
            var session = new SerializerSession
            {
                Buffer = new byte[100],
                Serializer = this
            };
            var s = GetSerializerByManifest(stream, session);
            return (T) s.ReadValue(stream, session);
        }

        public ValueSerializer GetSerializerByType(Type type)
        {
            //TODO: code generate this
            //ValueSerializer tmp;
            //if (_primitiveSerializers.TryGetValue(type, out tmp))
            //{
            //    return tmp;
            //}

            if (type == typeof(int))
                return Int32Serializer.Instance;

            if (type == typeof(long))
                return Int64Serializer.Instance;

            if (type == typeof(short))
                return Int16Serializer.Instance;

            if (type == typeof(byte))
                return ByteSerializer.Instance;

            if (type == typeof(bool))
                return BoolSerializer.Instance;

            if (type == typeof(DateTime))
                return DateTimeSerializer.Instance;

            if (type == typeof(string))
                return StringSerializer.Instance;

            if (type == typeof(Guid))
                return GuidSerializer.Instance;

            if (type == typeof(float))
                return FloatSerializer.Instance;

            if (type == typeof(double))
                return DoubleSerializer.Instance;

            if (type == typeof(decimal))
                return DecimalSerializer.Instance;

            if (type == typeof(char))
                return CharSerializer.Instance;

            if (type == typeof(byte[]))
                return ByteArraySerializer.Instance;

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == typeof(int) ||
                    elementType == typeof(long) ||
                    elementType == typeof(short) ||
                    elementType == typeof(DateTime) ||
                    elementType == typeof(bool) ||
                    elementType == typeof(string) ||
                    elementType == typeof(Guid) ||
                    elementType == typeof(float) ||
                    elementType == typeof(double) ||
                    elementType == typeof(decimal) ||
                    elementType == typeof(char)
                    )
                {
                    return ConsistentArraySerializer.Instance;
                }
                throw new NotSupportedException(""); //array of other types
            }

            var serializer = GetSerialzerForPoco(type);

            return serializer;
        }

        public ValueSerializer GetSerializerByManifest(Stream stream, SerializerSession session)
        {
            var first = stream.ReadByte();
            switch (first)
            {
                case 2:
                    return Int64Serializer.Instance;
                case 3:
                    return Int16Serializer.Instance;
                case 4:
                    return ByteSerializer.Instance;
                case 5:
                    return DateTimeSerializer.Instance;
                case 6:
                    return BoolSerializer.Instance;
                case 7:
                    return StringSerializer.Instance;
                case 8:
                    return Int32Serializer.Instance;
                case 9:
                    return ByteArraySerializer.Instance;
                    //insert
                case 11:
                    return GuidSerializer.Instance;
                case 12:
                    return FloatSerializer.Instance;
                case 13:
                    return DoubleSerializer.Instance;
                case 14:
                    return DecimalSerializer.Instance;
                case 15:
                    return CharSerializer.Instance;
                case 254:
                    return ConsistentArraySerializer.Instance;
                case 255:
                    var type = GetNamedTypeFromManifest(stream, session);
                    return GetSerialzerForPoco(type);
                default:
                    throw new NotSupportedException("Unknown manifest value");
            }
        }


        public Type GetNamedTypeFromManifest(Stream stream, SerializerSession session)
        {
            var bytes = (byte[]) ByteArraySerializer.Instance.ReadValue(stream, session);
            var typename = Encoding.UTF8.GetString(bytes);
            var type = Type.GetType(typename);
            return type;
        }
    }
}
 