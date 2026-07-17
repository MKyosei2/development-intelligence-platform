using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Persistence.Repositories;
using Dip.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Dip.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection s,IConfiguration c)
    {
        var cs=c.GetConnectionString("PostgreSql")??throw new InvalidOperationException("ConnectionStrings:PostgreSql missing.");
        s.AddDbContext<DipDbContext>(o=>o.UseNpgsql(cs));
        s.AddScoped<IProjectRepository,ProjectRepository>();s.AddScoped<IPipelineRunRepository,PipelineRunRepository>();
        s.AddScoped<IMetricRepository,MetricRepository>();s.AddScoped<IQualityIssueRepository,QualityIssueRepository>();
        s.AddScoped<IUnitOfWork>(p=>p.GetRequiredService<DipDbContext>());s.AddSingleton<ISystemClock,SystemClock>();return s;
    }
}
