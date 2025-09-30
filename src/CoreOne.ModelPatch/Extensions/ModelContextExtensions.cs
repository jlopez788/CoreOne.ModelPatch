using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreOne.ModelPatch.Extensions;

internal static class ModelContextExtensions
{
    private static readonly Type ICollection = typeof(ICollection);
    private static readonly Type ICollectionT = typeof(ICollection<>);

    public static DataList<ModelContext, Delta> GetChildren(this ModelContext context, ModelOptions options, Delta delta)
    {
        var data = new DataList<ModelContext, Delta>();
        var keys = new HashSet<string>(delta.Keys, MStringComparer.OrdinalIgnoreCase);
        var children = context.Properties.Where(p => keys.Contains(options.GetPreferredName(p.Value)) && IsCollectionType(p.Value.FPType));
        foreach (var child in children)
        {
            var name = options.GetPreferredName(child.Value);
            var content = delta.Get(name, () => delta.FirstOrDefault(p => p.Key.Matches(child.Key)).Value);
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

            return new ModelLink(parentLink ?? [], childLink);
        }
    }

    public static List<List<Metadata>> GetPrimaryKeys(this ModelContext context) => [.. context.Keys.Select(p => p.Select(m => context.Properties.Get(m.Name))
        .ToList(p => p != Metadata.Empty))];

    public static IResult<Expression<Func<T, bool>>> GetPrimaryKeysExpression<T>(this ModelContext context, ModelOptions options, Delta model)
    {
        if (context.IsValid)
        {
            var param = Expression.Parameter(typeof(T), "instance");
            var body = context.GetPrimaryKeys()
              .Select(entry => entry.ToDictionary(p => p.Name, GetValue))
              .Aggregate((Expression?)null, BuildDataExpression);
            var lambda = body is not null ? Expression.Lambda<Func<T, bool>>(body, param) : null;
            return new Result<Expression<Func<T, bool>>>(lambda, true);

            object? GetValue(Metadata meta)
            {
                var name = options.GetPreferredName(meta);
                var ovalue = model.Get(name);
                var value = Types.Parse(meta.FPType, ovalue);
                return value.Model ?? meta.FPType.GetDefault();
            }
            Expression? BuildDataExpression(Expression? next, Dictionary<string, object?> data)
            {
                var expression = data.Aggregate((Expression?)null, BuildExpression);
                return next is null ? expression :
                   next is not null && expression is not null ?
                   Expression.OrElse(next, expression) : next;
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

    private static bool IsCollectionType(Type? type) => type is not null && (
            (type.IsGenericType && type.GetGenericTypeDefinition() == ICollectionT) // Direct match for generic ICollection<T>
            || ICollection.IsAssignableFrom(type) // Implements non-generic ICollection
            || type.GetInterfaces() // Implements ICollection<T>
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == ICollectionT));
}