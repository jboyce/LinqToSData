using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace SDataLinqProvider
{
    public class SDataQuery<T> : Query<T>
    {
        private List<string> _includeNames = new List<string>();

        public SDataQuery(QueryProvider provider) : base(provider)
        {
        }

        public SDataQuery(QueryProvider provider, Expression expression) : base(provider, expression)
        {
        }

        public SDataQuery<T> Include(Expression<Func<T, object>> propertyExpression)
        {
            MemberExpression memExpression = propertyExpression.Body as MemberExpression;
            if (memExpression == null)
                throw new Exception("Include expression does not refer to a member on the entity.");

            if (memExpression.Member.MemberType != MemberTypes.Property)
                throw new Exception("Include expression does not refer to a property on the entity.");

            _includeNames.Add(memExpression.Member.Name);
            ((SDataQueryProvider<T>)provider).IncludeNames.Add(memExpression.Member.Name);

            return this;
        }
    }
}