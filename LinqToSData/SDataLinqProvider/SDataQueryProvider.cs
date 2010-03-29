using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Web;
using Sage.Common.Syndication;
using Sage.Integration.Client;
using Sage.Integration.Messaging.Model;
using Sage.Common.Metadata;
using Sage.Platform.Orm.Interfaces;
using Sage.Platform.ComponentModel;
using Sage.SalesLogix.Orm;
using Sage.Platform.ChangeManagement;
using System.ComponentModel;

namespace SDataLinqProvider
{
    public class SDataQueryProvider<TEntity> : QueryProvider, ISDataCrudProvider
    {
        private string _sdataContractUrl;
        private readonly string _userName;
        private readonly string _password;
        internal List<string> IncludeNames { get; private set; }
        private Delegate _projector;
        private static WeakDictionary<object, string> _eTagCache = new WeakDictionary<object, string>();

        public SDataQueryProvider(string sdataContractUrl, string userName, string password)
        {
            _sdataContractUrl = sdataContractUrl;
            _userName = userName;
            _password = password;
            IncludeNames = new List<string>();
        }

        public override string GetQueryText(Expression expression)
        {
            return Translate(expression);
        }

        public override object Execute(Expression expression)
        {
            string queryText = GetQueryText(expression);

            List<TEntity> entities = GetEntitiesFromSData(queryText);

            if (_projector == null)
                return entities;

            Type listType = typeof (List<>).MakeGenericType(_projector.Method.ReturnType);
            IList list = Activator.CreateInstance(listType) as IList;
            
            entities.Select(entity => Convert.ChangeType(_projector.DynamicInvoke(entity),
                                                                 _projector.Method.ReturnType))
                .ForEach(entry => list.Add(entry));
            return list;
        }

        private List<TEntity> GetEntitiesFromSData(string sdataQuery)
        {
            var request = CreateRequest(new Uri(sdataQuery), RequestVerb.GET);
            Stream stream;
            IMediaTypeSerializer serializer;
            HttpStatusCode code = request.Send(out serializer, out stream);

            if (code == HttpStatusCode.OK)
            {
                var requestTypeInfo = new RequestTypeInfo(typeof(TEntity));
                IFeed feed = (IFeed)GetFeedFromStream(requestTypeInfo, stream);
                List<TEntity> entities = ConvertFeedToEntities(feed, requestTypeInfo) as List<TEntity>;
                return entities;
            }

            return new List<TEntity>();
        }

        private SDataRequest CreateRequest(Uri uri, RequestVerb verb)
        {
            //set http context because of a dependency in 7.5.2 DynamicRequestBase
            HttpContext.Current = new HttpContext(new HttpRequest("", uri.ToString(), ""), new HttpResponse(null));
            var request = new SDataRequest(uri, RequestVerb.GET);
            request.Username = _userName;
            request.Password = _password;
            return request;
        }

        private TEntity GetEntityFromSData(string sdataQuery)
        {
            var request = CreateRequest(new Uri(sdataQuery), RequestVerb.GET);
            Stream stream;
            IMediaTypeSerializer serializer;
            HttpStatusCode code = request.Send(out serializer, out stream);

            if (code == HttpStatusCode.OK)
            {
                var requestTypeInfo = new RequestTypeInfo(typeof(TEntity));
                FeedEntry entry = (FeedEntry)GetFeedEntryFromStream(requestTypeInfo, stream);
                var requestHandler = Activator.CreateInstance(requestTypeInfo.RequestHandlerType);
                Type concreteEntityType = FindConcreteEntityType();
                MethodInfo copyMethod = requestTypeInfo.RequestHandlerType
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .First(method => method.Name == "CopyFeedEntryToEntity");
                var propNames = GetNonRelationshipProperties(requestTypeInfo.FeedEntryType).Concat(IncludeNames);
                return CreateEntityFromFeedEntry(requestHandler, entry, concreteEntityType, copyMethod, propNames);
            }

            return default(TEntity);
        }

        internal TEntity GetEntity(string entityId)
        {
            var translator = new SDataQueryTranslator(_sdataContractUrl, typeof(TEntity));
            return GetEntityFromSData(translator.IdToQueryText(entityId));    
        }

        private object ConvertFeedToEntities(IFeed feed, RequestTypeInfo requestTypeInfo)
        {
            var request = Activator.CreateInstance(requestTypeInfo.RequestHandlerType);
            MethodInfo copyMethod = requestTypeInfo.RequestHandlerType
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(method => method.Name == "CopyFeedEntryToEntity");
            Type concreteEntityType = FindConcreteEntityType();
            var propNames = GetNonRelationshipProperties(requestTypeInfo.FeedEntryType).Concat(IncludeNames);

            return feed.Entries.Cast<FeedEntry>()
                .Select(entry => CreateEntityFromFeedEntry(request, entry, concreteEntityType, copyMethod, propNames))
                .ToList();
        }

        private List<string> GetNonRelationshipProperties(Type entryType)
        {
            return (from property in entryType.GetProperties()
                    where PropertyHasNonRelationshipAttribute(property)
                    select property.Name).ToList();
        }

        private bool PropertyHasNonRelationshipAttribute(PropertyInfo propInfo)
        {
            var attrib = Attribute.GetCustomAttribute(propInfo, typeof (SMEPropertyAttribute));
            return (attrib != null) && !attrib.GetType().IsAssignableFrom(typeof(SMERelationshipPropertyAttribute));
        }

        internal static Type FindConcreteEntityType()
        {
            Assembly assembly = Assembly.Load("Sage.SalesLogix.Entities");
            return assembly.GetTypes().Where(type => typeof (TEntity).IsAssignableFrom(type)).First();
        }

        internal static TEntity CreateEntityFromFeedEntry(object request, FeedEntry entry, Type concreteEntityType, 
            MethodInfo copyMethod, IEnumerable<string> propertiesToCopy)
        {
            entry.ResetChangedProperties();
            propertiesToCopy.ForEach(propName => entry.SetPropertyChanged(propName, true));
            var entity = (TEntity)Activator.CreateInstance(concreteEntityType);
            copyMethod.Invoke(request, new object[] { entry, entity, new InclusionNode(InclusionNode.InclusionLevel.Include) });
            (entity as IAssignableId).Id = entry.Key;
            ResetPersistentState(entity as IPersistentEntity);
            _eTagCache.Add(entity, entry.HttpETag);
            return entity;
        }

        private object GetFeedFromStream(RequestTypeInfo requestTypeInfo, Stream stream)
        {
            var feedSer = new FeedSerializer();
            var feed = Activator.CreateInstance(requestTypeInfo.FeedType);
            MethodInfo loadMethod = typeof(FeedSerializer).GetMethods()
                .First(m => m.Name == "LoadFromStream"
                            && m.GetParameters().Length == 2
                            && m.GetParameters()[0].ParameterType.Name == "Feed`1");
            MethodInfo genLoadMethod = loadMethod.MakeGenericMethod(requestTypeInfo.FeedEntryType);
            genLoadMethod.Invoke(feedSer, new object[] { feed, stream});
            return feed;
        }

        private object GetFeedEntryFromStream(RequestTypeInfo requestTypeInfo, Stream stream)
        {
            var feedSer = new FeedSerializer();
            var feedEntry = Activator.CreateInstance(requestTypeInfo.FeedEntryType);
            MethodInfo loadMethod = typeof(FeedSerializer).GetMethods()
                .First(m => m.Name == "LoadFromStream"
                            && m.GetParameters().Length == 2
                            && m.GetParameters()[0].Name == "feedEntry");
            MethodInfo genLoadMethod = loadMethod.MakeGenericMethod(requestTypeInfo.FeedEntryType);
            genLoadMethod.Invoke(feedSer, new object[] { feedEntry, stream });
            return feedEntry;
        }

        private string Translate(Expression expression)
        {
            expression = Evaluator.PartialEval(expression);
            TranslateResult result = new SDataQueryTranslator(_sdataContractUrl, typeof(TEntity)).Translate(expression, IncludeNames);
            if (result.Projector != null)
                _projector = (result.Projector as LambdaExpression).Compile();
            return result.QueryText;
        }

        internal FeedEntry CopyEntityToFeedEntry(IPersistentEntity entity, InclusionNode include)
        {
            //set http context because of a dependency in 7.5.2 DynamicRequestBase
            HttpContext.Current = new HttpContext(new HttpRequest("", _sdataContractUrl, ""), new HttpResponse(null));

            var requestTypeInfo = new RequestTypeInfo(typeof(TEntity));
            var requestHandler = Activator.CreateInstance(requestTypeInfo.RequestHandlerType);

            MethodInfo copyMethod = requestTypeInfo.RequestHandlerType
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(method => method.Name == "CopyEntityToFeedEntry");

            var entry = (FeedEntry)Activator.CreateInstance(requestTypeInfo.FeedEntryType);
            copyMethod.Invoke(requestHandler, new object[] { entity, entry, include });
            return entry;
        }

        internal FeedEntry CopyEntityToFeedEntry(IPersistentEntity entity)
        {
            return CopyEntityToFeedEntry(entity, new InclusionNode(InclusionNode.InclusionLevel.Include));
        }

        #region ISDataCrudProvider Members

        void ISDataCrudProvider.Insert(IPersistentEntity entity)
        {
            FeedEntry entry = CopyEntityToFeedEntry(entity);
            var translator = new SDataQueryTranslator(_sdataContractUrl, typeof (TEntity));
            var request = CreateRequest(new Uri(translator.GetResourceUrl()), RequestVerb.POST);
            request.Operations[0].FeedEntry = entry;
            request.Operations[0].MediaType = MediaType.AtomEntry;
            request.Operations[0].Verb = RequestVerb.POST;
            HttpStatusCode code = request.Send();            
        }

        void ISDataCrudProvider.Update(IPersistentEntity entity)
        {
            ChangeSet changes = (entity as EntityBase).ChangeSet;
            var propChanges = changes.FindAll<PropertyChange>();
            InclusionNode include = new InclusionNode(InclusionNode.InclusionLevel.Include);
            propChanges.ForEach(change => include.AddChild(change.MemberName, new InclusionNode(InclusionNode.InclusionLevel.Include)));
            
            FeedEntry entry = CopyEntityToFeedEntry(entity, include);
            var translator = new SDataQueryTranslator(_sdataContractUrl, typeof(TEntity));
            var queryText = translator.IdToQueryText((entity as IComponentReference).Id.ToString());
            var request = CreateSimpleWebRequest(queryText, "PUT");
            string etag = "";
            _eTagCache.TryGetValue(entity, out etag);
            request.Headers.Add(HttpRequestHeader.IfMatch, etag);
            SetRequestContent(request, entry);
            var response = (HttpWebResponse)request.GetResponse();
        }

        void SetRequestContent(HttpWebRequest request, FeedEntry entry)
        {
            var requestTypeInfo = new RequestTypeInfo(typeof(TEntity));
            request.MediaType = "application/atom+xml;type=entry";//MediaType.AtomEntry;

            using(MemoryStream stream = new MemoryStream())
            {
                FeedSerializer serializer = new FeedSerializer();
                serializer.MediaType = MediaType.AtomEntry;
                MethodInfo tempMethod = typeof (FeedSerializer).GetMethods()
                    .Where(m => m.Name == "SaveToStream"
                                && m.GetParameters().Length == 3
                                && m.GetParameters()[0].ParameterType != typeof (IFeed)).First();
                MethodInfo saveMethod = tempMethod.MakeGenericMethod(requestTypeInfo.FeedEntryType);
                SerializationSettings serSettings = new SerializationSettings();
                saveMethod.Invoke(serializer, new object[] { entry, stream, serSettings });

                request.ContentLength = stream.Length;

                stream.Position = 0;
                
                using(Stream reqStream = request.GetRequestStream())
                {
                    CopyStream(stream, reqStream);
                }
            }            
        }

        private static void CopyStream(Stream input, Stream output)
        {
            using (StreamReader reader = new StreamReader(input))
            using (StreamWriter writer = new StreamWriter(output))
            {
                writer.Write(reader.ReadToEnd());
            }
        }

        void ISDataCrudProvider.Delete(IPersistentEntity entity)
        {            
            var translator = new SDataQueryTranslator(_sdataContractUrl, typeof(TEntity));
            var queryText = translator.IdToQueryText((entity as IComponentReference).Id.ToString());
            var request = CreateSimpleWebRequest(queryText, "DELETE");
            string etag = "";
            _eTagCache.TryGetValue(entity, out etag);
            request.Headers.Add(HttpRequestHeader.IfMatch, etag);
            var response = (HttpWebResponse)request.GetResponse();
        }

        private HttpWebRequest CreateSimpleWebRequest(string url, string verb)
        {
            var request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.Method = verb;
            CredentialCache cache = new CredentialCache();
            cache.Add(new Uri(url), "Digest", new NetworkCredential(_userName, _password));
            request.Credentials = cache;

            return request;
        }
        
        private static void ResetPersistentState(IPersistentEntity entity)
        {
            Type entityType = Type.GetType("Sage.SalesLogix.Orm.EntityBase, Sage.SalesLogix");
            var field = entityType.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(entity, PersistentState.Unmodified);
        }

        #endregion
    }
}