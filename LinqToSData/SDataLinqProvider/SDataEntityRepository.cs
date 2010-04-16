using System;
using System.Linq;
using System.Linq.Expressions;
using Sage.Platform.Orm.Interfaces;

namespace SDataLinqProvider
{
    public class SDataEntityRepository
    {
        private readonly string _sdataContractUrl;
        private readonly string _userName;
        private readonly string _password;

        public SDataEntityRepository(string sdataContractUrl, string userName, string password)
        {
            _sdataContractUrl = sdataContractUrl;
            _userName = userName;
            _password = password;
        }

        public SDataQuery<TEntity, TEntity> CreateQuery<TEntity>()
        {
            return new SDataQuery<TEntity, TEntity>(new SDataQueryProvider<TEntity>(_sdataContractUrl, _userName, _password, this));
        }

        public SDataQuery<TEntity, TEntity> CreateQuery<TEntity>(params Expression<Func<TEntity, object>>[] includeExpressions)
        {
            var query = new SDataQuery<TEntity, TEntity>(new SDataQueryProvider<TEntity>(_sdataContractUrl, _userName, _password, this));
            includeExpressions.ToList().ForEach(includeExpression => query.Include(includeExpression));
            return query;
        }

        public TEntity GetEntityById<TEntity>(string id)
        {
            var provider = new SDataQueryProvider<TEntity>(_sdataContractUrl, _userName, _password, this);
            return provider.GetEntity(id);
        }

        public TEntity Create<TEntity>()
        {
            Type entityType = SDataQueryProvider<TEntity>.FindConcreteEntityType();
            return (TEntity)Activator.CreateInstance(entityType);
        }

        public void Save(IPersistentEntity entity)
        {
            ISDataCrudProvider crudProvider = GetCrudProvider(entity);

            if ((entity.PersistentState & PersistentState.New) == PersistentState.New)
            {
                crudProvider.Insert(entity);
            }
            else if ((entity.PersistentState & PersistentState.Modified) == PersistentState.Modified)
            {
                crudProvider.Update(entity);
            }
        }

        public void Delete(IPersistentEntity entity)
        {
            ISDataCrudProvider crudProvider = GetCrudProvider(entity);
            crudProvider.Delete(entity);
        }

        private ISDataCrudProvider GetCrudProvider(IPersistentEntity entity)
        {
            Type entityIntfType = GetEntityInterfaceTypeFromInstance(entity);
            Type providerType = typeof(SDataQueryProvider<>).MakeGenericType(entityIntfType);
            return Activator.CreateInstance(providerType, _sdataContractUrl, _userName, _password) as ISDataCrudProvider;
        }

        private static Type GetEntityInterfaceTypeFromInstance(IPersistentEntity entity)
        {
            return entity
                    .GetType()
                    .GetInterfaces()
                    .FirstOrDefault(type => type.FullName.StartsWith("Sage.Entity.Interfaces."));
        }
    }
}