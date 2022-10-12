using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HansKindberg.IdentityServer.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTests.Security.Claims
{
	[TestClass]
	public class ClaimBasedCountySelectorTest : ClaimsSelectorTestBase
	{
		#region Methods

		protected internal virtual async Task<ClaimBasedCountySelector> CreateClaimBasedCountySelectorAsync(string claimsFileName, string commissionsFileName, string authenticationType = "Test", string commissionsClaimType = "commissions")
		{
			var claimsPrincipal = await this.CreateClaimsPrincipalAsync(claimsFileName, commissionsFileName, authenticationType, commissionsClaimType);

			var httpContextMock = new Mock<HttpContext>();
			httpContextMock.Setup(httpContext => httpContext.User).Returns(claimsPrincipal);
			var httpContext = httpContextMock.Object;

			var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
			httpContextAccessorMock.Setup(httpContextAccessor => httpContextAccessor.HttpContext).Returns(httpContext);
			var httpContextAccessor = httpContextAccessorMock.Object;

			var claimBasedCountySelector = new ClaimBasedCountySelector(httpContextAccessor, Mock.Of<ILoggerFactory>());

			return claimBasedCountySelector;
		}

		[TestMethod]
		public async Task GetClaimsAsync_ShouldWorkProperly()
		{
			var claimBasedCountySelector = await this.CreateClaimBasedCountySelectorAsync("Claims-2", "Commissions-2");

			var result = await claimBasedCountySelector.SelectAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

			var claims = await claimBasedCountySelector.GetClaimsAsync(result);

			Assert.AreEqual(11, claims.Count);

			var (key, value) = claims.ElementAt(0);
			Assert.AreEqual("selected_commissionHsaId", key);
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("selected_commissionHsaId", value.First().Type);
			Assert.AreEqual("TEST123456-c002", value.First().Value);

			(key, value) = claims.ElementAt(1);
			Assert.AreEqual("selected_commissionName", key);
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("selected_commissionName", value.First().Type);

			(key, value) = claims.ElementAt(2);
			Assert.AreEqual("selected_commissionPurpose", key);
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("selected_commissionPurpose", value.First().Type);

			(key, value) = claims.ElementAt(3);
			Assert.AreEqual("selected_commissionRight", key);
			Assert.AreEqual(3, value.Count);
			Assert.AreEqual("selected_commissionRight", value.First().Type);
			Assert.AreEqual("selected_commissionRight", value.ElementAt(1).Type);
			Assert.AreEqual("selected_commissionRight", value.ElementAt(2).Type);

			(key, value) = claims.ElementAt(4);
			Assert.AreEqual("selected_employeeHsaId", key);
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("selected_employeeHsaId", value.First().Type);
			Assert.AreEqual("TEST123456-e001", value.First().Value);

			(key, value) = claims.ElementAt(5);
			Assert.AreEqual("selected_healthCareProviderHsaId", key);
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("selected_healthCareProviderHsaId", value.First().Type);

			(key, value) = claims.ElementAt(6);
			Assert.AreEqual("selected_healthCareProviderName", key);
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("selected_healthCareProviderName", value.First().Type);

			(key, value) = claims.ElementAt(7);
			Assert.AreEqual("selected_healthCareProviderOrgNo", key);
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("selected_healthCareProviderOrgNo", value.First().Type);

			(key, value) = claims.ElementAt(8);
			Assert.AreEqual("selected_healthCareUnitHsaId", key);
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("selected_healthCareUnitHsaId", value.First().Type);

			(key, value) = claims.ElementAt(9);
			Assert.AreEqual("selected_healthCareUnitName", key);
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("selected_healthCareUnitName", value.First().Type);

			(key, value) = claims.ElementAt(10);
			Assert.AreEqual("selected_healthCareUnitStartDate", key);
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("selected_healthCareUnitStartDate", value.First().Type);
		}

		[TestMethod]
		public async Task SelectAsync_ShouldWorkProperly()
		{
			// First test
			var claimBasedCountySelector = await this.CreateClaimBasedCountySelectorAsync("Claims-1", "Commissions-1");

			var result = await claimBasedCountySelector.SelectAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

			Assert.IsNotNull(result);
			Assert.IsFalse(result.Complete);
			Assert.AreEqual(1, result.Selectables.Count);
			Assert.AreEqual(3, result.Selectables[claimBasedCountySelector.Group].Count);

			// Second test
			var value = result.Selectables[claimBasedCountySelector.Group].First().Value;
			var selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				{ claimBasedCountySelector.Group, value }
			};

			result = await claimBasedCountySelector.SelectAsync(selections);

			Assert.IsNotNull(result);
			Assert.IsTrue(result.Complete);
			Assert.AreEqual(1, result.Selectables.Count);
			Assert.AreEqual(3, result.Selectables[claimBasedCountySelector.Group].Count);
			Assert.IsTrue(result.Selectables[claimBasedCountySelector.Group].First().Selected);

			// Third test
			claimBasedCountySelector = await this.CreateClaimBasedCountySelectorAsync("Claims-2", "Commissions-2");

			result = await claimBasedCountySelector.SelectAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

			Assert.IsNotNull(result);
			Assert.IsTrue(result.Complete);
			Assert.AreEqual(1, result.Selectables.Count);
			Assert.AreEqual(4, result.Selectables[claimBasedCountySelector.Group].Count);
			Assert.IsTrue(result.Selectables[claimBasedCountySelector.Group].ElementAt(1).Selected);
		}

		#endregion
	}
}