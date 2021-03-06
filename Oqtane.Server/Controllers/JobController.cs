﻿using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Oqtane.Repository;
using Oqtane.Models;
using Oqtane.Shared;
using Oqtane.Infrastructure;
using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Oqtane.Controllers
{
    [Route("{site}/api/[controller]")]
    public class JobController : Controller
    {
        private readonly IJobRepository _jobs;
        private readonly ILogManager _logger;
        private readonly IServiceProvider _serviceProvider;

        public JobController(IJobRepository jobs, ILogManager logger, IServiceProvider serviceProvider)
        {
            _jobs = jobs;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        // GET: api/<controller>
        [HttpGet]
        [Authorize(Roles = Constants.HostRole)]
        public IEnumerable<Job> Get()
        {
            return _jobs.GetJobs();
        }

        // GET api/<controller>/5
        [HttpGet("{id}")]
        [Authorize(Roles = Constants.HostRole)]
        public Job Get(int id)
        {
            return _jobs.GetJob(id);
        }

        // POST api/<controller>
        [HttpPost]
        [Authorize(Roles = Constants.HostRole)]
        public Job Post([FromBody] Job Job)
        {
            if (ModelState.IsValid)
            {
                Job = _jobs.AddJob(Job);
                _logger.Log(LogLevel.Information, this, LogFunction.Create, "Job Added {Job}", Job);
            }
            return Job;
        }

        // PUT api/<controller>/5
        [HttpPut("{id}")]
        [Authorize(Roles = Constants.HostRole)]
        public Job Put(int id, [FromBody] Job Job)
        {
            if (ModelState.IsValid)
            {
                Job = _jobs.UpdateJob(Job);
                _logger.Log(LogLevel.Information, this, LogFunction.Update, "Job Updated {Job}", Job);
            }
            return Job;
        }

        // DELETE api/<controller>/5
        [HttpDelete("{id}")]
        [Authorize(Roles = Constants.HostRole)]
        public void Delete(int id)
        {
            _jobs.DeleteJob(id);
            _logger.Log(LogLevel.Information, this, LogFunction.Delete, "Job Deleted {JobId}", id);
        }

        // GET api/<controller>/start
        [HttpGet("start/{id}")]
        [Authorize(Roles = Constants.HostRole)]
        public void Start(int id)
        {
            Job job = _jobs.GetJob(id);
            Type jobtype = Type.GetType(job.JobType);
            if (jobtype != null)
            {
                var jobobject = ActivatorUtilities.CreateInstance(_serviceProvider, jobtype);
                ((IHostedService)jobobject).StartAsync(new System.Threading.CancellationToken());
            }
        }

        // GET api/<controller>/stop
        [HttpGet("stop/{id}")]
        [Authorize(Roles = Constants.HostRole)]
        public void Stop(int id)
        {
            Job job = _jobs.GetJob(id);
            Type jobtype = Type.GetType(job.JobType);
            if (jobtype != null)
            {
                var jobobject = ActivatorUtilities.CreateInstance(_serviceProvider, jobtype);
                ((IHostedService)jobobject).StopAsync(new System.Threading.CancellationToken());
            }
        }
    }
}
