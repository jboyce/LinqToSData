using Sage.Platform.Orm.Interfaces;

namespace SDataLinqProvider
{
    public interface ISDataCrudProvider
    {
        void Insert(IPersistentEntity entity);
        void Update(IPersistentEntity entity);
        void Delete(IPersistentEntity entity);
    }
}