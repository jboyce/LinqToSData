using System.Linq.Expressions;
using System.Text;

namespace SDataLinqProvider
{
    internal class SDataPropertyProjection
    {
        internal string Properties;
        internal Expression Selector;
    }

    internal class SDataPropertyProjector : ExpressionVisitor
    {
        StringBuilder _sb;

        internal SDataPropertyProjection ProjectProperties(Expression expression)
        {
            _sb = new StringBuilder();
            Expression selector = Visit(expression);
            return new SDataPropertyProjection { Properties = _sb.ToString(), Selector = selector };
        }

        protected override Expression VisitMemberAccess(MemberExpression memberExpression)
        {
            if (memberExpression.Expression != null && memberExpression.Expression.NodeType == ExpressionType.Parameter)
            {
                if (_sb.Length > 0)
                {
                    _sb.Append(",");
                }
                _sb.Append(memberExpression.Member.Name);
                return base.VisitMemberAccess(memberExpression);
            }
            else
            {
                return base.VisitMemberAccess(memberExpression);
            }
        }
    }
}