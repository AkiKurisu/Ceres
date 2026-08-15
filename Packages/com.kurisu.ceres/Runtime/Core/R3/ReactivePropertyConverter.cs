using System;
using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace R3.Ceres
{
    /// <summary>
    /// Serialization helper for <see cref="ReactiveProperty{T}"/> when use <see cref="JsonConverter"/>
    /// </summary>
    public class ReactivePropertyConverter : JsonConverter
    {
        private interface IReactivePropertyHandler
        {
            void WriteJson(JsonWriter writer, object value, JsonSerializer serializer);
            
            object ReadJson(JsonReader reader, JsonSerializer serializer);
        }
        
        private class ReactivePropertyHandler<T> : IReactivePropertyHandler
        {
            public void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var rp = (ReactiveProperty<T>)value;
                serializer.Serialize(writer, rp.Value);
            }

            public object ReadJson(JsonReader reader, JsonSerializer serializer)
            {
                var value = serializer.Deserialize<T>(reader);
                return new ReactiveProperty<T>(value);
            }
        }
        
        private static readonly ConcurrentDictionary<Type, IReactivePropertyHandler> Handlers = new();

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsGenericType 
                   && objectType.GetGenericTypeDefinition() == typeof(ReactiveProperty<>);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var type = value.GetType();
            var handler = GetOrCreateHandler(type);
            handler.WriteJson(writer, value, serializer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                var handler = GetOrCreateHandler(objectType);
                return handler.ReadJson(reader, serializer);
            }
            catch (Exception)
            {
                return Activator.CreateInstance(objectType);
            }
        }
        
        private static IReactivePropertyHandler GetOrCreateHandler(Type objectType)
        {
            if (Handlers.TryGetValue(objectType, out var handler))
            {
                return handler;
            }
            
            var valueType = objectType.GetGenericArguments()[0];
            var handlerType = typeof(ReactivePropertyHandler<>).MakeGenericType(valueType);
            handler = (IReactivePropertyHandler)Activator.CreateInstance(handlerType);
            Handlers.TryAdd(objectType, handler);
            return handler;
        }
    }
}