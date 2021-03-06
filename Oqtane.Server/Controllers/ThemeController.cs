﻿using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Oqtane.Repository;
using Oqtane.Models;
using Microsoft.AspNetCore.Authorization;
using Oqtane.Shared;
using Oqtane.Infrastructure;
using System.IO;
using System.Reflection;
using System.Linq;
using Microsoft.AspNetCore.Hosting;

namespace Oqtane.Controllers
{
    [Route("{site}/api/[controller]")]
    public class ThemeController : Controller
    {
        private readonly IThemeRepository _themes;
        private readonly IInstallationManager _installationManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogManager _logger;

        public ThemeController(IThemeRepository themes, IInstallationManager installationManager, IWebHostEnvironment environment, ILogManager logger)
        {
            _themes = themes;
            _installationManager = installationManager;
            _environment = environment;
            _logger = logger;
        }

        // GET: api/<controller>
        [HttpGet]
        [Authorize(Roles = Constants.RegisteredRole)]
        public IEnumerable<Theme> Get()
        {
            return _themes.GetThemes();
        }

        [HttpGet("install")]
        [Authorize(Roles = Constants.HostRole)]
        public void InstallThemes()
        {
            _installationManager.InstallPackages("Themes", true);
            _logger.Log(LogLevel.Information, this, LogFunction.Create, "Themes Installed");
        }

        // DELETE api/<controller>/xxx
        [HttpDelete("{themename}")]
        [Authorize(Roles = Constants.HostRole)]
        public void Delete(string themename)
        {
            List<Theme> themes = _themes.GetThemes().ToList();
            Theme theme = themes.Where(item => item.ThemeName == themename).FirstOrDefault();
            if (theme != null)
            {
                themename = theme.ThemeName.Substring(0, theme.ThemeName.IndexOf(","));

                string folder = Path.Combine(_environment.WebRootPath, "Themes\\" + themename);
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }

                string binfolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                foreach (string file in Directory.EnumerateFiles(binfolder, themename + "*.dll"))
                {
                    System.IO.File.Delete(file);
                }
                _logger.Log(LogLevel.Information, this, LogFunction.Delete, "Theme Deleted {ThemeName}", themename);

                _installationManager.RestartApplication();
            }
        }

        // GET api/<controller>/load/filename
        [HttpGet("load/{filename}")]
        public IActionResult Load(string filename)
        {
            string binfolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            byte[] file = System.IO.File.ReadAllBytes(Path.Combine(binfolder, filename));
            return File(file, "application/octet-stream", filename);
        }
    }
}
