using System.Linq.Expressions;
using System.Reflection;
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
            Expression selector = this.Visit(expression);
            return new SDataPropertyProjection { Properties = _sb.ToString(), Selector = selector };
        }

        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter)
            {
                if (_sb.Length > 0)
                {
                    _sb.Append(",");
                }
                _sb.Append(m.Member.Name);
                return base.VisitMemberAccess(m);
            }
            else
            {
                return base.VisitMemberAccess(m);
            }
        }
    }
}