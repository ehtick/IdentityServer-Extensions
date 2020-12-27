using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Application.Controllers
{
	[Route("identity")]
	public class IdentityController : ControllerBase
	{
		#region Constructors

		public IdentityController(ILoggerFactory loggerFactory)
		{
			this.Logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger(this.GetType());
		}

		#endregion

		#region Properties

		protected internal virtual ILogger Logger { get; }

		#endregion

		#region Methods

		[HttpGet]
		[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
		[SuppressMessage("Style", "IDE0050:Convert to tuple")]
		public virtual async Task<IActionResult> Get()
		{
			var claims = this.User.Claims.Select(c => new {c.Type, c.Value});

			if(this.Logger.IsEnabled(LogLevel.Information))
				this.Logger.LogInformation("claims: {claims}", claims);

			return await Task.FromResult(new JsonResult(claims));
		}

		#endregion
	}
}