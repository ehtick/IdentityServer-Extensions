namespace Application.Models.Views.Account
{
	public class SignedOutViewModel
	{
		#region Properties

		/// <summary>
		/// Local sign-out only. If false it will be a single sign-out.
		/// </summary>
		public virtual bool Locally { get; set; }

		#endregion
	}
}