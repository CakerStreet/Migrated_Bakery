using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers;

/// <summary>
/// Serves file downloads as attachments.
/// Migrated from legacy WebForms page: download.aspx / download.aspx.cs
/// 
/// Legacy behaviour:
///   - Reads ?file= query string, maps to physical path via Server.MapPath
///   - Validates file extension against an allowlist
///   - Serves the file as application/octet-stream with Content-Disposition: attachment
///
/// Modern version:
///   - Resolves files relative to wwwroot/
///   - Validates against directory traversal attacks (canonical path must stay within wwwroot)
///   - Same extension allowlist as legacy
///   - Returns PhysicalFileResult for efficient file serving
/// </summary>
[Route("download")]
[Route("download.aspx")]
public class DownloadController : Controller
{
    private readonly IConfiguration _config;

    /// <summary>
    /// Allowed file extensions for download, matching the legacy allowlist from download.aspx.cs.
    /// </summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".png", ".gif", ".jpeg",
        ".studio3", ".zip", ".rar", ".pdf",
        ".dcd", ".psd", ".otf", ".ttf",
        ".fnt", ".stl", ".obj"
    };

    public DownloadController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Serves a file download. The file path is relative to wwwroot.
    /// </summary>
    /// <param name="file">Relative path to the file (e.g. "upload/Product_images/image.jpg").</param>
    /// <returns>The file as an attachment, or an error response.</returns>
    [HttpGet]
    public IActionResult Index([FromQuery] string? file)
    {
        // Legacy: if nothing was passed → "Please provide a file to download."
        if (string.IsNullOrWhiteSpace(file))
        {
            return BadRequest("Please provide a file to download.");
        }

        // ── Directory traversal protection ──────────────────────────────
        // Strip leading ~/ or / so we always resolve relative to wwwroot
        string relativePath = file
            .Replace("~/", "")
            .TrimStart('/');

        string wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        string fullPath = Path.GetFullPath(Path.Combine(wwwroot, relativePath));

        // Canonical path must stay within wwwroot
        if (!fullPath.StartsWith(wwwroot, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid file path.");
        }

        // ── File existence check ────────────────────────────────────────
        // Legacy: "This file does not exist."
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound("This file does not exist.");
        }

        // ── Extension allowlist ─────────────────────────────────────────
        string ext = Path.GetExtension(fullPath);
        if (!AllowedExtensions.Contains(ext))
        {
            return BadRequest("File type not allowed for download.");
        }

        // ── Serve the file as attachment ────────────────────────────────
        string fileName = Path.GetFileName(fullPath);
        return PhysicalFile(fullPath, "application/octet-stream", fileName);
    }
}
