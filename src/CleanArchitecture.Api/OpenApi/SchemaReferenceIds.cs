using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;

namespace CleanArchitecture.Api.OpenApi;

/// <summary>
/// Every endpoint nests its contract as <c>Request</c>/<c>Response</c>; the default id is the short type name,
/// so all of them would collapse into one schema. Nested API types are prefixed with feature and endpoint
/// (<c>Users.Login.Request</c> becomes <c>UsersLoginRequest</c>).
/// </summary>
internal static class SchemaReferenceIds
{
    public static string? Create(JsonTypeInfo typeInfo)
    {
        Type type = typeInfo.Type;

        if (type is not { IsNested: true, DeclaringType: { } endpoint } || type.Assembly != typeof(SchemaReferenceIds).Assembly)
        {
            return OpenApiOptions.CreateDefaultSchemaReferenceId(typeInfo);
        }

        string feature = endpoint.Namespace?[(endpoint.Namespace.LastIndexOf('.') + 1)..] ?? string.Empty;

        return $"{feature}{endpoint.Name}{type.Name}";
    }
}
