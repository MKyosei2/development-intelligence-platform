// Purpose: Defines the DIP responsibility represented by P ro je ct Sc op eA ut ho ri za ti on.
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Projects;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Dip.Api.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequireProjectRoleAttribute(ProjectMembershipRole role, string? routeKey = null) : Attribute
{
    public ProjectMembershipRole Role { get; } = role;
    public string? RouteKey { get; } = routeKey;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequireProjectResourceAttribute(
    ProjectMembershipRole role,
    ProjectResourceKind kind,
    string routeKey) : Attribute
{
    public ProjectMembershipRole Role { get; } = role;
    public ProjectResourceKind Kind { get; } = kind;
    public string RouteKey { get; } = routeKey;
}

/// <summary>
/// Allows the small set of lifecycle operations that must remain available while
/// a project is archived. Normal project writes are rejected until the project is restored.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AllowArchivedProjectMutationAttribute : Attribute;

public static class RequestActor
{
    public static string Subject(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
              ?? throw new UnauthorizedAccessException("Authenticated subject claim is required.")
            : "local-development";

    public static string Login(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? user.FindFirst("name")?.Value ?? user.Identity?.Name ?? Subject(user)
            : "development-local";

    public static string AuditIdentity(ClaimsPrincipal user) =>
        $"{Login(user)}#{Subject(user)}";
}

/// <summary>
/// Action-level project authorization after model binding. It protects route/body
/// ProjectId values and also enforces the archived/deleted write barrier.
///
/// Public portfolio mode is deliberately narrow: only GET/HEAD endpoints carrying
/// ProjectRead are anonymously reachable. Integration/control surfaces stay private,
/// and a POST never becomes anonymous merely because an action reused ProjectRead.
/// </summary>
public sealed class ProjectScopeAuthorizationFilter(
    ProjectAccessService access,
    ProjectService projects,
    IProjectResourceProjectResolver resources,
    IConfiguration configuration) : IAsyncActionFilter
{
    private static readonly string[] InternalReadPrefixes =
    [
        "/api/v1/integrations/github",
        "/api/v1/system/status"
    ];

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            await next();
            return;
        }

        var requireAuthentication =
            configuration.GetValue<bool?>("Authentication:RequireAuthenticatedUser") ?? false;
        var publicReadOnly =
            configuration.GetValue<bool?>("PublicAccess:ReadOnly") ?? false;
        var user = context.HttpContext.User;
        var publicReadRequest = IsPublicReadRequest(context, publicReadOnly);

        if (requireAuthentication
            && user.Identity?.IsAuthenticated != true
            && !publicReadRequest)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var required = context.ActionDescriptor.EndpointMetadata
            .OfType<RequireProjectRoleAttribute>()
            .LastOrDefault();
        var indirect = context.ActionDescriptor.EndpointMetadata
            .OfType<RequireProjectResourceAttribute>()
            .LastOrDefault();
        var minimum = indirect?.Role ?? required?.Role ?? InferMinimum(context);

        Guid? projectId;
        try
        {
            projectId = ResolveProjectId(context, required?.RouteKey);
        }
        catch (InvalidOperationException exception)
        {
            context.Result = new BadRequestObjectResult(new { error = exception.Message });
            return;
        }

        if (indirect is not null)
        {
            if (!context.RouteData.Values.TryGetValue(indirect.RouteKey, out var raw)
                || !Guid.TryParse(raw?.ToString(), out var resourceId))
            {
                context.Result = new BadRequestObjectResult(new
                {
                    error = "Project-scoped resource identifier is missing."
                });
                return;
            }

            var resourceProjectId = await resources.ResolveProjectIdAsync(
                indirect.Kind,
                resourceId,
                context.HttpContext.RequestAborted);
            if (resourceProjectId is null)
            {
                context.Result = new NotFoundResult();
                return;
            }

            if (projectId.HasValue && projectId.Value != resourceProjectId.Value)
            {
                context.Result = new NotFoundResult();
                return;
            }

            projectId ??= resourceProjectId;
        }

        if (projectId is null)
        {
            await next();
            return;
        }

        // Public portfolio GET/HEAD is intentionally the same read model for every
        // viewer. For every non-public request, authenticated membership is enforced.
        if (requireAuthentication
            && !publicReadRequest
            && user.Identity?.IsAuthenticated == true
            && !user.IsInRole(Roles.Admin))
        {
            if (!await access.HasAccessAsync(
                    projectId.Value,
                    RequestActor.Subject(user),
                    minimum,
                    context.HttpContext.RequestAborted))
            {
                context.Result = new ForbidResult();
                return;
            }
        }

        if (IsProjectWrite(context)
            && !context.ActionDescriptor.EndpointMetadata
                .OfType<AllowArchivedProjectMutationAttribute>()
                .Any())
        {
            var lifecycle = await projects.GetLifecycleStateAsync(
                projectId.Value,
                context.HttpContext.RequestAborted);
            if (!lifecycle.Equals(ProjectService.ActiveState, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new ConflictObjectResult(new
                {
                    error = "project_not_active",
                    message = $"Project is {lifecycle}. Restore it before making project-scoped changes."
                });
                return;
            }
        }

        await next();
    }

    private static bool IsPublicReadRequest(ActionExecutingContext context, bool publicReadOnly)
    {
        if (!publicReadOnly)
            return false;

        var method = context.HttpContext.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
            return false;

        var path = context.HttpContext.Request.Path;
        if (InternalReadPrefixes.Any(prefix => path.StartsWithSegments(new PathString(prefix), StringComparison.OrdinalIgnoreCase)))
            return false;

        return context.ActionDescriptor.EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .Any(attribute => string.Equals(
                attribute.Policy,
                Policies.ProjectRead,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProjectWrite(ActionExecutingContext context)
        => context.ActionDescriptor.EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .Any(attribute =>
                string.Equals(attribute.Policy, Policies.ProjectWrite, StringComparison.OrdinalIgnoreCase));

    private static ProjectMembershipRole InferMinimum(ActionExecutingContext context)
    {
        var policies = context.ActionDescriptor.EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .Select(x => x.Policy)
            .Where(x => x is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return policies.Contains(Policies.ProjectWrite)
            ? ProjectMembershipRole.Member
            : ProjectMembershipRole.Viewer;
    }

    private static Guid? ResolveProjectId(ActionExecutingContext context, string? routeKey)
    {
        if (!string.IsNullOrWhiteSpace(routeKey)
            && context.RouteData.Values.TryGetValue(routeKey, out var routed)
            && Guid.TryParse(routed?.ToString(), out var explicitId))
        {
            return explicitId;
        }

        var ids = new HashSet<Guid>();
        if (context.RouteData.Values.TryGetValue("projectId", out var route)
            && Guid.TryParse(route?.ToString(), out var routeId)
            && routeId != Guid.Empty)
        {
            ids.Add(routeId);
        }

        foreach (var pair in context.ActionArguments)
        {
            if (pair.Value is Guid guid
                && string.Equals(pair.Key, "projectId", StringComparison.OrdinalIgnoreCase)
                && guid != Guid.Empty)
            {
                ids.Add(guid);
            }
            else if (pair.Value is not null)
            {
                CollectProjectIds(
                    pair.Value,
                    ids,
                    0,
                    new HashSet<object>(ReferenceEqualityComparer.Instance));
            }
        }

        if (ids.Count > 1)
            throw new InvalidOperationException("The request contains conflicting ProjectId values.");
        return ids.Count == 1 ? ids.Single() : null;
    }

    private static void CollectProjectIds(
        object value,
        ISet<Guid> ids,
        int depth,
        ISet<object> visited)
    {
        if (depth > 3
            || value is string
            || value.GetType().IsPrimitive
            || value.GetType().IsEnum)
        {
            return;
        }

        if (!value.GetType().IsValueType && !visited.Add(value))
            return;

        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is not null)
                    CollectProjectIds(item, ids, depth + 1, visited);
            }
            return;
        }

        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead)
                continue;

            object? member;
            try
            {
                member = property.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (string.Equals(property.Name, "ProjectId", StringComparison.OrdinalIgnoreCase)
                && member is Guid id
                && id != Guid.Empty)
            {
                ids.Add(id);
                continue;
            }

            if (member is not null
                && property.PropertyType != typeof(string)
                && !property.PropertyType.IsPrimitive
                && !property.PropertyType.IsEnum)
            {
                CollectProjectIds(member, ids, depth + 1, visited);
            }
        }
    }
}
