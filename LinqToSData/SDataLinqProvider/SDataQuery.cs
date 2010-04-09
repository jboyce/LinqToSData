using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SDataLinqProvider
{
    public class SDataQuery<T> : IQueryable<T>, IQueryable, IEnumerable<T>, IEnumerable, IOrderedQueryable<T>, IOrderedQueryable
    {
        protected SDataQueryProvider<T> provider;
        protected Expression expression;
        private List<string> _includeNames = new List<string>();

        public SDataQuery(SDataQueryProvider<T> provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }
            this.provider = provider;
            this.expression = Expression.Constant(this);
        }

        public SDataQuery(SDataQueryProvider<T> provider, Expression expression)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }
            if (!typeof(IQueryable<T>).IsAssignableFrom(expression.Type))
            {
                throw new ArgumentOutOfRangeException("expression");
            }
            this.provider = provider;
            this.expression = expression;
        }

        Expression IQueryable.Expression
        {
            get { return this.expression; }
        }

        Type IQueryable.ElementType
        {
            get { return typeof(T); }
        }

        IQueryProvider IQueryable.Provider
        {
            get { return this.provider; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return provider.Execute<T>(this.expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.provider.Execute(this.expression)).GetEnumerator();
        }

        public override string ToString()
        {
            return this.provider.GetQueryText(this.expression);
        }

        public SDataQuery<T> Include(Expression<Func<T, object>> propertyExpression)
        {
            MemberExpression memExpression = propertyExpression.Body as MemberExpression;
            if (memExpression == null)
                throw new Exception("Include expression does not refer to a member on the entity.");

            if (memExpression.Member.MemberType != MemberTypes.Property)
                throw new Exception("Include expression does not refer to a property on the entity.");

            _includeNames.Add(memExpression.Member.Name);
            provider.IncludeNames.Add(memExpression.Member.Name);

            return this;
        }
    }
}