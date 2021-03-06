﻿using Oqtane.Models;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System.Reflection;
using System;
using Oqtane.Shared;

namespace Oqtane.Services
{
    public class ThemeService : ServiceBase, IThemeService
    {
        private readonly HttpClient _http;
        private readonly SiteState _siteState;
        private readonly NavigationManager _navigationManager;

        public ThemeService(HttpClient http, SiteState siteState, NavigationManager navigationManager)
        {
            _http = http;
            _siteState = siteState;
            _navigationManager = navigationManager;
        }

        private string apiurl
        {
            get { return CreateApiUrl(_siteState.Alias, _navigationManager.Uri, "Theme"); }
        }

        public async Task<List<Theme>> GetThemesAsync()
        {
            List<Theme> themes = await _http.GetJsonAsync<List<Theme>>(apiurl);

            // get list of loaded assemblies
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Theme theme in themes)
            {
                if (theme.Dependencies != "")
                {
                    foreach (string dependency in theme.Dependencies.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string assemblyname = dependency.Replace(".dll", "");
                        if (assemblies.Where(item => item.FullName.StartsWith(assemblyname + ",")).FirstOrDefault() == null)
                        {
                            // download assembly from server and load
                            var bytes = await _http.GetByteArrayAsync(apiurl + "/load/" + assemblyname + ".dll");
                            Assembly.Load(bytes);
                        }
                    }
                }
                if (assemblies.Where(item => item.FullName.StartsWith(theme.AssemblyName + ",")).FirstOrDefault() == null)
                {
                    // download assembly from server and load
                    var bytes = await _http.GetByteArrayAsync(apiurl + "/load/" + theme.AssemblyName + ".dll");
                    Assembly.Load(bytes);
                }
            }

            return themes.OrderBy(item => item.Name).ToList();
        }

        public Dictionary<string, string> GetThemeTypes(List<Theme> Themes)
        {
            var selectableThemes = new Dictionary<string, string>();
            foreach (Theme theme in Themes)
            {
                foreach (string themecontrol in theme.ThemeControls.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    selectableThemes.Add(themecontrol, theme.Name + " - " + Utilities.GetTypeNameLastSegment(themecontrol, 0));
                }
            }
            return selectableThemes;
        }

        public Dictionary<string, string> GetPaneLayoutTypes(List<Theme> Themes, string ThemeName)
        {
            var selectablePaneLayouts = new Dictionary<string, string>();
            foreach (Theme theme in Themes)
            { 
                if (ThemeName.StartsWith(theme.ThemeName))
                {
                    foreach (string panelayout in theme.PaneLayouts.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        selectablePaneLayouts.Add(panelayout, theme.Name + " - " + @Utilities.GetTypeNameLastSegment(panelayout, 0));
                    }
                }
            }
            return selectablePaneLayouts;
        }

        public Dictionary<string, string> GetContainerTypes(List<Theme> Themes)
        {
            var selectableContainers = new Dictionary<string, string>();
            foreach (Theme theme in Themes)
            {
                foreach (string container in theme.ContainerControls.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    selectableContainers.Add(container, theme.Name + " - " + @Utilities.GetTypeNameLastSegment(container, 0));
                }
            }
            return selectableContainers;
        }

        public async Task InstallThemesAsync()
        {
            await _http.GetJsonAsync<List<string>>(apiurl + "/install");
        }

        public async Task DeleteThemeAsync(string ThemeName)
        {
            await _http.DeleteAsync(apiurl + "/" + ThemeName);
        }
    }
}
