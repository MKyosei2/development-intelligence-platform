using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Dip.Api.Authorization;

[AttributeUsage(AttributeTargets.Method|AttributeTargets.Class,AllowMultiple=false)]
public sealed class RequireProjectRoleAttribute(ProjectMembershipRole role,string? routeKey=null):Attribute
{
    public ProjectMembershipRole Role{get;}=role;
    public string? RouteKey{get;}=routeKey;
}

[AttributeUsage(AttributeTargets.Method|AttributeTargets.Class,AllowMultiple=false)]
public sealed class RequireProjectResourceAttribute(ProjectMembershipRole role,ProjectResourceKind kind,string routeKey):Attribute
{
    public ProjectMembershipRole Role{get;}=role;
    public ProjectResourceKind Kind{get;}=kind;
    public string RouteKey{get;}=routeKey;
}

public static class RequestActor
{
    public static string Subject(ClaimsPrincipal user)=>user.Identity?.IsAuthenticated==true?(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value??throw new UnauthorizedAccessException("Authenticated subject claim is required.")):"local-development";
    public static string Login(ClaimsPrincipal user)=>user.Identity?.IsAuthenticated==true?(user.FindFirst("name")?.Value??user.Identity?.Name??Subject(user)):"development-local";
    public static string AuditIdentity(ClaimsPrincipal user)=>$"{Login(user)}#{Subject(user)}";
}

/// <summary>Action-level project authorization after model binding. It protects route and body ProjectId values across the API.</summary>
public sealed class ProjectScopeAuthorizationFilter(ProjectAccessService access,IProjectResourceProjectResolver resources,IConfiguration configuration):IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        if(context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()){await next();return;}
        if(!(configuration.GetValue<bool?>("Authentication:RequireAuthenticatedUser")??false)){await next();return;}
        var user=context.HttpContext.User;if(user.Identity?.IsAuthenticated!=true){context.Result=new UnauthorizedResult();return;}
        if(user.IsInRole(Roles.Admin)){await next();return;}
        var required=context.ActionDescriptor.EndpointMetadata.OfType<RequireProjectRoleAttribute>().LastOrDefault();
        var indirect=context.ActionDescriptor.EndpointMetadata.OfType<RequireProjectResourceAttribute>().LastOrDefault();
        var minimum=indirect?.Role??required?.Role??InferMinimum(context);
        Guid? projectId;
        try{projectId=ResolveProjectId(context,required?.RouteKey);}catch(InvalidOperationException ex){context.Result=new BadRequestObjectResult(new{error=ex.Message});return;}
        Guid? resourceProjectId=null;
        if(indirect is not null)
        {
            if(!context.RouteData.Values.TryGetValue(indirect.RouteKey,out var raw)||!Guid.TryParse(raw?.ToString(),out var resourceId)){context.Result=new BadRequestObjectResult(new{error="Project-scoped resource identifier is missing."});return;}
            resourceProjectId=await resources.ResolveProjectIdAsync(indirect.Kind,resourceId,context.HttpContext.RequestAborted);if(resourceProjectId is null){context.Result=new NotFoundResult();return;}
            if(projectId.HasValue&&projectId.Value!=resourceProjectId.Value){context.Result=new NotFoundResult();return;}
            projectId??=resourceProjectId;
        }
        if(projectId is null){await next();return;}
        if(!await access.HasAccessAsync(projectId.Value,RequestActor.Subject(user),minimum,context.HttpContext.RequestAborted)){context.Result=new ForbidResult();return;}
        await next();
    }
    private static ProjectMembershipRole InferMinimum(ActionExecutingContext context)
    {
        var policies=context.ActionDescriptor.EndpointMetadata.OfType<AuthorizeAttribute>().Select(x=>x.Policy).Where(x=>x is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return policies.Contains(Policies.ProjectWrite)?ProjectMembershipRole.Member:ProjectMembershipRole.Viewer;
    }
    private static Guid? ResolveProjectId(ActionExecutingContext context,string? routeKey)
    {
        if(!string.IsNullOrWhiteSpace(routeKey)&&context.RouteData.Values.TryGetValue(routeKey,out var routed)&&Guid.TryParse(routed?.ToString(),out var explicitId))return explicitId;
        var ids=new HashSet<Guid>();
        if(context.RouteData.Values.TryGetValue("projectId",out var route)&&Guid.TryParse(route?.ToString(),out var routeId)&&routeId!=Guid.Empty)ids.Add(routeId);
        foreach(var pair in context.ActionArguments)
        {
            if(pair.Value is Guid g&&string.Equals(pair.Key,"projectId",StringComparison.OrdinalIgnoreCase)&&g!=Guid.Empty)ids.Add(g);
            else if(pair.Value is not null)CollectProjectIds(pair.Value,ids,0,new HashSet<object>(ReferenceEqualityComparer.Instance));
        }
        if(ids.Count>1)throw new InvalidOperationException("The request contains conflicting ProjectId values.");
        return ids.Count==1?ids.Single():null;
    }
    private static void CollectProjectIds(object value,ISet<Guid> ids,int depth,ISet<object> visited)
    {
        if(depth>3||value is string||value.GetType().IsPrimitive||value.GetType().IsEnum)return;
        if(!value.GetType().IsValueType&&!visited.Add(value))return;
        if(value is System.Collections.IEnumerable enumerable){foreach(var item in enumerable)if(item is not null)CollectProjectIds(item,ids,depth+1,visited);return;}
        foreach(var prop in value.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public))
        {
            if(prop.GetIndexParameters().Length>0||!prop.CanRead)continue;object? member;try{member=prop.GetValue(value);}catch{continue;}
            if(string.Equals(prop.Name,"ProjectId",StringComparison.OrdinalIgnoreCase)&&member is Guid id&&id!=Guid.Empty){ids.Add(id);continue;}
            if(member is not null&&prop.PropertyType!=typeof(string)&&!prop.PropertyType.IsPrimitive&&!prop.PropertyType.IsEnum)CollectProjectIds(member,ids,depth+1,visited);
        }
    }
}
