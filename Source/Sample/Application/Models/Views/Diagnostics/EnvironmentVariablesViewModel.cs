using System;
using System.Collections.Generic;

namespace Application.Models.Views.Diagnostics
{
	public class EnvironmentVariablesViewModel
	{
		#region Properties

		public virtual IDictionary<string, string> Items { get; } = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		#endregion
	}
}