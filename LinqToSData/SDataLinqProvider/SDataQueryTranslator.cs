using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Sage.Integration.Messaging.Model;

namespace SDataLinqProvider
{
    internal class TranslateResult
    {
        internal string QueryText;
        internal Expression Projector;
    }

    public class SDataQueryTranslator : ExpressionVisitor
    {
        private StringBuilder _sb;
        private readonly string _sdataContractUrl;
        private Dictionary<Type, string> _resourceKindMappings;
        private Expression _projector;
        private bool _hasQueryParameters = false;
        private readonly Type _entityType;

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

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpr)
        {
            foreach (var argument in methodCallExpr.Arguments.OfType<MethodCallExpression>())
            {
                VisitMethodCall(argument);
            }

            if (methodCallExpr.Method.DeclaringType == typeof(Queryable))
            {
                if (!_hasQueryParameters)
                {
                    _sb.Append("?");
                    _hasQueryParameters = true;
                }
                else
                    _sb.Append("&");

                if (methodCallExpr.Method.Name == "Where")
                {
                    _sb.Append("where=");
                    Visit(methodCallExpr.Arguments[0]);
                    LambdaExpression lambda = (LambdaExpression) StripQuotes(methodCallExpr.Arguments[1]);
                    Visit(lambda.Body);
                    return methodCallExpr;
                }
                else if (methodCallExpr.Method.Name == "Select")
                {
                    LambdaExpression lambda = (LambdaExpression)StripQuotes(methodCallExpr.Arguments[1]);
                    _projector = lambda;
                    SDataPropertyProjection projection = new SDataPropertyProjector()
                        .ProjectProperties(lambda.Body);
                    _sb.Append("select=");
                    _sb.Append(projection.Properties);
                    return methodCallExpr;
                }
            }
            throw new NotSupportedException(string.Format("The method '{0}' is not supported", methodCallExpr.Method.Name));
        }

        protected override Expression VisitBinary(BinaryExpression binaryExpr)
        {
            _sb.Append("(");
            Visit(binaryExpr.Left);
            switch (binaryExpr.NodeType)
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
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", binaryExpr.NodeType));
            }
            Visit(binaryExpr.Right);
            _sb.Append(")");
            return binaryExpr;
        }

        protected override Expression VisitConstant(ConstantExpression constantExpr)
        {
            IQueryable q = constantExpr.Value as IQueryable;
            if (q != null)
            {
                return constantExpr;
            }
            
            if (constantExpr.Value == null)
            {
                _sb.Append("NULL");
            }
            else
            {
                switch (Type.GetTypeCode(constantExpr.Value.GetType()))
                {
                    case TypeCode.Boolean:
                        _sb.Append(((bool)constantExpr.Value) ? 1 : 0);
                        break;
                    case TypeCode.String:
                        _sb.Append("'");
                        _sb.Append(constantExpr.Value);
                        _sb.Append("'");
                        break;
                    case TypeCode.Object:
                        throw new NotSupportedException(string.Format("The constant for '{0}' is not supported", constantExpr.Value));
                    default:
                        _sb.Append(constantExpr.Value);
                        break;
                }
            }
            return constantExpr;
        }

        protected override Expression VisitMemberAccess(MemberExpression memberExpr)
        {
            if (memberExpr.Expression != null && memberExpr.Expression.NodeType == ExpressionType.Parameter)
            {
                _sb.Append(memberExpr.Member.Name);
                return memberExpr;
            }
            throw new NotSupportedException(string.Format("The member '{0}' is not supported", memberExpr.Member.Name));
        }

        private static Expression StripQuotes(Expression expression)
        {
            while (expression.NodeType == ExpressionType.Quote)
            {
                expression = ((UnaryExpression)expression).Operand;
            }
            return expression;
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
            Assembly assembly = Assembly.Load("Sage.SData.Client.Entities");
            _resourceKindMappings = assembly.GetTypes().ToDictionary(
                type => GetEntityInterfaceFromType(type),
                type => GetResourcePath(type));
        }
        
        private Type GetEntityInterfaceFromType(Type type)
        {
            return type.GetInterfaces().First(intf => intf.FullName.Contains("Sage.Entity.Interfaces"));
        }

        private string GetResourcePath(Type type)
        {
            var attrib = Attribute.GetCustomAttribute(type, typeof(RequestPathAttribute)) as RequestPathAttribute;
            return attrib.Path;
        }
    }
}