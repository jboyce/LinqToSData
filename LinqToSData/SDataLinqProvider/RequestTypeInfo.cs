using System;
using System.Linq;
using System.Reflection;
using Sage.Integration.Messaging.Model;

namespace SDataLinqProvider
{
    public class RequestTypeInfo
    {
        public Type EntityType { get; private set; }
        public Type RequestHandlerType { get; private set; }
        public string ResourceKind { get; private set; }
        public Type FeedEntryType { get; private set; }
        public Type FeedType { get; private set; }

        public RequestTypeInfo(Type entityType)
        {
            LoadInfoFromFrom752Adapter(entityType);
        }

        private string GetResourcePath(Type type)
        {
            var attrib = Attribute.GetCustomAttribute(type, typeof(RequestPathAttribute)) as RequestPathAttribute;
            return attrib.Path;
        }

        private void LoadInfoFromFromAugustaAdapter(Type entityType)
        {
            EntityType = entityType;

            Assembly assembly = Assembly.Load("Sage.Integration.Entity.Feeds");
            Type requestType = assembly.GetTypes().Where(type =>
                type.BaseType.Name.StartsWith("RequestHandlerBase")
                && type.BaseType.GetGenericArguments()[2] == entityType)
                .First();

            RequestHandlerType = requestType;
            ResourceKind = GetResourcePath(requestType);
            FeedEntryType = requestType.BaseType.GetGenericArguments()[1];
            FeedType = requestType.BaseType.GetGenericArguments()[0];
        }

        private void LoadInfoFromFrom752Adapter(Type entityType)
        {
            EntityType = entityType;

            Assembly assembly = Assembly.Load("Sage.Integration.Entity.Feeds");
            Type requestType = assembly.GetTypes().Where(type =>
                type.BaseType.Name.StartsWith("DynamicRequestBase")
                && type.BaseType.GetGenericArguments()[2] == entityType)
                .First();

            RequestHandlerType = requestType;
            ResourceKind = GetResourcePath(requestType);
            FeedEntryType = requestType.BaseType.GetGenericArguments()[1];
            FeedType = requestType.BaseType.GetGenericArguments()[0];
        }
    }
}