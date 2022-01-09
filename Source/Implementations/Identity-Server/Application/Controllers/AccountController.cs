using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using HansKindberg.IdentityServer.Application.Models.Views.Account;
using HansKindberg.IdentityServer.Collections.Generic.Extensions;
using HansKindberg.IdentityServer.FeatureManagement;
using HansKindberg.IdentityServer.FeatureManagement.Extensions;
using HansKindberg.IdentityServer.Models.Extensions;
using HansKindberg.IdentityServer.Web;
using HansKindberg.IdentityServer.Web.Extensions;
using HansKindberg.IdentityServer.Web.Http.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.FeatureManagement.Mvc;
using RegionOrebroLan.Logging.Extensions;
using RegionOrebroLan.Web.Authentication;
using RegionOrebroLan.Web.Authentication.Security.Claims.Extensions;
using Rsk.Saml.DuendeIdentityServer.Services.Models;

namespace HansKindberg.IdentityServer.Application.Controllers
{
	[Authorize]
	public class AccountController : SiteController
	{
		#region Constructors

		public AccountController(IFacade facade) : base(facade) { }

		#endregion

		#region Methods

		[AllowAnonymous]
		public virtual async Task<IActionResult> AccessDenied()
		{
			return await Task.FromResult(this.View());
		}

		protected internal virtual async Task<SignedOutViewModel> CreateSignedOutViewModelAsync(string signOutId)
		{
			var signOutOptions = this.Facade.IdentityServer.CurrentValue.SignOut;
			var signOutRequest = await this.Facade.Interaction.GetLogoutContextAsync(signOutId);

			var model = new SignedOutViewModel
			{
				AutomaticRedirect = signOutOptions.AutomaticRedirectAfterSignOut,
				Client = string.IsNullOrEmpty(signOutRequest?.ClientName) ? signOutRequest?.ClientId : signOutRequest.ClientName,
				IframeUrl = signOutOptions.SloEnabled ? signOutRequest?.SignOutIFrameUrl : null,
				RedirectUrl = signOutRequest?.PostLogoutRedirectUri,
				SecondsBeforeRedirect = signOutOptions.SecondsBeforeRedirectAfterSignOut
			};

			// ReSharper disable InvertIf
			if(this.Facade.FeatureManager.IsEnabled(Feature.Saml))
			{
				if(signOutOptions.SloEnabled && signOutRequest != null)
					model.SamlIframeUrl = await this.Facade.SamlInteraction.GetSamlSignOutFrameUrl(signOutId, new SamlLogoutRequest(signOutRequest));

				var samlRequestId = await this.GetSamlRequestIdAsync();

				if(samlRequestId != null)
				{
					var redirectUrl = await this.Facade.SamlInteraction.GetLogoutCompletionUrl(samlRequestId);

					if(redirectUrl != null)
						model.RedirectUrl = redirectUrl;
				}
			}
			// ReSharper restore InvertIf

			return model;
		}

		protected internal virtual async Task<SignInViewModel> CreateSignInViewModelAsync(string returnUrl)
		{
			var authorizationRequest = await this.Facade.Interaction.GetAuthorizationContextAsync(returnUrl);

			var model = new SignInViewModel
			{
				Form =
				{
					UserName = authorizationRequest?.LoginHint
				},
				FormsAuthentication =
				{
					AllowPersistent = this.Facade.IdentityServer.CurrentValue.FormsAuthentication.AllowPersistent,
					Duration = this.Facade.IdentityServer.CurrentValue.FormsAuthentication.Duration
				},
				FormsAuthenticationEnabled = this.Facade.FeatureManager.IsEnabled(Feature.FormsAuthentication)
			};

			if(authorizationRequest?.IdP != null)
			{
				var authenticationScheme = await this.Facade.AuthenticationSchemeRetriever.GetAsync(authorizationRequest.IdP);

				if(authenticationScheme != null)
				{
					model.FormsAuthenticationEnabled = model.FormsAuthenticationEnabled && string.Equals(authenticationScheme.Name, Duende.IdentityServer.IdentityServerConstants.LocalIdentityProvider, StringComparison.OrdinalIgnoreCase);

					if(!model.FormsAuthenticationEnabled)
						model.AuthenticationSchemes.Add(authenticationScheme);

					return model;
				}
			}

			var authenticationSchemes = (await this.Facade.AuthenticationSchemeRetriever.ListAsync())
				.Where(authenticationScheme => authenticationScheme.Interactive && authenticationScheme.Kind != AuthenticationSchemeKind.Cookie)
				.OrderBy(authenticationScheme => authenticationScheme.Index)
				.ThenBy(authenticationScheme => authenticationScheme.Name, StringComparer.OrdinalIgnoreCase);

			foreach(var authenticationScheme in authenticationSchemes)
			{
				model.AuthenticationSchemes.Add(authenticationScheme);
			}

			// ReSharper disable InvertIf
			if(authorizationRequest?.Client?.ClientId != null)
			{
				var client = await this.Facade.ClientStore.FindEnabledClientByIdAsync(authorizationRequest.Client.ClientId);

				if(client != null)
				{
					model.FormsAuthenticationEnabled = model.FormsAuthenticationEnabled && client.EnableLocalLogin;

					var authenticationSchemeRestrictions = (client.IdentityProviderRestrictions ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);

					if(authenticationSchemeRestrictions.Any())
					{
						for(var i = model.AuthenticationSchemes.Count - 1; i >= 0; i--)
						{
							if(!authenticationSchemeRestrictions.Contains(model.AuthenticationSchemes[i].Name))
								model.AuthenticationSchemes.RemoveAt(i);
						}
					}
				}
			}
			// ReSharper restore InvertIf

			return model;
		}

		[SuppressMessage("Style", "IDE0016:Use 'throw' expression")]
		protected internal virtual async Task<SignInViewModel> CreateSignInViewModelAsync(SignInForm form, string returnUrl)
		{
			if(form == null)
				throw new ArgumentNullException(nameof(form));

			var model = await this.CreateSignInViewModelAsync(returnUrl);

			model.Form = form;

			return model;
		}

		protected internal virtual async Task<SignOutViewModel> CreateSignOutViewModelAsync(string signOutId)
		{
			var model = new SignOutViewModel
			{
				Confirm = this.Facade.IdentityServer.CurrentValue.SignOut.ConfirmSignOut
			};

			if(this.User.Identity.IsAuthenticated)
			{
				var signOutRequest = await this.Facade.Interaction.GetLogoutContextAsync(signOutId);

				if(signOutRequest != null && !signOutRequest.ShowSignoutPrompt)
					model.Confirm = false;
			}
			else
			{
				model.Confirm = false;
			}

			return model;
		}

		protected internal virtual async Task<string> GetSamlRequestIdAsync()
		{
			var requestIdParameter = this.Facade.IdentityServer.CurrentValue.Saml.UserInteraction.RequestIdParameter;

			return await Task.FromResult(this.HttpContext.Request.Query[requestIdParameter]);
		}

		public virtual async Task<IActionResult> Index()
		{
			return await Task.FromResult(this.View());
		}

		protected internal virtual async Task<AuthorizationRequest> ResolveAndValidateAsync(SignInForm form, string returnUrl)
		{
			if(form == null)
				throw new ArgumentNullException(nameof(form));

			form.Persistent = form.Persistent && this.Facade.IdentityServer.CurrentValue.FormsAuthentication.AllowPersistent;

			return await this.ValidateFormsAuthenticationForClientAsync(returnUrl);
		}

		[AllowAnonymous]
		[SuppressMessage("Style", "IDE0050:Convert to tuple")]
		public virtual async Task<IActionResult> SignIn(string returnUrl)
		{
			returnUrl = this.ResolveAndValidateReturnUrl(returnUrl);

			var model = await this.CreateSignInViewModelAsync(returnUrl);

			// ReSharper disable InvertIf
			if(!model.FormsAuthenticationEnabled && model.AuthenticationSchemes.Count == 1)
			{
				var authenticationScheme = model.AuthenticationSchemes.First();

				return this.RedirectToAction(authenticationScheme.Kind.ToString(), "Authenticate", new { authenticationScheme = authenticationScheme.Name, returnUrl });
			}
			// ReSharper restore InvertIf

			return this.View(model);
		}

		[AllowAnonymous]
		[FeatureGate(Feature.FormsAuthentication)]
		[HttpPost]
		[ValidateAntiForgeryToken]
		[SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Validated in another method.")]
		public virtual async Task<IActionResult> SignIn(SignInForm form, string returnUrl)
		{
			returnUrl = this.ResolveAndValidateReturnUrl(returnUrl);

			var authorizationRequest = await this.ResolveAndValidateAsync(form, returnUrl);

			if(form.Cancel)
			{
				// ReSharper disable InvertIf
				if(authorizationRequest != null)
				{
					await this.Facade.Interaction.DenyAuthorizationAsync(authorizationRequest, AuthorizationError.AccessDenied);

					if(authorizationRequest.IsNativeClient())
						return await this.Redirect(returnUrl, this.Facade.IdentityServer.CurrentValue.SignOut.SecondsBeforeRedirectAfterSignOut);
				}
				// ReSharper restore InvertIf

				return this.Redirect(returnUrl);
			}

			if(this.ModelState.IsValid)
			{
				var signInResult = await this.Facade.Identity.SignInAsync(form.Password, form.Persistent, form.UserName);

				if(signInResult.Succeeded)
				{
					var user = await this.Facade.Identity.GetUserAsync(form.UserName);

					if(user == null)
						throw new InvalidOperationException($"The user \"{form.UserName}\" does not exist.");

					await this.Facade.Events.RaiseAsync(new UserLoginSuccessEvent(user.UserName, user.Id, user.UserName, clientId: authorizationRequest?.Client.ClientId));

					if(authorizationRequest != null && authorizationRequest.IsNativeClient())
						return await this.Redirect(returnUrl, this.Facade.IdentityServer.CurrentValue.SignOut.SecondsBeforeRedirectAfterSignOut);

					return this.Redirect(returnUrl);
				}

				this.Logger.LogDebugIfEnabled($"Sign-in for user \"{form.UserName}\" failed. Result: {signInResult}");

				await this.Facade.Events.RaiseAsync(new UserLoginFailureEvent(form.UserName, "invalid credentials", clientId: authorizationRequest?.Client.ClientId));

				this.ModelState.AddModelError(string.Empty, this.Localizer.GetString("errors/invalid-username-or-password"));
			}

			var model = await this.CreateSignInViewModelAsync(form, returnUrl);

			return await Task.FromResult(this.View(model));
		}

		[AllowAnonymous]
		public virtual async Task<IActionResult> SignOut(string signOutId)
		{
			var model = await this.CreateSignOutViewModelAsync(signOutId);

			if(!model.Confirm)
				return await this.SignOut(model.Form, signOutId);

			return this.View(model);
		}

		/// <summary>
		/// The form-parameter is never used. We keep it here so we can distinguish the SignOut actions http-get and http-post.
		/// </summary>
		[AllowAnonymous]
		[HttpPost]
		[ValidateAntiForgeryToken]
		[SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling")]
		public virtual async Task<IActionResult> SignOut(SignOutForm form, string signOutId)
		{
			if(this.Facade.IdentityServer.CurrentValue.SignOut.IdpInitiatedSloEnabled)
				signOutId ??= await this.Facade.Interaction.CreateLogoutContextAsync();

			var model = await this.CreateSignedOutViewModelAsync(signOutId);
			string externalSignOutScheme = null;

			if(this.User.Identity.IsAuthenticated)
			{
				var authenticationSchemeName = this.User.Claims.FindFirstIdentityProviderClaim()?.Value;

				if(authenticationSchemeName != null && !string.Equals(authenticationSchemeName, Duende.IdentityServer.IdentityServerConstants.LocalIdentityProvider, StringComparison.OrdinalIgnoreCase))
				{
					var authenticationScheme = await this.Facade.AuthenticationSchemeRetriever.GetAsync(authenticationSchemeName);

					if(authenticationScheme != null && authenticationScheme.SignOutSupport)
					{
						externalSignOutScheme = authenticationSchemeName;
						signOutId ??= await this.Facade.Interaction.CreateLogoutContextAsync();
					}
				}

				await this.Facade.Identity.SignOutAsync();

				await this.Facade.Events.RaiseAsync(new UserLogoutSuccessEvent(User.GetSubjectId(), User.GetDisplayName()));
			}

			// ReSharper disable InvertIf
			if(externalSignOutScheme != null)
			{
				var query = this.HttpContext.Request.Query.ToSortedDictionary().Set(QueryStringKeys.SignOutId, signOutId);

				var redirectUrl = this.Url.Action("SignOut", query);

				return this.SignOut(new AuthenticationProperties { RedirectUri = redirectUrl }, externalSignOutScheme);
			}
			// ReSharper restore InvertIf

			this.HttpContext.SetSignedOut();

			return this.View("SignedOut", model);
		}

		protected internal virtual async Task<AuthorizationRequest> ValidateFormsAuthenticationForClientAsync(string returnUrl)
		{
			var authorizationRequest = await this.Facade.Interaction.GetAuthorizationContextAsync(returnUrl);

			var clientId = authorizationRequest?.Client?.ClientId;

			if(clientId == null)
				return authorizationRequest;

			var client = await this.Facade.ClientStore.FindEnabledClientByIdAsync(clientId);

			if(client == null)
				return authorizationRequest;

			if(!client.EnableLocalLogin)
				throw new InvalidOperationException($"Forms-authentication is not enabled for client \"{clientId}\".");

			return authorizationRequest;
		}

		#endregion
	}
}