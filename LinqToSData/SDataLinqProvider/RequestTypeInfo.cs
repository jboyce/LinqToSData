using System;
using System.Linq;
using System.Reflection;
using Sage.Integration.Messaging.Model;

namespace SDataLinqProvider
{
    public class RequestTypeInfo
    {
        public Type EntityType { get; private set; }
        public string ResourceKind { get; private set; }

        public RequestTypeInfo(Type entityType)
        {
            EntityType = entityType;

            Assembly assembly = Assembly.Load("Sage.SData.Client.Entities");
            Type requestType = assembly.GetTypes().Where(type => entityType.IsAssignableFrom(type)).First();

            ResourceKind = GetResourcePath(requestType);
        }

        private string GetResourcePath(Type type)
        {
            var attrib = Attribute.GetCustomAttribute(type, typeof(RequestPathAttribute)) as RequestPathAttribute;
            return attrib.Path;
        }
    }
}