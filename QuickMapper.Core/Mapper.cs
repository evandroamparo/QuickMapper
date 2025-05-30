using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace QuickMapper.Core
{
    public interface IMapper
    {
        IMapper CreateMap<TSource, TDestination>();
        void CreateReverseMap<TSource, TDestination>();
        IMapper IgnoreProperty<TSource>(Expression<Func<TSource, object>> propertyExpression);
        IMapper AddValidator<TSource>(Func<TSource, bool> validator);
        TDestination Map<TSource, TDestination>(TSource source) where TDestination : new();
        void ForMember<TSource, TDestination, TMember>(
            Expression<Func<TDestination, TMember?>> destinationMember,
            Func<TSource, TMember?> mapFrom);
    }

    public class Mapper(ILogger<Mapper> logger) : IMapper
    {
        private readonly Dictionary<(Type, Type), Action<object, object>> _mappings = [];
        private readonly Dictionary<(Type, Type), Dictionary<string, Func<object, object>>> _memberMappings = [];
        private readonly List<Func<object, bool>> _validators = [];
        private readonly HashSet<string> _ignoredProperties = [];
        private readonly ILogger<Mapper> _logger = logger;

        public IMapper CreateMap<TSource, TDestination>()
        {
            var key = (typeof(TSource), typeof(TDestination));
            if (!_mappings.ContainsKey(key))
            {
                _mappings[key] = (src, dest) =>
                {
                    if (src == null || dest == null) return;

                    foreach (var prop in typeof(TDestination).GetProperties())
                    {
                        MapProperty(key, prop, src, dest);
                    }
                };
                _memberMappings[key] = [];
            }
            return this;
        }

        private void MapProperty((Type, Type) key, System.Reflection.PropertyInfo destProp, object src, object dest)
        {
            if (_ignoredProperties.Contains(destProp.Name))
                return;

            var sourceProp = key.Item1.GetProperty(destProp.Name);

            if (sourceProp == null && destProp.CanWrite)
            {
                if (TryCustomMapping(key, destProp.Name, src, dest, destProp))
                    return;
            }

            if (sourceProp == null || !destProp.CanWrite)
                return;

            var value = sourceProp.GetValue(src);
            if (value == null)
            {
                destProp.SetValue(dest, null);
                return;
            }

            if (TryCustomMapping(key, destProp.Name, src, dest, destProp))
                return;

            if (TryCollectionMapping(sourceProp, destProp, value, dest))
                return;

            if (TryNestedObjectMapping(sourceProp, destProp, value, dest))
                return;

            // Simple property mapping
            destProp.SetValue(dest, value);
        }

        private bool TryCustomMapping((Type, Type) key, string propName, object src, object dest, System.Reflection.PropertyInfo destProp)
        {
            if (_memberMappings.TryGetValue(key, out var value) && value.ContainsKey(propName))
            {
                var convertedValue = value[propName](src);
                destProp.SetValue(dest, convertedValue);
                return true;
            }
            return false;
        }

        private bool TryCollectionMapping(System.Reflection.PropertyInfo sourceProp, System.Reflection.PropertyInfo destProp, object value, object dest)
        {
            if (IsCollectionType(sourceProp.PropertyType) && IsCollectionType(destProp.PropertyType))
            {
                var destCollection = CreateDestinationCollection(value, destProp.PropertyType);
                destProp.SetValue(dest, destCollection);
                return true;
            }
            return false;
        }

        private bool TryNestedObjectMapping(System.Reflection.PropertyInfo sourceProp, System.Reflection.PropertyInfo destProp, object value, object dest)
        {
            if (!IsSimpleType(sourceProp.PropertyType))
            {
                var nestedKey = (sourceProp.PropertyType, destProp.PropertyType);
                if (_mappings.ContainsKey(nestedKey))
                {
                    var nestedInstance = Activator.CreateInstance(destProp.PropertyType);
                    if (nestedInstance != null)
                    {
                        _mappings[nestedKey](value, nestedInstance);
                        destProp.SetValue(dest, nestedInstance);
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsSimpleType(Type type) =>
            type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime);

        private static bool IsCollectionType(Type type) =>
            type.IsGenericType &&
            (type.GetGenericTypeDefinition() == typeof(List<>) ||
             type.GetGenericTypeDefinition() == typeof(IList<>) ||
             type.GetGenericTypeDefinition() == typeof(ICollection<>));

        private object CreateDestinationCollection(object sourceCollection, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull(sourceCollection);

            var sourceList = (System.Collections.IEnumerable)sourceCollection;
            var destListType = destinationType;

            if (destinationType.IsInterface)
            {
                destListType = typeof(List<>).MakeGenericType(destinationType.GetGenericArguments()[0]);
            }

            var destList = Activator.CreateInstance(destListType) ?? throw new InvalidOperationException($"Failed to create collection of type {destListType}");
            var typedDestList = (System.Collections.IList)destList;
            var elementType = destinationType.GetGenericArguments()[0];

            foreach (var item in sourceList)
            {
                if (item == null) continue;

                var sourceType = item.GetType();
                var itemKey = (sourceType, elementType);

                if (_mappings.ContainsKey(itemKey))
                {
                    var destItem = Activator.CreateInstance(elementType);
                    if (destItem != null)
                    {
                        _mappings[itemKey](item, destItem);
                        typedDestList.Add(destItem);
                    }
                }
                else
                {
                    typedDestList.Add(item);
                }
            }

            return typedDestList;
        }

        public void CreateReverseMap<TSource, TDestination>()
        {
            CreateMap<TSource, TDestination>();
            CreateMap<TDestination, TSource>();
        }

        public IMapper IgnoreProperty<TSource>(Expression<Func<TSource, object>> propertyExpression)
        {
            var memberExpr = propertyExpression.Body as MemberExpression
                           ?? ((UnaryExpression)propertyExpression.Body).Operand as MemberExpression;
            if (memberExpr != null)
            {
                _ignoredProperties.Add(memberExpr.Member.Name);
                _logger.LogInformation("Property {PropertyName} marked as ignored", memberExpr.Member.Name);
            }
            return this;
        }

        public IMapper AddValidator<TSource>(Func<TSource, bool> validator)
        {
            _validators.Add(src => validator((TSource)src));
            return this;
        }

        public void ForMember<TSource, TDestination, TMember>(
            Expression<Func<TDestination, TMember?>> destinationMember,
            Func<TSource, TMember?> mapFrom)
        {
            ArgumentNullException.ThrowIfNull(destinationMember);
            ArgumentNullException.ThrowIfNull(mapFrom);

            var key = (typeof(TSource), typeof(TDestination));
            if (!_memberMappings.TryGetValue(key, out var value))
            {
                value = [];
                _memberMappings[key] = value;
            }

            var memberName = ((MemberExpression)destinationMember.Body).Member.Name;
            var sourceType = typeof(TSource);
            value[memberName] = src =>
            {
                ArgumentNullException.ThrowIfNull(src);

                //                 var sourceProp = sourceType.GetProperty(memberName);
                //                 if (sourceProp != null)
                //                 {
                //                     var value = sourceProp.GetValue(src);
                // #pragma warning disable CS8603 // Possible null reference return - intentional for nullable types
                //                     return mapFrom((TSource)src);
                //                 }
#pragma warning disable CS8603 // Possible null reference return.
                return mapFrom((TSource)src);
#pragma warning restore CS8603 // Possible null reference return.
            };
        }

        public TDestination Map<TSource, TDestination>(TSource source) where TDestination : new()
        {
            if (source == null)
            {
                _logger.LogError("Source object is null");
                throw new ArgumentNullException(nameof(source));
            }

            foreach (var validator in _validators)
            {
                if (!validator(source))
                {
                    _logger.LogError("Validation failed for {SourceType}", typeof(TSource).Name);
                    throw new InvalidOperationException("Validation failed");
                }
            }

            var key = (typeof(TSource), typeof(TDestination));
            if (!_mappings.TryGetValue(key, out var mappingAction))
            {
                _logger.LogError("No mapping defined for {SourceType} to {DestType}", typeof(TSource).Name, typeof(TDestination).Name);
                throw new InvalidOperationException($"No mapping defined for {typeof(TSource)} to {typeof(TDestination)}");
            }

            var destination = new TDestination();
            mappingAction(source, destination);

            // Only apply custom mappings for non-ignored properties
            foreach (var memberMapping in _memberMappings[key])
            {
                if (!_ignoredProperties.Contains(memberMapping.Key))
                {
                    var property = typeof(TDestination).GetProperty(memberMapping.Key);
                    property?.SetValue(destination, memberMapping.Value(source));
                }
            }

            _logger.LogInformation("Successfully mapped {SourceType} to {DestType}", typeof(TSource).Name, typeof(TDestination).Name);
            return destination;
        }
    }
}
