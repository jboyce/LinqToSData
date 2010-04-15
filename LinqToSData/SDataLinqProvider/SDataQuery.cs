using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SDataLinqProvider
{
    public class SDataQuery<TReturnElement, TEntity> : IQueryable<TReturnElement>, IQueryable, IEnumerable<TReturnElement>, IEnumerable, IOrderedQueryable<TReturnElement>, IOrderedQueryable
    {
        protected SDataQueryProvider<TEntity> _provider;
        protected Expression _expression;
        //private List<string> _includeNames = new List<string>();

        public SDataQuery(SDataQueryProvider<TEntity> provider)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");

            _provider = provider;
            _expression = Expression.Constant(this);
        }

        public SDataQuery(SDataQueryProvider<TEntity> provider, Expression expression)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");
            
            if (expression == null)
                throw new ArgumentNullException("expression");
            
            if (!typeof(IQueryable<TReturnElement>).IsAssignableFrom(expression.Type))
                throw new ArgumentOutOfRangeException("expression");
            
            _provider = provider;
            _expression = expression;
        }

        Expression IQueryable.Expression
        {
            get { return _expression; }
        }

        Type IQueryable.ElementType
        {
            get { return typeof(TReturnElement); }
        }

        IQueryProvider IQueryable.Provider
        {
            get { return _provider; }
        }

        public IEnumerator<TReturnElement> GetEnumerator()
        {
            return _provider.Execute<TReturnElement>(_expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_provider.Execute(_expression)).GetEnumerator();
        }

        public override string ToString()
        {
            return _provider.GetQueryText(_expression);
        }

        public SDataQuery<TReturnElement, TEntity> Include(Expression<Func<TReturnElement, object>> propertyExpression)
        {
            MemberExpression memExpression = propertyExpression.Body as MemberExpression;
            if (memExpression == null)
                throw new Exception("Include expression does not refer to a member on the entity.");

            if (memExpression.Member.MemberType != MemberTypes.Property)
                throw new Exception("Include expression does not refer to a property on the entity.");

            //_includeNames.Add(memExpression.Member.Name);
            _provider.IncludeNames.Add(memExpression.Member.Name);

            return this;
        }
    }
}