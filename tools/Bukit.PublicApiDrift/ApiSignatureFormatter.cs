using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Bukit.PublicApiDrift;

internal static class ApiSignatureFormatter
{
    private const BindingFlags DeclaredMembers = BindingFlags.DeclaredOnly | BindingFlags.Instance |
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static string FormatType(Type type)
    {
        var parts = new List<string> { FormatTypeVisibility(type) };
        if (type.IsAbstract && type.IsSealed) parts.Add("static");
        else
        {
            if (type.IsAbstract && !type.IsInterface) parts.Add("abstract");
            if (type.IsSealed && !type.IsValueType && !IsDelegate(type)) parts.Add("sealed");
        }

        parts.Add(FormatTypeKind(type));
        parts.Add(FormatDeclaredTypeName(type));

        if (type.IsEnum)
        {
            parts.Add(":");
            parts.Add(FormatTypeName(Enum.GetUnderlyingType(type)));
        }
        else
        {
            var bases = new List<string>();
            if (type.BaseType is not null && type.BaseType != typeof(object) &&
                type.BaseType != typeof(ValueType) && type.BaseType != typeof(Enum) &&
                type.BaseType != typeof(MulticastDelegate))
                bases.Add(FormatTypeName(type.BaseType));
            bases.AddRange(GetDeclaredInterfaces(type).Select(static item => FormatTypeName(item)));
            if (bases.Count > 0)
            {
                parts.Add(":");
                parts.Add(string.Join(", ", bases.OrderBy(static item => item, StringComparer.Ordinal)));
            }
        }

        var constraints = type.IsGenericTypeDefinition
            ? type.GetGenericArguments().Select(FormatGenericConstraints).Where(static item => item.Length > 0)
            : [];
        return string.Join(" ", parts) + JoinConstraints(constraints);
    }

    public static IReadOnlyList<string> FormatPublicMembers(Type type) =>
        FormatMembers(type, IsPublic, static field => field.IsPublic);

    public static IReadOnlyList<string> FormatProtectedMembers(Type type) =>
        FormatMembers(type, IsProtected, static field => field.IsFamily || field.IsFamilyOrAssembly);

    private static IReadOnlyList<string> FormatMembers(
        Type type,
        Func<MethodBase, bool> includeMethod,
        Func<FieldInfo, bool> includeField)
    {
        var nullability = new NullabilityInfoContext();
        var members = new List<string>();

        members.AddRange(type.GetConstructors(DeclaredMembers).Where(includeMethod)
            .Select(item => FormatMethod(item, nullability)));
        members.AddRange(type.GetMethods(DeclaredMembers)
            .Where(item => includeMethod(item) && !IsPropertyOrEventAccessor(item))
            .Select(item => FormatMethod(item, nullability)));
        members.AddRange(type.GetProperties(DeclaredMembers)
            .Where(item => item.GetAccessors(nonPublic: true).Any(includeMethod))
            .Select(item => FormatProperty(item, nullability, includeMethod)));
        members.AddRange(type.GetFields(DeclaredMembers)
            .Where(includeField)
            .Select(item => FormatField(item, nullability)));
        members.AddRange(type.GetEvents(DeclaredMembers)
            .Where(item => GetEventAccessors(item).Any(includeMethod))
            .Select(item => FormatEvent(item, nullability, includeMethod)));

        return members.Distinct(StringComparer.Ordinal).OrderBy(static item => item, StringComparer.Ordinal).ToArray();
    }

    private static string FormatMethod(MethodBase method, NullabilityInfoContext nullability)
    {
        var parts = new List<string> { FormatAccessibility(method) };
        if (method.IsStatic) parts.Add("static");
        if (method.IsAbstract) parts.Add("abstract");
        else if (method.IsVirtual) parts.Add("virtual");
        if (method.IsFinal) parts.Add("final");

        var methodInfo = method as MethodInfo;
        if (methodInfo is not null)
            parts.Add(FormatParameterType(methodInfo.ReturnParameter, nullability.Create(methodInfo.ReturnParameter)));

        var name = method.IsConstructor ? ".ctor" : method.Name;
        if (method.IsGenericMethodDefinition)
            name += "<" + string.Join(", ", method.GetGenericArguments().Select(FormatGenericParameter)) + ">";

        var parameters = method.GetParameters().Select(parameter => FormatParameter(parameter, nullability.Create(parameter)));
        var result = string.Join(" ", parts) + " " + name + "(" + string.Join(", ", parameters) + ")";
        if (method.IsGenericMethodDefinition)
            result += JoinConstraints(method.GetGenericArguments().Select(FormatGenericConstraints).Where(static item => item.Length > 0));
        return result;
    }

    private static string FormatProperty(PropertyInfo property, NullabilityInfoContext nullability)
        => FormatProperty(property, nullability, IsPublicOrProtected);

    private static string FormatProperty(
        PropertyInfo property,
        NullabilityInfoContext nullability,
        Func<MethodBase, bool> includeMethod)
    {
        var accessors = property.GetAccessors(nonPublic: true).Where(item => includeMethod(item)).ToArray();
        var parts = new List<string> { FormatAccessibility(accessors) };
        AddAccessorState(parts, accessors);
        parts.Add(FormatTypeName(property.PropertyType, nullability.Create(property)));

        var index = property.GetIndexParameters();
        var name = index.Length == 0
            ? property.Name
            : property.Name + "[" + string.Join(", ", index.Select(item => FormatParameter(item, nullability.Create(item)))) + "]";
        var body = new List<string>();
        var getter = property.GetGetMethod(nonPublic: true);
        var setter = property.GetSetMethod(nonPublic: true);
        if (getter is not null && accessors.Contains(getter)) body.Add(FormatAccessor("get", getter, accessors));
        if (setter is not null && accessors.Contains(setter)) body.Add(FormatAccessor(IsInitOnly(setter) ? "init" : "set", setter, accessors));
        return string.Join(" ", parts) + " " + name + " { " + string.Join(" ", body) + " }";
    }

    private static string FormatField(FieldInfo field, NullabilityInfoContext nullability)
    {
        var parts = new List<string> { FormatAccessibility(field) };
        if (field.IsLiteral) parts.Add("const");
        else
        {
            if (field.IsStatic) parts.Add("static");
            if (field.IsInitOnly) parts.Add("readonly");
        }
        parts.Add(FormatTypeName(field.FieldType, nullability.Create(field)));
        parts.Add(field.Name);
        if (field.IsLiteral)
        {
            var value = field.DeclaringType?.IsEnum == true
                ? FormatTypeName(field.DeclaringType) + "." + field.Name
                : FormatDefault(field.GetRawConstantValue());
            parts.Add("= " + value);
        }
        return string.Join(" ", parts);
    }

    private static string FormatEvent(EventInfo @event, NullabilityInfoContext nullability)
        => FormatEvent(@event, nullability, IsPublicOrProtected);

    private static string FormatEvent(
        EventInfo @event,
        NullabilityInfoContext nullability,
        Func<MethodBase, bool> includeMethod)
    {
        var accessors = GetEventAccessors(@event).Where(item => includeMethod(item)).ToArray();
        var parts = new List<string> { FormatAccessibility(accessors) };
        AddAccessorState(parts, accessors);
        parts.Add("event");
        parts.Add(FormatTypeName(@event.EventHandlerType ?? typeof(void), nullability.Create(@event)));
        parts.Add(@event.Name);

        var body = accessors.Select(item => FormatAccessor(item == @event.GetAddMethod(true) ? "add" : "remove", item, accessors));
        return string.Join(" ", parts) + " { " + string.Join(" ", body) + " }";
    }

    private static string FormatTypeName(Type type, NullabilityInfo? nullability = null)
    {
        if (type.IsByRef)
            return FormatTypeName(type.GetElementType()!, nullability?.ElementType ?? nullability);
        if (type.IsPointer)
            return FormatTypeName(type.GetElementType()!, nullability?.ElementType) + "*";
        if (type.IsArray)
        {
            var commas = new string(',', type.GetArrayRank() - 1);
            return FormatTypeName(type.GetElementType()!, nullability?.ElementType) + "[" + commas + "]" + NullabilitySuffix(type, nullability);
        }
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return FormatTypeName(underlying, nullability?.GenericTypeArguments.FirstOrDefault()) + "?";
        if (type.IsGenericParameter)
            return type.Name + NullabilitySuffix(type, nullability);

        string name;
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var baseName = RemoveGenericArity(definition.FullName ?? definition.Name).Replace('+', '.');
            var nullableArguments = nullability?.GenericTypeArguments ?? [];
            var arguments = type.GetGenericArguments().Select((argument, index) =>
                FormatTypeName(argument, index < nullableArguments.Length ? nullableArguments[index] : null));
            name = baseName + "<" + string.Join(", ", arguments) + ">";
        }
        else
        {
            name = (type.FullName ?? type.Name).Replace('+', '.');
        }
        return name + NullabilitySuffix(type, nullability);
    }

    private static string FormatGenericConstraints(Type parameter)
    {
        var constraints = new List<string>();
        var attributes = parameter.GenericParameterAttributes;
        if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0) constraints.Add("class");
        if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) constraints.Add("struct");
        constraints.AddRange(parameter.GetGenericParameterConstraints()
            .Where(static item => item != typeof(ValueType))
            .Select(static item => FormatTypeName(item))
            .OrderBy(static item => item, StringComparer.Ordinal));
        if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0 &&
            (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0)
            constraints.Add("new()");
        return constraints.Count == 0 ? string.Empty : $"where {parameter.Name} : {string.Join(", ", constraints)}";
    }

    private static string FormatDefault(object? value)
    {
        if (value is null) return "null";
        if (value == Missing.Value || value == DBNull.Value) return "missing";
        if (value is string text) return JsonSerializer.Serialize(text);
        if (value is char character) return "'" + EscapeChar(character) + "'";
        if (value is bool boolean) return boolean ? "true" : "false";
        if (value.GetType().IsEnum)
        {
            var enumType = value.GetType();
            var enumName = Enum.GetName(enumType, value);
            return enumName is null
                ? $"({FormatTypeName(enumType)}){Convert.ToString(value, CultureInfo.InvariantCulture)}"
                : $"{FormatTypeName(enumType)}.{enumName}";
        }
        if (value is float single) return single.ToString("R", CultureInfo.InvariantCulture);
        if (value is double @double) return @double.ToString("R", CultureInfo.InvariantCulture);
        if (value is decimal decimalValue) return decimalValue.ToString(CultureInfo.InvariantCulture);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
    }

    private static string FormatParameter(ParameterInfo parameter, NullabilityInfo nullability)
    {
        var prefix = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef && parameter.IsIn ? "in " :
            parameter.ParameterType.IsByRef ? "ref " : string.Empty;
        var result = prefix + FormatTypeName(parameter.ParameterType, nullability) + " " + (parameter.Name ?? "_");
        if (parameter.HasDefaultValue) result += " = " + FormatDefault(parameter.DefaultValue);
        else if (parameter.IsOptional) result += " = missing";
        return result;
    }

    private static string FormatParameterType(ParameterInfo parameter, NullabilityInfo nullability)
    {
        var prefix = parameter.ParameterType.IsByRef ? parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref " : string.Empty;
        return prefix + FormatTypeName(parameter.ParameterType, nullability);
    }

    private static IEnumerable<Type> GetDeclaredInterfaces(Type type)
    {
        var inherited = type.BaseType?.GetInterfaces() ?? [];
        if (type.IsInterface)
            inherited = type.GetInterfaces().SelectMany(static item => item.GetInterfaces()).ToArray();
        return type.GetInterfaces().Except(inherited).Distinct();
    }

    private static IEnumerable<MethodInfo> GetEventAccessors(EventInfo @event)
    {
        if (@event.GetAddMethod(true) is { } add) yield return add;
        if (@event.GetRemoveMethod(true) is { } remove) yield return remove;
        if (@event.GetRaiseMethod(true) is { } raise) yield return raise;
    }

    private static bool IsPropertyOrEventAccessor(MethodInfo method) => method.IsSpecialName &&
        (method.Name.StartsWith("get_", StringComparison.Ordinal) || method.Name.StartsWith("set_", StringComparison.Ordinal) ||
         method.Name.StartsWith("add_", StringComparison.Ordinal) || method.Name.StartsWith("remove_", StringComparison.Ordinal) ||
         method.Name.StartsWith("raise_", StringComparison.Ordinal));

    private static bool IsPublic(MethodBase method) => method.IsPublic;
    private static bool IsProtected(MethodBase method) => method.IsFamily || method.IsFamilyOrAssembly;
    private static bool IsPublicOrProtected(MethodBase method) => IsPublic(method) || IsProtected(method);

    private static string FormatAccessibility(MethodBase method) => method.IsPublic ? "public" :
        method.IsFamilyOrAssembly ? "protected internal" : method.IsFamily ? "protected" :
        method.IsFamilyAndAssembly ? "private protected" : method.IsAssembly ? "internal" : "private";

    private static string FormatAccessibility(FieldInfo field) => field.IsPublic ? "public" :
        field.IsFamilyOrAssembly ? "protected internal" : field.IsFamily ? "protected" :
        field.IsFamilyAndAssembly ? "private protected" : field.IsAssembly ? "internal" : "private";

    private static string FormatAccessibility(IReadOnlyList<MethodInfo> methods) =>
        methods.Any(static item => item.IsPublic) ? "public" :
        methods.Any(static item => item.IsFamilyOrAssembly) ? "protected internal" : "protected";

    private static string FormatAccessor(string name, MethodInfo accessor, IReadOnlyList<MethodInfo> accessors)
    {
        var overall = FormatAccessibility(accessors);
        var accessibility = FormatAccessibility(accessor);
        return (StringComparer.Ordinal.Equals(overall, accessibility) ? string.Empty : accessibility + " ") + name + ";";
    }

    private static void AddAccessorState(List<string> parts, IReadOnlyList<MethodInfo> accessors)
    {
        if (accessors.Any(static item => item.IsStatic)) parts.Add("static");
        if (accessors.Any(static item => item.IsAbstract)) parts.Add("abstract");
        else if (accessors.Any(static item => item.IsVirtual)) parts.Add("virtual");
        if (accessors.Any(static item => item.IsFinal)) parts.Add("final");
    }

    private static bool IsInitOnly(MethodInfo setter) => setter.ReturnParameter.GetRequiredCustomModifiers()
        .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));

    private static string FormatTypeVisibility(Type type) => type.IsNested
        ? type.IsNestedPublic ? "public" : type.IsNestedFamORAssem ? "protected internal" : type.IsNestedFamily ? "protected" :
          type.IsNestedFamANDAssem ? "private protected" : type.IsNestedAssembly ? "internal" : "private"
        : type.IsPublic ? "public" : "internal";

    private static string FormatTypeKind(Type type) => type.IsEnum ? "enum" : IsDelegate(type) ? "delegate" :
        type.IsInterface ? "interface" : type.IsValueType ? "struct" : "class";

    private static bool IsDelegate(Type type) => typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);

    private static string FormatDeclaredTypeName(Type type)
    {
        var name = RemoveGenericArity(type.FullName ?? type.Name).Replace('+', '.');
        if (!type.IsGenericTypeDefinition) return name;
        var parameters = type.GetGenericArguments();
        return name + "<" + string.Join(", ", parameters.Select(FormatGenericParameter)) + ">";
    }

    private static string FormatGenericParameter(Type parameter)
    {
        var variance = parameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;
        return variance == GenericParameterAttributes.Covariant ? "out " + parameter.Name :
            variance == GenericParameterAttributes.Contravariant ? "in " + parameter.Name : parameter.Name;
    }

    private static string RemoveGenericArity(string name)
    {
        var builder = new StringBuilder(name.Length);
        for (var index = 0; index < name.Length; index++)
        {
            if (name[index] != '`')
            {
                builder.Append(name[index]);
                continue;
            }
            while (index + 1 < name.Length && char.IsDigit(name[index + 1])) index++;
        }
        return builder.ToString();
    }

    private static string NullabilitySuffix(Type type, NullabilityInfo? nullability)
    {
        if (type.IsValueType && !type.IsGenericParameter) return string.Empty;
        return nullability?.ReadState switch
        {
            NullabilityState.Nullable => "?",
            NullabilityState.NotNull => "!",
            _ => "~"
        };
    }

    private static string JoinConstraints(IEnumerable<string> constraints)
    {
        var items = constraints.ToArray();
        return items.Length == 0 ? string.Empty : " " + string.Join(" ", items);
    }

    private static string EscapeChar(char value) => value switch
    {
        '\\' => "\\\\",
        '\'' => "\\'",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        '\0' => "\\0",
        _ when char.IsControl(value) => $"\\u{(int)value:x4}",
        _ => value.ToString()
    };

}
