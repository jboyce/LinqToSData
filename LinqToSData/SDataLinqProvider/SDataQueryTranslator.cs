using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Sage.Integration.Messaging.Model;

namespace SDataLinqProvider
{
    public abstract class SDataProjectionRow
    {
        public abstract object GetValue(int index);
    }

    internal class TranslateResult
    {
        internal string QueryText;
        internal Expression Projector;
    }

    public class SDataQueryTranslator : ExpressionVisitor
    {
        private StringBuilder _sb;
        private string _sdataContractUrl;
        private Dictionary<Type, string> _resourceKindMappings;
        private Expression _projector;
        private bool _hasQueryParameters = false;
        private Type _entityType;

        public SDataQueryTranslator(string sdataContractUrl, Type entityType)
        {
            _sdataContractUrl = sdataContractUrl;
            _entityType = entityType;
        }

        internal TranslateResult Translate(Expression expression, IEnumerable<string> includeNames)
        {
            _sb = new StringBuilder(_sdataContractUrl + "/-/" + ResourceKindMappings[_entityType]);
            Visit(expression);

            AddIncludes(includeNames);
            return new TranslateResult() { Projector = _projector, QueryText = _sb.ToString() };
        }

        internal string IdToQueryText(string entityId)
        {
            return GetResourceUrl() + "('" + entityId + "')";    
        }

        internal string GetResourceUrl()
        {
            return _sdataContractUrl + "/-/" + ResourceKindMappings[_entityType];                
        }

        private void AddIncludes(IEnumerable<string> includeNames)
        {
            if (includeNames == null || includeNames.Count() == 0)
                return;

            _sb.Append("&include=" + string.Join(",", includeNames.ToArray()));
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            foreach (var argument in m.Arguments.OfType<MethodCallExpression>())
            {
                VisitMethodCall(argument);
            }

            if (m.Method.DeclaringType == typeof(Queryable))
            {
                if (!_hasQueryParameters)
                {
                    _sb.Append("?");
                    _hasQueryParameters = true;
                }
                else
                    _sb.Append("&");

                if (m.Method.Name == "Where")
                {
                    _sb.Append("where=");
                    this.Visit(m.Arguments[0]);
                    LambdaExpression lambda = (LambdaExpression) StripQuotes(m.Arguments[1]);
                    this.Visit(lambda.Body);
                    return m;
                }
                else if (m.Method.Name == "Select")
                {
                    LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                    _projector = lambda;
                    SDataPropertyProjection projection = new SDataPropertyProjector()
                        .ProjectProperties(lambda.Body);
                    _sb.Append("select=");
                    _sb.Append(projection.Properties);
                    return m;
                }
            }
            throw new NotSupportedException(string.Format("The method '{0}' is not supported", m.Method.Name));
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            _sb.Append("(");
            this.Visit(b.Left);
            switch (b.NodeType)
            {
                case ExpressionType.And:
                    _sb.Append(" and ");
                    break;
                case ExpressionType.AndAlso:
                    _sb.Append(" and ");
                    break;
                case ExpressionType.Or:
                    _sb.Append(" or ");
                    break;
                case ExpressionType.Equal:
                    _sb.Append(" eq ");
                    break;
                case ExpressionType.NotEqual:
                    _sb.Append(" ne ");
                    break;
                case ExpressionType.LessThan:
                    _sb.Append(" lt ");
                    break;
                case ExpressionType.LessThanOrEqual:
                    _sb.Append(" le ");
                    break;
                case ExpressionType.GreaterThan:
                    _sb.Append(" gt ");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    _sb.Append(" ge ");
                    break;
                default:
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));
            }
            this.Visit(b.Right);
            _sb.Append(")");
            return b;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            IQueryable q = c.Value as IQueryable;
            if (q != null)
            {
                // assume constant nodes w/ IQueryables are table references
                //_sb.Append(q.ElementType.Name);
            }
            else if (c.Value == null)
            {
                _sb.Append("NULL");
            }
            else
            {
                switch (Type.GetTypeCode(c.Value.GetType()))
                {
                    case TypeCode.Boolean:
                        _sb.Append(((bool)c.Value) ? 1 : 0);
                        break;
                    case TypeCode.String:
                        _sb.Append("'");
                        _sb.Append(c.Value);
                        _sb.Append("'");
                        break;
                    case TypeCode.Object:
                        throw new NotSupportedException(string.Format("The constant for '{0}' is not supported", c.Value));
                    default:
                        _sb.Append(c.Value);
                        break;
                }
            }
            return c;
        }

        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter)
            {
                _sb.Append(m.Member.Name);
                return m;
            }
            throw new NotSupportedException(string.Format("The member '{0}' is not supported", m.Member.Name));
        }

        private static Expression StripQuotes(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
            {
                e = ((UnaryExpression)e).Operand;
            }
            return e;
        }

        internal Dictionary<Type, string> ResourceKindMappings
        {
            get
            {
                if (_resourceKindMappings == null)
                    BuildupResourceKindMappings();
                return _resourceKindMappings;
            }
        }

        private void BuildupResourceKindMappings()
        {
            BuildupResourceKindMappings752();
        }

        private void BuildupResourceKindMappingsForAugusta()
        {
            Assembly assembly = Assembly.Load("Sage.Integration.Entity.Feeds");
            var requestTypes = assembly.GetTypes().Where(type => type.BaseType.Name.StartsWith("RequestHandlerBase"));
            _resourceKindMappings = requestTypes.ToDictionary(
                type => type.BaseType.GetGenericArguments()[2],
                type => GetResourcePath(type));
        }

        private void BuildupResourceKindMappings752()
        {
            Assembly assembly = Assembly.Load("Sage.Integration.Entity.Feeds");
            var requestTypes = assembly.GetTypes().Where(type => type.BaseType.Name.StartsWith("DynamicRequestBase"));
            _resourceKindMappings = requestTypes.ToDictionary(
                type => type.BaseType.GetGenericArguments()[2],
                type => GetResourcePath(type));
        }

        private string GetResourcePath(Type type)
        {
            var attrib = Attribute.GetCustomAttribute(type, typeof(RequestPathAttribute)) as RequestPathAttribute;
            return attrib.Path;
        }
    }
}