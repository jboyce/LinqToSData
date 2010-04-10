using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Sage.Common.Syndication;
using Sage.SData.Client.Extensions;
using Sage.Platform.Orm.Interfaces;
using Sage.Platform.ComponentModel;
using Sage.SData.Client.Core;
using Sage.SData.Client.Atom;

namespace SDataLinqProvider
{
    public class SDataQueryProvider<TEntity> : IQueryProvider, ISDataCrudProvider
    {
        private readonly string _sdataContractUrl;
        private readonly string _userName;
        private readonly string _password;
        internal List<string> IncludeNames { get; private set; }
        private Delegate _projector;
        private readonly SDataService _sdataService;
        private readonly RequestTypeInfo _requestTypeInfo;
        private static readonly WeakDictionary<object, string> _eTagCache = new WeakDictionary<object, string>();

        public SDataQueryProvider(string sdataContractUrl, string userName, string password)
        {
            _sdataContractUrl = sdataContractUrl;
            _userName = userName;
            _password = password;
            _requestTypeInfo = new RequestTypeInfo(typeof(TEntity));
            _sdataService = new SDataService(_sdataContractUrl, _userName, _password);
            IncludeNames = new List<string>();
        }

        IQueryable<TReturnElement> IQueryProvider.CreateQuery<TReturnElement>(Expression expression)
        {
            return new SDataQuery<TReturnElement, TEntity>(this, expression);
        }

        IQueryable IQueryProvider.CreateQuery(Expression expression)
        {
            Type elementType = TypeSystem.GetElementType(expression.Type);
            try
            {
                return (IQueryable)Activator.CreateInstance(typeof(SDataQuery<,>).MakeGenericType(elementType, typeof(TEntity)), new object[] { this, expression });
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        S IQueryProvider.Execute<S>(Expression expression)
        {
            return (S)Execute(expression);
        }

        object IQueryProvider.Execute(Expression expression)
        {
            return Execute(expression);
        }

        public string GetQueryText(Expression expression)
        {
            return Translate(expression);
        }

        public object Execute(Expression expression)
        {
            string queryText = GetQueryText(expression);

            IEnumerable<TEntity> entities = GetEntitiesFromSData(queryText);

            if (_projector == null)
                return entities;

            return ReturnEntityProjection<object>(entities);
        }

        public IEnumerable<TResult> Execute<TResult>(Expression expression)
        {
            string queryText = GetQueryText(expression);

            IEnumerable<TEntity> entities = GetEntitiesFromSData(queryText);

            if (_projector == null)
                return (IEnumerable<TResult>)entities;

            return ReturnEntityProjection<TResult>(entities);
        }

        private IEnumerable<TResult> ReturnEntityProjection<TResult>(IEnumerable<TEntity> entities)
        {
            foreach (TEntity entity in entities)
            {
                yield return (TResult)_projector.DynamicInvoke(entity);
            }
        }

        private IEnumerable<TEntity> GetEntitiesFromSData(string sdataQuery)
        {
            var request = CreateCollectionRequest();
            SDataUri uri = new SDataUri(sdataQuery);
            if (!string.IsNullOrEmpty(uri.Where))
                request.QueryValues["where"] = uri.Where;
            
            if (uri.QueryArgs.ContainsKey("select"))
                request.QueryValues["select"] = uri.QueryArgs["select"];

            Type concreteEntityType = FindConcreteEntityType();

            var reader = request.ExecuteReader();
            var currentEntry = reader.Current;

            //resorting to a while loop since the reader seems to have a bug where it modifies the enumeration
            while (currentEntry != null)
            {
                var entity = Activator.CreateInstance(concreteEntityType) as IPersistentEntity;
                CopyAtomEntryToEntity(currentEntry, entity);
                yield return (TEntity)entity;
                currentEntry = reader.Next() ? reader.Current : null;
            }
        }

        private TEntity GetEntityFromSData(string entityId)
        {
            var request = CreateResourceRequest(entityId);
            AtomEntry entry = request.Read();

            Type concreteEntityType = FindConcreteEntityType();
            var entity = Activator.CreateInstance(concreteEntityType) as IPersistentEntity;
            CopyAtomEntryToEntity(entry, entity);
            return (TEntity)entity;
        }

        internal TEntity GetEntity(string entityId)
        {
            return GetEntityFromSData(entityId);    
        }

        internal static Type FindConcreteEntityType()
        {
            Assembly assembly = Assembly.Load("Sage.SalesLogix.Entities");
            return assembly.GetTypes().Where(type => typeof (TEntity).IsAssignableFrom(type)).First();
        }                

        private string Translate(Expression expression)
        {
            expression = Evaluator.PartialEval(expression);
            TranslateResult result = new SDataQueryTranslator(_sdataContractUrl, typeof(TEntity)).Translate(expression, IncludeNames);
            if (result.Projector != null)
                _projector = (result.Projector as LambdaExpression).Compile();
            return result.QueryText;
        }

        #region ISDataCrudProvider Members

        void ISDataCrudProvider.Insert(IPersistentEntity entity)
        {
            var request = CreateResourceRequest(null);
            request.Entry = CopyEntityToAtomEntry(entity); 
            request.Create();
        }

        AtomEntry CopyEntityToAtomEntry(IPersistentEntity entity)
        {
            var entry = new AtomEntry();
            var payload = new SDataPayload();
            payload.ResourceName = typeof (TEntity).Name.Substring(1);
            payload.Namespace = "http://schemas.sage.com/dynamic/2007";

            foreach (var prop in typeof(TEntity).GetProperties())
            {
                if (!prop.PropertyType.FullName.StartsWith("Sage.Entity.Interfaces") &&
                    !prop.PropertyType.FullName.StartsWith("ICollection") &&
                    prop.CanWrite)
                    payload.Values[prop.Name] = prop.GetValue(entity, null);
            }
            entry.SetSDataPayload(payload);

            return entry;
        }

        void CopyAtomEntryToEntity(AtomEntry entry, IPersistentEntity entity)
        {
            var payload = entry.GetSDataPayload();
            foreach (var prop in typeof(TEntity).GetProperties())
            {
                SetEntityProperty(prop, payload, entity);
            }

            (entity as IAssignableId).Id = payload.Key;
        }

        private void SetEntityProperty(PropertyInfo prop, SDataPayload payload, IPersistentEntity entity)
        {
            if (!prop.CanWrite)
                return;

            if (prop.PropertyType.FullName.StartsWith("Sage.Entity.Interfaces") ||
                prop.PropertyType.FullName.StartsWith("ICollection")) 
                return;

            if (!payload.Values.ContainsKey(prop.Name) || payload.Values[prop.Name] == null) 
                return;

            object convertedValue;
            if (prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(Nullable<bool>))
                convertedValue = Convert.ToBoolean(payload.Values[prop.Name]);
            else if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(Nullable<DateTime>))
                convertedValue = Convert.ToDateTime(payload.Values[prop.Name]);
            else if (prop.PropertyType.IsAssignableFrom(typeof(int)))
                convertedValue = Convert.ToInt32(payload.Values[prop.Name]);
            else if (prop.PropertyType.IsAssignableFrom(typeof(decimal)))
                convertedValue = Convert.ToDecimal(payload.Values[prop.Name]);
            else if (prop.PropertyType.IsAssignableFrom(typeof(double)))
                convertedValue = Convert.ToDouble(payload.Values[prop.Name]);
            else
                convertedValue = Convert.ChangeType(payload.Values[prop.Name], prop.PropertyType);

            prop.SetValue(entity, convertedValue, null);
        }

        void ISDataCrudProvider.Update(IPersistentEntity entity)
        {
            var request = CreateResourceRequest((entity as IComponentReference).Id.ToString());
            request.Entry = CopyEntityToAtomEntry(entity);  //only get modified properties
            string etag = "";
            _eTagCache.TryGetValue(entity, out etag);
            request.Entry.SetSDataHttpIfMatch(etag);
            request.Update();
        }

        void ISDataCrudProvider.Delete(IPersistentEntity entity)
        {
            var request = CreateResourceRequest((entity as IComponentReference).Id.ToString());
            var entry = new AtomEntry();
            string etag = "";
            _eTagCache.TryGetValue(entity, out etag);
            entry.SetSDataHttpIfMatch(etag);
            request.Delete();
        }

        private SDataResourceCollectionRequest CreateCollectionRequest()
        {
            var request = new SDataResourceCollectionRequest(_sdataService);
            request.ResourceKind = _requestTypeInfo.ResourceKind;
            return request;
        }

        private SDataSingleResourceRequest CreateResourceRequest(string entityId)
        {
            var request = new SDataSingleResourceRequest(_sdataService);
            request.ResourceKind = _requestTypeInfo.ResourceKind;
            if (!string.IsNullOrEmpty(entityId))
                request.ResourceSelector = "('" + entityId + "')";
            return request;
        }
        
        #endregion
    }
}