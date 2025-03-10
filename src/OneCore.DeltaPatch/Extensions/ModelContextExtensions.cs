using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations.Schema;

namespace OneCore.ModelPatch.Extensions;

public static class ModelContextExtensions
{
    public static DataList<ModelContext, Delta> GetChildren(this ModelContext context, Delta delta)
    {
        var data = new DataList<ModelContext, Delta>();
        var children = context.Properties.Where(p => p.Value.FPType.Implements(Types.CollectionT) && delta.ContainsKey(p.Key));
        foreach (var child in children)
        {
            var content = delta.Get(child.Key);
            if (content is JArray array)
            {
                var type = MetaType.GetUnderlyingType(child.Value.FPType);
                var key = new ModelContext(type, FindInverseProperty(child.Value, type));
                foreach (var entry in array)
                {
                    var inner = entry.ToObject<Delta>();
                    if (inner is not null)
                        data.Add(key, inner);
                }
            }
        }
        return data;

        ModelLink FindInverseProperty(Metadata meta, Type modelType)
        {
            string? childLink = null;
            var parentLink = context.Keys.FirstOrDefault();
            var metas = MetaType.GetMetadatas(modelType);
            var inverse = meta.GetCustomAttribute<InversePropertyAttribute>();
            if (inverse is not null)
            {
                var other = metas.FirstOrDefault(p => p.Name.Matches(inverse.Property));
                var fk = other.GetCustomAttribute<ForeignKeyAttribute>();
                if (fk is not null)
                    childLink = fk.Name;
            }
            if (childLink is null)
            {
                var name = context.Type.Name;
                var test = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { $"{name}Id", $"{name}Key" };
                childLink = metas.FirstOrDefault(p => test.Contains(p.Name)).Name;
            }

            return new ModelLink(parentLink ?? "", childLink);
        }
    }

    public static IList<Metadata> GetPrimaryKeys(this ModelContext context) => context.Keys.Select(p => context.Properties.Get(p))
              .ToList(p => p != Metadata.Empty);

    public static IResult<Expression<Func<T, bool>>> GetPrimaryKeysExpression<T>(this ModelContext context, Delta model)
    {
        if (context.IsValid)
        {
            var param = Expression.Parameter(typeof(T), "instance");
            var body = context.GetPrimaryKeys()
                .ToData(p => p.Name, GetValue)
                .Aggregate((Expression?)null, BuildExpression);
            var lambda = body is not null ? Expression.Lambda<Func<T, bool>>(body, param) : null;
            return new Result<Expression<Func<T, bool>>>(lambda, true);

            object? GetValue(Metadata p)
            {
                var ovalue = model.Get(p.Name);
                var value = Types.Parse(p.FPType, ovalue);
                return value.Model ?? p.FPType.GetDefault();
            }
            Expression BuildExpression(Expression? next, KeyValuePair<string, object?> kp)
            {
                var member = Expression.Property(param, kp.Key);
                var constant = Expression.Constant(kp.Value);
                var expression = Expression.Equal(member, constant);
                return next is null ? expression : Expression.AndAlso(next, expression);
            }
        }

        return new Result<Expression<Func<T, bool>>>(ResultType.Fail, $"{typeof(T).FullName} must have at least one Key property");
    }
}