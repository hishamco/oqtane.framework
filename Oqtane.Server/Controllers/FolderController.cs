﻿using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Oqtane.Repository;
using Oqtane.Models;
using Oqtane.Shared;
using System.Linq;
using Oqtane.Infrastructure;
using Oqtane.Security;

namespace Oqtane.Controllers
{
    [Route("{site}/api/[controller]")]
    public class FolderController : Controller
    {
        private readonly IFolderRepository _folders;
        private readonly IUserPermissions _userPermissions;
        private readonly ILogManager _logger;

        public FolderController(IFolderRepository folders, IUserPermissions userPermissions, ILogManager logger)
        {
            _folders = folders;
            _userPermissions = userPermissions;
            _logger = logger;
        }

        // GET: api/<controller>?siteid=x
        [HttpGet]
        public IEnumerable<Folder> Get(string siteid)
        {
            List<Folder> folders = new List<Folder>();
            foreach(Folder folder in _folders.GetFolders(int.Parse(siteid)))
            {
                if (_userPermissions.IsAuthorized(User, "Browse", folder.Permissions))
                {
                    folders.Add(folder);
                }
            }
            return folders;
        }

        // GET api/<controller>/5
        [HttpGet("{id}")]
        public Folder Get(int id)
        {
            Folder folder = _folders.GetFolder(id);
            if (_userPermissions.IsAuthorized(User, "Browse", folder.Permissions))
            {
                return folder;
            }
            else
            {
                _logger.Log(LogLevel.Error, this, LogFunction.Read, "User Not Authorized To Access Folder {Folder}", folder);
                HttpContext.Response.StatusCode = 401;
                return null;
            }
        }

        // POST api/<controller>
        [HttpPost]
        [Authorize(Roles = Constants.RegisteredRole)]
        public Folder Post([FromBody] Folder Folder)
        {
            if (ModelState.IsValid)
            {
                string permissions;
                if (Folder.ParentId != null)
                {
                    permissions = _folders.GetFolder(Folder.ParentId.Value).Permissions;
                }
                else
                {
                    permissions = UserSecurity.SetPermissionStrings(new List<PermissionString> { new PermissionString { PermissionName = "Edit", Permissions = Constants.AdminRole } });
                }
                if (_userPermissions.IsAuthorized(User, "Edit", permissions))
                {
                    if (string.IsNullOrEmpty(Folder.Path) && Folder.ParentId != null)
                    {
                        Folder parent = _folders.GetFolder(Folder.ParentId.Value);
                        Folder.Path = parent.Path + Folder.Name + "\\";
                    }
                    Folder = _folders.AddFolder(Folder);
                    _logger.Log(LogLevel.Information, this, LogFunction.Create, "Folder Added {Folder}", Folder);
                }
                else
                {
                    _logger.Log(LogLevel.Error, this, LogFunction.Create, "User Not Authorized To Add Folder {Folder}", Folder);
                    HttpContext.Response.StatusCode = 401;
                    Folder = null;
                }
            }
            return Folder;
        }

        // PUT api/<controller>/5
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.RegisteredRole)]
        public Folder Put(int id, [FromBody] Folder Folder)
        {
            if (ModelState.IsValid && _userPermissions.IsAuthorized(User, "Folder", Folder.FolderId, "Edit"))
            {
                if (string.IsNullOrEmpty(Folder.Path) && Folder.ParentId != null)
                {
                    Folder parent = _folders.GetFolder(Folder.ParentId.Value);
                    Folder.Path = parent.Path + Folder.Name + "\\";
                }
                Folder = _folders.UpdateFolder(Folder);
                _logger.Log(LogLevel.Information, this, LogFunction.Update, "Folder Updated {Folder}", Folder);
            }
            else
            {
                _logger.Log(LogLevel.Error, this, LogFunction.Update, "User Not Authorized To Update Folder {Folder}", Folder);
                HttpContext.Response.StatusCode = 401;
                Folder = null;
            }
            return Folder;
        }

        // PUT api/<controller>/?siteid=x&folderid=y&parentid=z
        [HttpPut]
        [Authorize(Roles = Constants.RegisteredRole)]
        public void Put(int siteid, int folderid, int? parentid)
        {
            if (_userPermissions.IsAuthorized(User, "Folder", folderid, "Edit"))
            {
                int order = 1;
                List<Folder> folders = _folders.GetFolders(siteid).ToList();
                foreach (Folder folder in folders.Where(item => item.ParentId == parentid).OrderBy(item => item.Order))
                {
                    if (folder.Order != order)
                    {
                        folder.Order = order;
                        _folders.UpdateFolder(folder);
                    }
                    order += 2;
                }
                _logger.Log(LogLevel.Information, this, LogFunction.Update, "Folder Order Updated {SiteId} {FolderId} {ParentId}", siteid, folderid, parentid);
            }
            else
            {
                _logger.Log(LogLevel.Error, this, LogFunction.Update, "User Not Authorized To Update Folder Order {SiteId} {FolderId} {ParentId}", siteid, folderid, parentid);
                HttpContext.Response.StatusCode = 401;
            }
        }

        // DELETE api/<controller>/5
        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.RegisteredRole)]
        public void Delete(int id)
        {
            if (_userPermissions.IsAuthorized(User, "Folder", id, "Edit"))
            {
                _folders.DeleteFolder(id);
                _logger.Log(LogLevel.Information, this, LogFunction.Delete, "Folder Deleted {FolderId}", id);
            }
            else
            {
                _logger.Log(LogLevel.Error, this, LogFunction.Delete, "User Not Authorized To Delete Folder {FolderId}", id);
                HttpContext.Response.StatusCode = 401;
            }
        }
    }
}
