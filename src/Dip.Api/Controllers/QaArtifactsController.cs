// Purpose: Exposes Q aA rt if ac ts HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/qa/runs/{runId:guid}/artifacts")]
[Authorize(Policy = Policies.ProjectRead)]
[RequireProjectResource(ProjectMembershipRole.Viewer, ProjectResourceKind.QaRun, "runId")]
public sealed class QaArtifactsController(IQaRunRepository runs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Dip.Application.Features.QualityAutomation.QaArtifactDto>>> List(Guid runId, CancellationToken ct)
    {
        var run = await runs.GetAsync(runId, ct);
        if (run is null) return NotFound();
        var root = Path.GetFullPath(run.ArtifactRoot);
        if (!Directory.Exists(root)) return Ok(Array.Empty<Dip.Application.Features.QualityAutomation.QaArtifactDto>());
        var items = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => new FileInfo(path)).OrderBy(x => x.FullName).Take(500).Select(info =>
        {
            var relative = Path.GetRelativePath(root, info.FullName).Replace(Path.DirectorySeparatorChar, '/'); var kind = Path.GetExtension(info.Name).ToLowerInvariant() switch { ".mp4" => "video", ".wav" => "audio", ".dmp" => "dump", ".json" => "json", ".log" or ".txt" => "log", _ => "file" };
            return new Dip.Application.Features.QualityAutomation.QaArtifactDto(relative, info.Length, kind);
        }).ToArray(); return Ok(items);
    }

    [HttpGet("{**relativePath}")]
    public async Task<IActionResult> Get(Guid runId, string relativePath, CancellationToken ct)
    {
        var run = await runs.GetAsync(runId, ct); if (run is null) return NotFound(); if (string.IsNullOrWhiteSpace(relativePath)) return BadRequest();
        var root = Path.GetFullPath(run.ArtifactRoot); var file = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; if (!file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(file)) return NotFound();
        var contentType = Path.GetExtension(file).ToLowerInvariant() switch { ".mp4" => "video/mp4", ".wav" => "audio/wav", ".json" => "application/json", ".log" or ".txt" => "text/plain", ".dmp" => "application/octet-stream", _ => "application/octet-stream" };
        return PhysicalFile(file, contentType, enableRangeProcessing: true);
    }
}
