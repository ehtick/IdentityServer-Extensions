using HansKindberg.IdentityServer.Hosting;

namespace Application
{
	public static class Program
	{
		#region Methods

		public static int Main(string[] args)
		{
			return Host.Run<Startup>("Hans Kindberg - IdentityServer sample application", args);
		}

		#endregion
	}
}